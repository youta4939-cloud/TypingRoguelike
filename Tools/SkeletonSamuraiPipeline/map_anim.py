import numpy as np,cv2,json,subprocess,os,sys
from PIL import Image,ImageDraw,ImageFont
D='/workspace/art/enemies/skeleton_samurai_parts/'
P=json.load(open(D+'parts_pivots.json')); pp=P['parts']; order=list(pp.keys())
L={n:cv2.imread(D+n+'.png',-1).astype(np.float32) for n in order}
SW=cv2.imread(D+'sway_mask_kimono.png',0).astype(np.float32)/255
orig=cv2.imread(D+'work/src.png').astype(np.float32); h,w=orig.shape[:2]
FN='/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'
def over(c,Lp): a=Lp[...,3:]/255; return c*(1-a)+Lp[...,:3]*a
JP={'01_shadow':'影','02_legs':'脚・草鞋','03_kimono_lining':'着物の裏地（固定）','04_kimono':'着物（揺れ）','05_torso':'胴体・鎧','06_scabbards':'腰の鞘','07_sash_strip_L':'帯の垂れ布 左','08_sash_rope':'帯の紐','09_sash_strip_R':'帯の垂れ布 右',
'10_arm_R':'右腕（画面右）','11_arm_L_katana':'左腕＋刀（画面左）','12_skull':'頭蓋骨','13_hat':'笠','14_hat_talisman_L':'笠の札 左','15_hat_chain_L_outer':'笠の鎖 左外','16_hat_chain_L_inner':'笠の鎖 左内','17_hat_talisman_R':'笠の札 右','18_hat_chain_R':'笠の鎖 右'}
cols={'01_shadow':(90,90,90),'02_legs':(230,120,230),'03_kimono_lining':(120,120,120),'04_kimono':(230,170,60),'05_torso':(110,185,240),'06_scabbards':(60,90,200),'07_sash_strip_L':(80,220,80),'08_sash_rope':(40,40,230),'09_sash_strip_R':(60,240,200),
'10_arm_R':(250,210,80),'11_arm_L_katana':(0,220,255),'12_skull':(255,255,255),'13_hat':(150,60,200),'14_hat_talisman_L':(80,255,80),'15_hat_chain_L_outer':(255,120,0),'16_hat_chain_L_inner':(255,120,0),'17_hat_talisman_R':(80,255,80),'18_hat_chain_R':(255,120,0)}
if not os.environ.get('ONLYANIM'):
    base=np.full_like(orig,254.)
    for n in order: base=over(base,L[n])
    gray=base.mean(2,keepdims=True).repeat(3,2)*0.45+110
    acc=np.zeros((h,w),np.float32); vis={}
    for n in reversed(order):
        a=L[n][...,3]/255; v=a*(1-acc); vis[n]=v; acc=acc+v
    ov=gray.copy()
    for n in order:
        al=(vis[n]*0.65)[...,None]; ov=ov*(1-al)+np.array(cols[n],np.float32)*al
    S=0.62
    ov=cv2.resize(ov.astype(np.uint8),None,fx=S,fy=S,interpolation=cv2.INTER_AREA)
    ext=np.full((ov.shape[0],ov.shape[1]+330,3),40,np.uint8); ext[:,:ov.shape[1]]=ov
    im=Image.fromarray(cv2.cvtColor(ext,cv2.COLOR_BGR2RGB)); dr=ImageDraw.Draw(im)
    F=ImageFont.truetype(FN,19); f2=ImageFont.truetype(FN,14)
    x0=ov.shape[1]+16
    lst=[n for n in order if n!='03_kimono_lining']
    for i,n in enumerate(lst):
        y=20+i*34; dr.rectangle([x0,y,x0+22,y+22],fill=cols[n][::-1]); dr.text((x0+32,y-2),JP[n],font=F,fill=(255,255,255))
    for n in order:
        if n in ('01_shadow','02_legs','03_kimono_lining','06_scabbards'): continue
        q=pp[n]['pivot']; x,y=q[0]*S,q[1]*S
        dr.ellipse([x-5,y-5,x+5,y+5],outline=(255,255,255),width=2,fill=(255,50,50))
    y=20+len(lst)*34+14
    for t in ['● 赤: 回転軸（ピボット）','※ 左右は画面上の左右','※ 正面〜やや画面左向き：反転不要','※ 着物は sway_mask で揺らす（推奨）']:
        dr.text((x0,y),t,font=f2,fill=(220,220,220)); y+=24
    im.save(D+'parts_map.png')
    # exploded
    tiles=[]
    for n in order+['sway_mask_kimono']:
        if n=='sway_mask_kimono':
            m=SW; Lp=np.dstack([m*255,m*255,m*255,np.full_like(m,255)])
        else: Lp=L[n]
        al=Lp[...,3]; ys,xs=np.where(al>2); c=Lp[ys.min():ys.max()+1,xs.min():xs.max()+1]
        s=0.22 if c.shape[0]>500 else (0.4 if c.shape[0]>150 else 0.7)
        c=cv2.resize(c,None,fx=s,fy=s,interpolation=cv2.INTER_AREA); hh,ww=c.shape[:2]
        yy,xx=np.mgrid[0:hh,0:ww]; chk=np.where(((yy//8+xx//8)%2)[...,None]==0,120,90).astype(np.float32).repeat(3,2)
        tiles.append((n,over(chk,c)))
    Wt=1500; x=10; y=30; rowh=0; canvas=np.full((2400,Wt,3),45,np.float32); pos=[]
    for n,t in tiles:
        hh,ww=t.shape[:2]
        if x+ww>Wt-10: x=10; y+=rowh+38; rowh=0
        canvas[y:y+hh,x:x+ww]=t; pos.append((n,x,y)); x+=max(ww,len(n)*8+10)+18; rowh=max(rowh,hh)
    canvas=canvas[:y+rowh+20]
    im=Image.fromarray(cv2.cvtColor(canvas.astype(np.uint8),cv2.COLOR_BGR2RGB)); dr=ImageDraw.Draw(im)
    for n,x,y in pos: dr.text((x,y-22),n,font=f2,fill=(255,255,255))
    im.save(D+'parts_exploded.png')
if os.environ.get('NOANIM'): sys.exit(0)
# ---------------- sway idle
def M(piv,ang=0,sx=1,sy=1,tx=0,ty=0):
    a=np.deg2rad(ang); c,s=np.cos(a),np.sin(a); px,py=piv
    R=np.array([[c*sx,s*sy],[-s*sx,c*sy]]); t=np.array([px,py])-R@np.array([px,py])+[tx,ty]
    return np.vstack([np.hstack([R,t[:,None]]),[0,0,1]])
Yg,Xg=np.mgrid[0:h,0:w].astype(np.float32)
def wave_kimono(ph,A=3.5):
    # shader-equivalent: uv offset = mask * A * sin(time + phase(x,y))
    dx=A*SW*np.sin(ph-Yg*0.010+Xg*0.006)
    dy=0.8*SW*np.sin(ph*1.0-Yg*0.010+Xg*0.006+1.3)
    return cv2.remap(L['04_kimono'],Xg-dx,Yg-dy,cv2.INTER_LINEAR,borderValue=0)
def frame_params(f,N=120,scale=1.0):
    p=2*np.pi*f/N
    br=np.sin(p)
    torso=M(pp['05_torso']['pivot'],0,1,1+0.004*br)
    sk=torso@M(pp['12_skull']['pivot'],0.6*np.sin(p-0.7)*scale)
    T={'05_torso':torso,'06_scabbards':torso,'12_skull':sk,'13_hat':sk,
       '07_sash_strip_L':torso@M(pp['07_sash_strip_L']['pivot'],2.5*scale*np.sin(2*p-0.9)),
       '08_sash_rope':torso@M(pp['08_sash_rope']['pivot'],2.0*scale*np.sin(2*p-1.3)),
       '09_sash_strip_R':torso@M(pp['09_sash_strip_R']['pivot'],2.5*scale*np.sin(2*p-1.7)),
       '10_arm_R':torso@M(pp['10_arm_R']['pivot'],0.6*scale*np.sin(p+0.5)),
       '11_arm_L_katana':torso@M(pp['11_arm_L_katana']['pivot'],0.6*scale*np.sin(p+2.0))}
    for n,amp,ph in (('14_hat_talisman_L',6,0.3),('15_hat_chain_L_outer',4,0.9),('16_hat_chain_L_inner',4,1.6),('17_hat_talisman_R',6,2.2),('18_hat_chain_R',4,2.8)):
        T[n]=sk@M(pp[n]['pivot'],amp*scale*np.sin(2*p-ph))
    return T,2*p
def render(base,f,scale=1.0,A=3.5,skip=()):
    T,ph=frame_params(f,scale=scale)
    K=wave_kimono(ph,A)
    c=base.copy()
    for n in order:
        if n in skip: continue
        Lp=K if n=='04_kimono' else L[n]
        if n in T: Lp=cv2.warpAffine(Lp,T[n][:2],(w,h),flags=cv2.INTER_LINEAR,borderValue=0)
        c=over(c,Lp)
    return c
# hole test on magenta at exaggerated amplitude (x1.5 swing, 6px wave)
mag=np.full_like(orig,(255,0,255))
dbg=[render(mag,f,1.5,6.0,skip=('01_shadow',)) for f in (15,45)]
cv2.imwrite(D+'debug_sway_extreme_x1.5.png',cv2.resize(np.hstack(dbg).astype(np.uint8),None,fx=0.4,fy=0.4,interpolation=cv2.INTER_AREA))
for i,d in enumerate(dbg): cv2.imwrite(D+f'work/dbg{i}.png',d.astype(np.uint8))
# video on dark game-like background
bgc=np.full_like(orig,(24,18,22)); 
g=np.exp(-(((Xg-512)/700)**2+((Yg-900)/900)**2))[...,None]; bgc=bgc+g*np.array((18,14,40),np.float32)
N=120; fr=[]
OW,OH=682,1024
for f in range(N):
    c=render(bgc,f)
    fr.append(cv2.resize(np.clip(c,0,255).astype(np.uint8),(OW,OH),interpolation=cv2.INTER_AREA))
pr=subprocess.Popen(f'ffmpeg -y -loglevel error -f rawvideo -pix_fmt bgr24 -s {OW}x{OH} -r 30 -i - -c:v libx264 -pix_fmt yuv420p -crf 18 {D}skeleton_samurai_sway_test.mp4'.split(),stdin=subprocess.PIPE)
for x in fr: pr.stdin.write(x.tobytes())
pr.stdin.close(); pr.wait()
for i in (0,15,30,45): cv2.imwrite(D+f'work/frame_{i:03d}.png',fr[i])
