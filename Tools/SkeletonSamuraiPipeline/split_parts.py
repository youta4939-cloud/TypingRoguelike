# Skeleton samurai: cut-out + part split (workflow of paper_angel_parts / bigmouth_parts)
# inputs in work/: src.png (1024x1536 original), mask_birefnet-general.png (matte), sam_*.png (SAM part masks)
import numpy as np,cv2,json
from scipy import ndimage as ndi
W='/workspace/art/enemies/skeleton_samurai_parts/work/'; OUT='/workspace/art/enemies/skeleton_samurai_parts/'
img=cv2.imread(W+'src.png').astype(np.float32); h,w=img.shape[:2]
Y,X=np.mgrid[0:h,0:w]; L=img.mean(2)
def sam(n,dil=2):
    m=cv2.imread(W+f'sam_{n}.png',0)>127
    return cv2.dilate(m.astype(np.uint8),np.ones((2*dil+1,2*dil+1)))>0 if dil else m
def box(x0,y0,x1,y1): return (X>=x0)&(X<=x1)&(Y>=y0)&(Y<=y1)
def poly(pts):
    m=np.zeros((h,w),np.uint8); cv2.fillPoly(m,[np.array(pts,np.int32)],1); return m>0
def soft(m,s=0.8): return cv2.GaussianBlur(m.astype(np.float32),(0,0),s)
# ---------- alpha + white decontamination (no white halo)
a0=cv2.imread(W+'mask_birefnet-general.png',0).astype(np.float32)/255
a=np.clip((a0-0.04)/0.9,0,1)
n,lab,st,_=cv2.connectedComponentsWithStats((a>0.15).astype(np.uint8))
keep=np.isin(lab,[i for i in range(1,n) if st[i,4]>25]); keep=cv2.dilate(keep.astype(np.uint8),np.ones((5,5)))>0
a*=keep
BG=np.array([254,254,254],np.float32)
a3=np.maximum(a,1e-3)[...,None]
col=np.clip((img-(1-a3)*BG)/a3,0,255)
# a pixel that is near-white after unmixing at low alpha is residual paper/background -> drop alpha a bit
inner=(a>0.97)
_,ind=ndi.distance_transform_edt(~inner,return_indices=True)
near=img[ind[0],ind[1]]
edge=(a<0.97)&(a>0)
mix=(np.clip((0.97-a)/0.7,0,1)*0.5)[...,None]
col=np.where(edge[...,None],col*(1-mix)+near*mix,col)
# re-derive alpha so composite over white still matches original: a' = (255-img)/(255-col)
den=np.maximum((BG-col).mean(2),8)
a_fix=np.clip((BG-img).mean(2)/den,0,1)
a=np.where(edge,np.minimum(a,np.maximum(a_fix,0)),a)
col[a<=0]=0
fg=a>0.004
# ---------- part masks (front-most first)
hat=sam('hat',1)&fg
skull=sam('skull',1)&fg&~hat
talL=((sam('talL',3)|sam('chainL1',3))&box(292,228,342,470))&fg&~hat
chLo=(fg&box(260,232,291,415))&~hat
chLi=(sam('chainL2',3)&box(352,236,385,380))&fg&~hat
talR=(sam('talR',3)&box(645,226,700,345))&fg&~hat
chR=(fg&box(728,212,762,340))&~hat
hang={'hat_talisman_L':talL,'hat_chain_L_outer':chLo,'hat_chain_L_inner':chLi,'hat_talisman_R':talR,'hat_chain_R':chR}
anyhang=talL|chLo|chLi|talR|chR
hand=poly([(258,800),(300,762),(346,768),(348,835),(322,884),(262,872)])
katana=(sam('katana',2)|hand)&fg&~anyhang
armR=sam('armR',2)&fg&~katana
rope=sam('rope',1)&fg&~armR&~katana
stripL=sam('stripL',1)&fg&~rope
stripR=(sam('stripR',1)|sam('stripR2',1))&fg&~rope&~stripL
scab=(sam('scab_up2',2)|sam('scab_lo',2))&fg&~armR&~stripR&~rope
legs=(sam('legL',2)|sam('legR',2))&fg&(Y>1150)
front=hat|skull|anyhang|katana|armR|rope|stripL|stripR|scab
rest=fg&~front&~legs
Y0=650   # waist line: torso above, kimono (sway) below
# ---------- fill what is hidden behind moving parts
close=cv2.morphologyEx(rest.astype(np.uint8),cv2.MORPH_CLOSE,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(13,13)))>0
close=ndi.binary_fill_holes(close)
movers_over_body=rope|stripL|stripR|armR|(katana&hand)|skull|(hat&(Y>200))|talR|scab
beneath=(cv2.dilate(movers_over_body.astype(np.uint8),np.ones((5,5)))>0)&(close|((rope|stripL|stripR|armR|skull)))
beneath&=~(katana&~hand)   # blade is over white
holes=(beneath&~rest)|(cv2.dilate(front.astype(np.uint8),np.ones((3,3)))>0)&beneath
src=np.where(rest[...,None],col,0).astype(np.uint8)
filled=cv2.inpaint(src,(holes|~rest).astype(np.uint8)*255&(cv2.dilate(holes.astype(np.uint8),np.ones((9,9)))*255),7,cv2.INPAINT_TELEA).astype(np.float32)
dk=np.median(col[(skull|cv2.dilate(skull.astype(np.uint8),np.ones((21,21)))>0)&rest&(L<60)],axis=0)
under_skull=cv2.dilate(skull.astype(np.uint8),np.ones((9,9)))>0
filled=np.where((under_skull&~rest)[...,None],dk,filled)
base_c=np.where(rest[...,None],col,filled)
base_a=np.where(rest,a,np.where(holes,1.0,0))
layers={}
def rgba(c,al): return np.dstack([np.clip(c,0,255),np.clip(al,0,1)*255]).astype(np.uint8)
def part(m,s=0.6): return (col.copy(),np.where(cv2.dilate(m.astype(np.uint8),np.ones((3,3)))>0,a,0)*np.clip(soft(m,s)*1.6,0,1))
# ---------- torso / kimono
torso_a=base_a*np.clip((Y0+18-Y)/18,0,1)
kim_a=base_a*(Y>=Y0-30)
layers['torso']=(base_c,torso_a)
layers['kimono']=(base_c,kim_a)
# lining: static copy of the solid interior, revealed when the hem waves
solid=cv2.erode(((kim_a>0.98)).astype(np.uint8),cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(25,25)))>0
solid=cv2.morphologyEx(solid.astype(np.uint8),cv2.MORPH_OPEN,np.ones((9,9)))>0
layers['kimono_lining']=(base_c,soft(solid,2.0)*(Y>=Y0))
# legs + fill under the hakama tatters in front of them
lz=cv2.morphologyEx(legs.astype(np.uint8),cv2.MORPH_CLOSE,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(41,41)))>0
lz=ndi.binary_fill_holes(lz)
lext=(cv2.dilate(lz.astype(np.uint8),cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(15,15)))>0)&(Y<1330)&kim_a.astype(bool)
legc=cv2.inpaint(np.where(legs[...,None],col,base_c).astype(np.uint8),((lext|lz)&~legs).astype(np.uint8)*255,5,cv2.INPAINT_TELEA).astype(np.float32)
legc=np.where(legs[...,None],col,legc)
leg_a=np.where(legs,a,np.where((lext|lz)&~legs&(kim_a>0.5),1.0,0))
layers['legs']=(legc,leg_a)
# ---------- front parts
for k,m in [('scabbards',scab),('sash_strip_L',stripL),('sash_rope',rope),('sash_strip_R',stripR),('arm_R',armR),('arm_L_katana',katana),('skull',skull),('hat',hat)]:
    layers[k]=part(m)
for k,m in hang.items(): layers[k]=part(m,0.5)
# ---------- shadow (floor wash from the white background)
sh=np.clip(1-img.mean(2)/254.,0,1)*(Y>1370)*~(cv2.dilate(fg.astype(np.uint8),np.ones((3,3)))>0)
shm=cv2.inpaint((sh*255).astype(np.uint8),((cv2.dilate(fg.astype(np.uint8),np.ones((7,7)))>0)&(Y>1370)).astype(np.uint8)*255,7,cv2.INPAINT_TELEA)/255.
shm=cv2.GaussianBlur(shm,(0,0),1.2)*(Y>1365)
layers['shadow']=(np.zeros_like(img),np.clip(shm*1.1,0,0.85))
# ---------- sway mask (white = hem moves most, black = fixed at waist)
t=np.clip((Y-Y0)/(1345-Y0),0,1); t=t*t*(3-2*t)
sway=(t**1.2)*(kim_a>0.01)
sway=cv2.GaussianBlur(sway,(0,0),3)*(cv2.dilate((kim_a>0.01).astype(np.uint8),np.ones((9,9)))>0)
cv2.imwrite(OUT+'sway_mask_kimono.png',(np.clip(sway,0,1)*255).astype(np.uint8))
# ---------- write
names=['01_shadow','02_legs','03_kimono_lining','04_kimono','05_torso','06_scabbards','07_sash_strip_L','08_sash_rope','09_sash_strip_R','10_arm_R','11_arm_L_katana','12_skull','13_hat',
       '14_hat_talisman_L','15_hat_chain_L_outer','16_hat_chain_L_inner','17_hat_talisman_R','18_hat_chain_R']
for nme in names:
    c,al=layers[nme[3:]]; c=np.where((al>0)[...,None],c,0)
    cv2.imwrite(OUT+nme+'.png',rgba(c,al))
# alt: hem sections L/C/R (bone approach)
import os; os.makedirs(OUT+'alt_hem_sections',exist_ok=True)
secs={'04a_kimono_hem_L':(-1,400),'04b_kimono_hem_C':(400,640),'04c_kimono_hem_R':(640,9999)}
F=24; piv_sec={}
for k,(x0,x1) in secs.items():
    wgt=np.clip((X-(x0-F))/(2*F),0,1)*np.clip(((x1+F)-X)/(2*F),0,1) if x0>=0 else np.clip(((x1+F)-X)/(2*F),0,1)
    if x1>5000: wgt=np.clip((X-(x0-F))/(2*F),0,1)
    al=kim_a*wgt; cv2.imwrite(OUT+'alt_hem_sections/'+k+'.png',rgba(np.where((al>0)[...,None],base_c,0),al))
    xs=X[(al>0.5)&(np.abs(Y-Y0)<40)]; piv_sec[k]=[int(np.median(xs)) if xs.size else int((x0+min(x1,w))/2),Y0]
def top(m):
    ys,xs=np.where(m); y=ys.min(); return [int(np.median(xs[ys<y+6])),int(y)]
def bot(m):
    ys,xs=np.where(m); y=ys.max(); return [int(np.median(xs[ys>y-6])),int(y)]
sk_b=bot(skull)
piv={"canvas":{"w":w,"h":h,"source":"01M5H71RPN6R2RC6YN40DSFDJP.png (1024x1536, used at native size, no upscale)",
  "coords":"pixels, origin top-left of canvas, y down. Every PNG is FULL CANVAS: place all at local (0,0), scale 1, same PPU",
  "draw_order":"back to front = order of 'parts'","facing":"front / slightly toward screen-left (no flip needed)"},
 "parts":{
 "01_shadow":{"pivot":[515,1440]},
 "02_legs":{"pivot":[515,1440],"note":"static"},
 "03_kimono_lining":{"pivot":[515,Y0],"note":"static backing; shows when the hem waves"},
 "04_kimono":{"pivot":[515,Y0],"note":"sway with shader using sway_mask_kimono.png (recommended) or alt_hem_sections"},
 "05_torso":{"pivot":[515,Y0],"note":"root of the upper body; breathing = scaleY 1.000-1.004 at this pivot"},
 "06_scabbards":{"pivot":[515,Y0],"parent":"05_torso","note":"static with torso"},
 "07_sash_strip_L":{"pivot":top(stripL),"parent":"05_torso","swing_deg":2.5},
 "08_sash_rope":{"pivot":top(rope),"parent":"05_torso","swing_deg":2.0},
 "09_sash_strip_R":{"pivot":top(stripR),"parent":"05_torso","swing_deg":2.5},
 "10_arm_R":{"pivot":top(armR),"parent":"05_torso","note":"elbow (in sleeve). screen-right arm resting on hilts","swing_deg":0.6},
 "11_arm_L_katana":{"pivot":[316,790],"parent":"05_torso","note":"wrist inside sleeve; hand+katana one piece","swing_deg":0.6},
 "12_skull":{"pivot":sk_b,"parent":"05_torso","note":"neck","swing_deg":0.8},
 "13_hat":{"pivot":sk_b,"parent":"12_skull"},
 "14_hat_talisman_L":{"pivot":top(talL),"parent":"13_hat","swing_deg":6},
 "15_hat_chain_L_outer":{"pivot":top(chLo),"parent":"13_hat","swing_deg":4},
 "16_hat_chain_L_inner":{"pivot":top(chLi),"parent":"13_hat","swing_deg":4},
 "17_hat_talisman_R":{"pivot":top(talR),"parent":"13_hat","swing_deg":6},
 "18_hat_chain_R":{"pivot":top(chR),"parent":"13_hat","swing_deg":4}},
 "alt_hem_sections":{k:{"pivot":v,"swing_deg":2.0,"note":"use INSTEAD of 04_kimono (not together)"} for k,v in piv_sec.items()},
 "sway_mask":{"file":"sway_mask_kimono.png","for":"04_kimono","waist_y":Y0,"hem_y":1345,"recommended_amplitude_px":"3-4 at the hem (mask=1)"}}
json.dump(piv,open(OUT+'parts_pivots.json','w'),indent=1,ensure_ascii=False)
np.save(W+'col.npy',col); np.save(W+'alpha.npy',a)
print('ok',{k:int(v.sum()) for k,v in [('rest',rest),('legs',legs),('scab',scab),('kat',katana)]})
