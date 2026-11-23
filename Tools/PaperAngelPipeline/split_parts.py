# Paper-angel enemy: cut-out + part split (same workflow as bigmouth_parts/split_parts.py)
# Input: work/up4.png (Real-ESRGAN x4 of source), work/mask_birefnet-general.png (matte), work/edgeL/R.npy (snapped hood edges)
import numpy as np,cv2,json
from scipy import ndimage as ndi
W='/workspace/art/enemies/paper_angel_parts/work/'; OUT='/workspace/art/enemies/paper_angel_parts/'
img=cv2.imread(W+'up4.png').astype(np.float32); h,w=img.shape[:2]
yy,xx=np.mgrid[0:h,0:w]; Y,X=yy,xx
L=img.mean(2)
a0=cv2.imread(W+'mask_birefnet-general.png',0).astype(np.float32)/255
# ---------- alpha cleanup
a=np.clip((a0-0.06)/0.88,0,1)
n,lab,st,_=cv2.connectedComponentsWithStats((a>0.2).astype(np.uint8))
keep=np.isin(lab,[i for i in range(1,n) if st[i,4]>3000]); keep=cv2.dilate(keep.astype(np.uint8),np.ones((5,5)))>0
a*=keep
# ---------- background estimate + colour decontamination (kills grey halo)
S=4; sm=cv2.resize(img,(w//S,h//S),interpolation=cv2.INTER_AREA)
hole=cv2.resize((a>0.01).astype(np.uint8),(w//S,h//S),interpolation=cv2.INTER_NEAREST)
hole=cv2.dilate(hole,np.ones((7,7)))
bgs=cv2.inpaint(sm.astype(np.uint8),hole*255,9,cv2.INPAINT_TELEA)
bg=cv2.GaussianBlur(cv2.resize(bgs,(w,h),interpolation=cv2.INTER_CUBIC).astype(np.float32),(0,0),3)
a3=np.maximum(a,1e-3)[...,None]
col=np.clip((img-(1-a3)*bg)/a3,0,255)
# pull edge pixels toward interior colour (no grey/dark rim)
inner=(a>0.97).astype(np.uint8)
dist,ind=ndi.distance_transform_edt(1-inner,return_indices=True)
near=img[ind[0],ind[1]]
edge=(a<0.97)&(a>0)
mix=np.clip((0.97-a)/0.6,0,1)[...,None]*0.6
col=np.where(edge[...,None],col*(1-mix)+near*mix,col)
col[a<=0]=0
np.save(W+'bg.npy',bg)
def rgba(c,al): return np.dstack([np.clip(c,0,255),np.clip(al,0,1)*255]).astype(np.uint8)
def poly(pts):
    m=np.zeros((h,w),np.uint8); cv2.fillPoly(m,[np.array(pts,np.int32)],1); return m>0
# ---------- hood/cape front edges (wing seams)
yl,xl=np.load(W+'edgeL.npy'); yr,xr=np.load(W+'edgeR.npy')
ys=np.arange(h).astype(float)
def cutL(y):
    y=np.asarray(y,float)
    top=np.interp(y,[361,370],[720,xl[0]])
    sn=np.interp(y,yl,xl)
    ln=587.2+(y-562)*(354-587.2)/(1054-562)
    return np.where(y<370,top,np.where(y<=562,sn,ln))-2   # -2: hood rim stays on body
def cutR(y):
    y=np.asarray(y,float)
    top=np.interp(y,[361,370],[720,xr[0]])
    sn=np.interp(y,yr,xr)
    ln=851.4+(y-540)*(1173-851.4)/(1145-540)
    return np.where(y<370,top,np.where(y<=540,sn,ln))+2
CL=cutL(ys)[:,None]; CR=cutR(ys)[:,None]
fg=a>0.003
zoneL=((X<CL)&(Y<1058))|((Y<361)&(X<720))
zoneR=((X>CR)&(Y<1150))|((Y<361)&(X>=720))
def largest(m,minarea=0):
    n,lb,st,_=cv2.connectedComponentsWithStats(m.astype(np.uint8))
    if minarea: return np.isin(lb,[i for i in range(1,n) if st[i,4]>minarea])
    return lb==(1+np.argmax(st[1:,4]))
wingL=largest(fg&zoneL,800); wingR=largest(fg&zoneR,800)
body_vis=fg&~wingL&~wingR
# ---------- pivots
PL=np.array([610.,797.]); PR=np.array([830.,797.])
SPLIT_L=215.0; SPLIT_R=-35.0          # ray angle (deg, image coords) separating outer/inner blades
def ang(P): return np.degrees(np.arctan2(Y-P[1],X-P[0]))
angL=np.mod(ang(PL),360); angR=ang(PR)
radL=np.hypot(X-PL[0],Y-PL[1]); radR=np.hypot(X-PR[0],Y-PR[1])
outerL=wingL&(angL>=SPLIT_L); innerL=wingL&(angL<SPLIT_L)
outerR=wingR&(angR<=SPLIT_R); innerR=wingR&(angR>SPLIT_R)
# ---------- radial root fill behind the hood (fan blades extended toward the shoulder)
solid_body=body_vis&(a>0.6)
def radial_fill(vis,angm,radm,P,amin,amax,margin=1.5,inset=8):
    bins=np.round(angm*4).astype(int)
    vb=bins[vis]; vr=radm[vis]
    lo,hi=int(amin*4-margin*4),int(amax*4+margin*4)
    rmin=np.full(hi-lo+1,np.inf)
    np.minimum.at(rmin,np.clip(vb-lo,0,hi-lo),vr)
    # fill angle gaps / margins by nearest valid bin
    ok=np.isfinite(rmin); idx=np.arange(len(rmin))
    nearest=np.interp(idx,idx[ok],idx[ok]).round().astype(int)
    src_bin=nearest; r_src=rmin[nearest]
    region=(bins>=lo)&(bins<=hi)&(radm>25)
    b=np.clip(bins-lo,0,hi-lo)
    rs=r_src[b]
    region&=(radm<rs)&solid_body
    # sample colour at radius rs+inset along the source angle
    th=np.radians((src_bin[b]+lo)/4.0)
    sx=P[0]+(rs+inset)*np.cos(th); sy=P[1]+(rs+inset)*np.sin(th)
    core=vis&(a>0.95)
    _,ix=ndi.distance_transform_edt(~core,return_indices=True)
    cfill=col[ix[0],ix[1]]
    colf=np.zeros_like(img)
    for c in range(3):
        colf[...,c]=ndi.map_coordinates(cfill[...,c].astype(np.float32),[sy,sx],order=1,mode='nearest')
    # darken toward the root slightly (it's in the hood's shadow)
    shade=np.clip(0.80+0.20*(radm/np.maximum(rs,1)),0.8,1.0)[...,None]
    return region,colf*shade
def wing_layer(vis_layer,angm,radm,P,mode):
    av=angm[vis_layer]; amin,amax=av.min(),av.max()
    reg,cf=radial_fill(vis_layer,angm,radm,P,amin,amax)
    c=np.where(vis_layer[...,None],col,np.where(reg[...,None],cf,0))
    al=np.where(vis_layer,a,np.where(reg,1.0,0))
    return c,al,reg
def rot_about(arr,P,deg,interp=cv2.INTER_LINEAR):
    M=cv2.getRotationMatrix2D((float(P[0]),float(P[1])),deg,1.0)
    return cv2.warpAffine(arr,M,(w,h),flags=interp,borderValue=0)
layers={}
for side,inn,out,angm,radm,P,sgn in (('L',innerL,outerL,angL,radL,PL,1),('R',innerR,outerR,angR,radR,PR,-1)):
    ci,ai,_=wing_layer(inn,angm,radm,P,'inner')
    co,ao,_=wing_layer(out,angm,radm,P,'outer')
    # inner: extend under the outer blades with rotated copies of itself (an extra blade underneath)
    # cv2 positive angle = counter-clockwise on screen. For L wing, outer side is CW (toward tip/up) -> negative.
    acc_c=ci.copy(); acc_a=ai.copy()
    for d in (4,8,12):
        rc=rot_about(np.dstack([ci*ai[...,None],ai]).astype(np.float32),P,-sgn*d)
        ra=rc[...,3]; rcol=rc[...,:3]/np.maximum(ra,1e-4)[...,None]
        under=(ra>0)&(acc_a<0.999)
        na=acc_a+ra*(1-acc_a)
        acc_c=np.where(under[...,None],(acc_c*acc_a[...,None]+rcol*ra[...,None]*(1-acc_a[...,None]))/np.maximum(na,1e-4)[...,None],acc_c)
        acc_a=na
    # keep the extension only where it is hidden in the rest pose (under outer blades or body)
    hidden=(ao>0.98)|solid_body
    ext=(acc_a>0)&(ai<=0.001)
    acc_a=np.where(ext&~hidden,0,acc_a)
    layers[f'wing_{side}_inner']=(acc_c,acc_a)
    layers[f'wing_{side}_outer']=(co,ao)
# ---------- seal (red spiral) separate
red=(img[...,2]-img[...,1])
disc=np.zeros((h,w),np.uint8)
rr=(red>45)&poly([(560,960),(830,960),(830,1230),(560,1230)])
ysr,xsr=np.where(rr); cx,cy=np.median(xsr),np.median(ysr)
R=np.percentile(np.hypot(xsr-cx,ysr-cy),99)+10
cv2.circle(disc,(int(cx),int(cy)),int(R),1,-1); disc=disc>0
strokes=disc&(red>18)
strokes=cv2.dilate(strokes.astype(np.uint8),np.ones((3,3)))>0
paper=cv2.inpaint(col.astype(np.uint8),strokes.astype(np.uint8)*255,6,cv2.INPAINT_TELEA).astype(np.float32)
# re-add paper texture: high-pass of a nearby clean patch is unreliable; keep smooth fill (hidden under seal)
ring=(cv2.dilate(disc.astype(np.uint8),np.ones((41,41)))>0)&~disc&(red<15)&(a>0.99)
ringc=np.median(col[ring],axis=0)
paper=np.where(strokes[...,None],0.45*cv2.GaussianBlur(paper,(0,0),3)+0.55*ringc,paper)
Sred=np.median(img[disc&(red>70)],axis=0)
d=Sred-paper; t=((img-paper)*d).sum(2)/np.maximum((d*d).sum(2),1)
sa=np.clip(t,0,1)*disc
sa=np.where(strokes,sa,0); sa=cv2.GaussianBlur(sa,(3,3),0.6)*disc
sc=np.clip((img-(1-sa[...,None])*paper)/np.maximum(sa,1e-3)[...,None],0,255); sc[sa<0.02]=Sred
layers['seal']=(sc,sa)
# glow fx (optional, for the pulse; additive)
gm=cv2.GaussianBlur(sa,(0,0),14)*1.6
gm=np.clip(gm,0,1)*np.clip(1-sa*0.3,0,1)
layers['seal_glow_fx']=(np.broadcast_to(np.array([40,40,235],np.float32),img.shape).copy(),gm*0.85)
# ---------- face (folded mask + hood interior), drawn BEHIND the body through the hood opening
FP=[(679,612),(654,641),(622,672),(594,694),(572,716),(562,766),(694,900),(815,778),(803,752),(747,672),(700,628)]
opening=poly(FP)
op_s=cv2.GaussianBlur(opening.astype(np.float32),(0,0),0.9)
ext=cv2.dilate(opening.astype(np.uint8),cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(31,31)))>0
fc=cv2.inpaint(col.astype(np.uint8),(ext&~cv2.erode(opening.astype(np.uint8),np.ones((3,3)))).astype(np.uint8)*255,7,cv2.INPAINT_TELEA).astype(np.float32)
fc=np.where(opening[...,None],col,fc*0.85)   # hidden margin slightly darker (inside the hood)
layers['face']=(fc,ext.astype(np.float32))
body_c=col.copy()
# body under seal: paper fill
body_c=np.where(disc[...,None],paper,body_c)
# ---------- feet
legbox=poly([(390,1690),(600,1690),(600,1908),(390,1908)])|poly([(820,1690),(1030,1690),(1030,1908),(820,1908)])
feet=fg&legbox&(Y>1755)
fa=np.where(feet,a,0)*np.clip((Y-1758)/14,0,1)
layers['feet']=(col.copy(),fa)
body_a=np.where(body_vis,a,0)*(1-op_s)
body_a=np.where(legbox&(Y>1770),body_a*np.clip((1792-Y)/14,0,1),body_a)
layers['body']=(body_c,body_a)
# ---------- floor shadow (darkening of the floor vs. same-row floor far from the figure)
rowref=np.median(np.concatenate([img[:,30:200],img[:,1280:1450]],1).mean(2),axis=1)
shd=np.clip(1-L/rowref[:,None],0,1)*(Y>1780)*poly([(250,1780),(1220,1780),(1220,1908),(250,1908)])
shd=np.where(fg,0,shd)
shm=cv2.inpaint((shd*255).astype(np.uint8),(cv2.dilate(fg.astype(np.uint8),np.ones((9,9)))&(Y>1700)).astype(np.uint8)*255,9,cv2.INPAINT_TELEA)/255.
ell=np.clip(1-(((X-700)/470.)**2+((Y-1872)/60.)**2),0,1)**0.7
shm=cv2.GaussianBlur(shm,(0,0),5)*np.clip((Y-1780)/40,0,1)*ell
layers['shadow']=(np.zeros_like(img),np.clip(shm*1.15,0,0.6))
# ---------- write
names=['01_shadow','02_wing_L_inner','03_wing_L_outer','04_wing_R_inner','05_wing_R_outer','06_feet','07_face_inner','08_body','09_seal','10_seal_glow_fx']
src={'01_shadow':'shadow','02_wing_L_inner':'wing_L_inner','03_wing_L_outer':'wing_L_outer','04_wing_R_inner':'wing_R_inner','05_wing_R_outer':'wing_R_outer','06_feet':'feet','07_face_inner':'face','08_body':'body','09_seal':'seal','10_seal_glow_fx':'seal_glow_fx'}
for n in names:
    c,al=layers[src[n]]; c=np.where((al>0)[...,None],c,0)
    cv2.imwrite(OUT+n+'.png',rgba(c,al))
fa_full=a.copy()
cv2.imwrite(W+'full_cutout.png',rgba(col,a))
piv={"canvas":{"w":w,"h":h,"scale_from_source":4,"source":"01M5GXVA0NS3EN52KSVKVN309H.png (369x477)",
  "coords":"pixels, origin top-left of canvas, y down","upscaler":"Real-ESRGAN x4 (onnx)",
  "draw_order":"back to front = list order below","facing":"frontal/symmetric (no flip needed; game enemies face lower-left)"},
 "parts":{
 "01_shadow":{"pivot":[700,1880],"note":"multiply/alpha black; scale x with float height"},
 "02_wing_L_inner":{"pivot":[int(PL[0]),int(PL[1])],"parent":"08_body","note":"shoulder root (hidden behind hood). screen-left wing, lower blades"},
 "03_wing_L_outer":{"pivot":[int(PL[0]),int(PL[1])],"parent":"08_body","note":"same root; upper blades incl. tip. lag inner by ~0.15 cycle"},
 "04_wing_R_inner":{"pivot":[int(PR[0]),int(PR[1])],"parent":"08_body","note":"screen-right wing, lower blades"},
 "05_wing_R_outer":{"pivot":[int(PR[0]),int(PR[1])],"parent":"08_body"},
 "06_feet":{"pivot":[700,1880],"note":"ground contact"},
 "07_face_inner":{"pivot":[690,640],"parent":"08_body","note":"folded mask + hood interior, seen through the hood opening (drawn behind body). small nod/bob only (<=2deg, <=4px)"},
 "08_body":{"pivot":[700,1880],"note":"hood+robe+cape (opening cut out); root of the rig"},
 "09_seal":{"pivot":[int(cx),int(cy)],"parent":"08_body","note":"chest seal; pulse scale 1.00-1.04"},
 "10_seal_glow_fx":{"pivot":[int(cx),int(cy)],"parent":"08_body","blend":"additive","note":"optional fx; animate opacity 0-0.6"}},
 "wing_limits_deg":{"recommended":"+-10 (outer), +-7 (inner); hidden fill tested to +-14"}}
json.dump(piv,open(OUT+'parts_pivots.json','w'),indent=1,ensure_ascii=False)
print('seal',cx,cy,R)
