import numpy as np,cv2,json,subprocess
from PIL import Image,ImageDraw,ImageFont
D='/workspace/art/enemies/paper_angel_parts/'
P=json.load(open(D+'parts_pivots.json')); pp=P['parts']
names=['01_shadow','02_wing_L_inner','03_wing_L_outer','04_wing_R_inner','05_wing_R_outer','06_feet','07_face_inner','08_body','09_seal','10_seal_glow_fx']
L={n:cv2.imread(D+n+'.png',-1).astype(np.float32) for n in names}
bg=np.load(D+'work/bg.npy'); h,w=bg.shape[:2]
FN='/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'
order=names[:-1]
def over(c,Lp): a=Lp[...,3:]/255; return c*(1-a)+Lp[...,:3]*a
# ---------------- parts map
base=bg.copy()
for n in order: base=over(base,L[n])
gray=base.mean(2,keepdims=True).repeat(3,2)*0.55+60
cols={'01_shadow':(90,90,90),'02_wing_L_inner':(80,200,80),'03_wing_L_outer':(60,240,200),'04_wing_R_inner':(230,140,60),'05_wing_R_outer':(250,210,80),
'06_feet':(230,120,230),'07_face_inner':(120,40,200),'08_body':(110,185,240),'09_seal':(40,40,230)}
vis={}
acc=np.zeros((h,w),np.float32)
for n in reversed(order):           # visible portion of each part
    a=L[n][...,3]/255; v=a*(1-acc); vis[n]=v; acc=acc+v
ov=gray.copy()
for n in order:
    a=(vis[n]*0.6)[...,None]; ov=ov*(1-a)+np.array(cols[n],np.float32)*a
S=0.5
ov=cv2.resize(ov.astype(np.uint8),None,fx=S,fy=S,interpolation=cv2.INTER_AREA)
ext=np.full((ov.shape[0],ov.shape[1]+340,3),40,np.uint8); ext[:,:ov.shape[1]]=ov
im=Image.fromarray(cv2.cvtColor(ext,cv2.COLOR_BGR2RGB)); dr=ImageDraw.Draw(im)
F=ImageFont.truetype(FN,22); f2=ImageFont.truetype(FN,16)
lab=[('08_body','胴体（頭巾・ローブ）'),('07_face_inner','顔（頭巾の奥）'),('09_seal','胸の印'),('03_wing_L_outer','左羽・外側（画面左）'),('02_wing_L_inner','左羽・内側（画面左）'),
     ('05_wing_R_outer','右羽・外側（画面右）'),('04_wing_R_inner','右羽・内側（画面右）'),('06_feet','足'),('01_shadow','影')]
x0=ov.shape[1]+20
for i,(n,t) in enumerate(lab):
    c=cols[n][::-1]; y=40+i*44; dr.rectangle([x0,y,x0+26,y+26],fill=c); dr.text((x0+36,y-2),t,font=F,fill=(255,255,255))
def pt(p,label,c=(255,60,60),left=False):
    x,y=p[0]*S,p[1]*S; dr.ellipse([x-7,y-7,x+7,y+7],outline=(255,255,255),width=2,fill=c)
    tw=dr.textlength(label,font=f2); dr.text(((x-9-tw) if left else (x+9),y-10),label,font=f2,fill=(255,255,255),stroke_width=2,stroke_fill=(0,0,0))
# wing split rays
for Pk,angd in (('02_wing_L_inner',215),('04_wing_R_inner',-35)):
    q=pp[Pk]['pivot']; t=np.radians(angd); dr.line([(q[0]*S,q[1]*S),((q[0]+700*np.cos(t))*S,(q[1]+700*np.sin(t))*S)],fill=(255,255,80),width=2)
pt(pp['02_wing_L_inner']['pivot'],'左羽の軸（肩）',left=True); pt(pp['04_wing_R_inner']['pivot'],'右羽の軸（肩）')
pt(pp['07_face_inner']['pivot'],'顔の軸',(120,40,200)); pt(pp['09_seal']['pivot'],'印の中心',(255,200,0)); pt(pp['08_body']['pivot'],'胴体の軸（足元）',(255,255,255))
y=40+len(lab)*44+20
for t in ['● 赤: 羽の回転軸（頭巾の裏に隠れる）','— 黄: 羽の外側/内側の境目','● 紫: 顔のうなずき軸','● 黄: 印の脈動の中心','※ 左右は画面上の左右','※ 正面向きなので反転不要']:
    dr.text((x0,y),t,font=f2,fill=(220,220,220)); y+=28
im.save(D+'parts_map.png')
# ---------------- exploded view
def crop(n):
    a=L[n][...,3]; ys,xs=np.where(a>2); return L[n][ys.min():ys.max()+1,xs.min():xs.max()+1]
tiles=[]
for n in names:
    c=crop(n); s=0.30; c=cv2.resize(c,None,fx=s,fy=s,interpolation=cv2.INTER_AREA)
    hh,ww=c.shape[:2]; yy,xx=np.mgrid[0:hh,0:ww]; chk=np.where(((yy//10+xx//10)%2)[...,None]==0,200,160).astype(np.float32).repeat(3,2)
    if n=='01_shadow' or n=='10_seal_glow_fx': chk=chk*0.9
    t=over(chk,c); tiles.append((n,t))
Wt=1500; x=10; y=30; rowh=0; canvas=np.full((2200,Wt,3),58,np.float32); pos=[]
for n,t in tiles:
    hh,ww=t.shape[:2]
    if x+ww>Wt-10: x=10; y+=rowh+40; rowh=0
    canvas[y:y+hh,x:x+ww]=t; pos.append((n,x,y)); x+=ww+20; rowh=max(rowh,hh)
canvas=canvas[:y+rowh+20]
im=Image.fromarray(cv2.cvtColor(canvas.astype(np.uint8),cv2.COLOR_BGR2RGB)); dr=ImageDraw.Draw(im)
for n,x,y in pos: dr.text((x,y-24),n,font=f2,fill=(255,255,255))
im.save(D+'parts_exploded.png')
import os,sys
if os.environ.get('NOANIM'): sys.exit(0)
# ---------------- idle animation
def M(piv,ang=0,sx=1,sy=1,tx=0,ty=0):
    a=np.deg2rad(ang); c,s=np.cos(a),np.sin(a); px,py=piv
    R=np.array([[c*sx,s*sy],[-s*sx,c*sy]])   # positive ang = CCW on screen (same as cv2)
    t=np.array([px,py])-R@np.array([px,py])+[tx,ty]
    return np.vstack([np.hstack([R,t[:,None]]),[0,0,1]])
PAD=90; W2,H2=w+2*PAD,h+2*PAD
bgp=cv2.copyMakeBorder(bg,PAD,PAD,PAD,PAD,cv2.BORDER_REPLICATE)
Tp=np.array([[1,0,PAD],[0,1,PAD],[0,0,1]],float)
N=120; fr=[]
for f in range(N):
    p=2*np.pi*f/N
    up=np.sin(p)                     # float: 1 cycle / loop
    flap=np.sin(2*p)                 # 2 flaps / loop
    flap_o=np.sin(2*p-0.6)           # outer blades lag (follow-through)
    fl=M((0,0),0,1,1,0,-7*up)
    AO,AI=9,6
    body=fl
    T={'01_shadow':M(pp['01_shadow']['pivot'],0,1-0.06*up,1-0.06*up),
       '06_feet':fl,'08_body':fl,
       '07_face_inner':fl@M(pp['07_face_inner']['pivot'],1.2*np.sin(p-0.8),1,1,0,1.5*np.sin(p-0.8)),
       '02_wing_L_inner':fl@M(pp['02_wing_L_inner']['pivot'],AI*flap),
       '03_wing_L_outer':fl@M(pp['03_wing_L_outer']['pivot'],AO*flap_o),
       '04_wing_R_inner':fl@M(pp['04_wing_R_inner']['pivot'],-AI*flap),
       '05_wing_R_outer':fl@M(pp['05_wing_R_outer']['pivot'],-AO*flap_o)}
    pulse=0.5+0.5*np.sin(p*2-1.2)
    sc=1+0.03*pulse
    T['09_seal']=fl@M(pp['09_seal']['pivot'],0,sc,sc)
    c=bgp.copy()
    for n in order:
        o=cv2.warpAffine(L[n],(Tp@T[n])[:2],(W2,H2),flags=cv2.INTER_LINEAR,borderValue=0)
        a=o[...,3:]/255
        if n=='01_shadow': a=a*(1-0.25*up)
        c=c*(1-a)+o[...,:3]*a
    g=cv2.warpAffine(L['10_seal_glow_fx'],(Tp@T['09_seal'])[:2],(W2,H2),borderValue=0)
    c=c+g[...,:3]*(g[...,3:]/255)*0.55*pulse*0.6
    fr.append(cv2.resize(np.clip(c,0,255).astype(np.uint8),(W2//2*2//2,H2//2*2//2),interpolation=cv2.INTER_AREA))
H,Wf=fr[0].shape[:2]
pr=subprocess.Popen(f'ffmpeg -y -loglevel error -f rawvideo -pix_fmt bgr24 -s {Wf}x{H} -r 30 -i - -c:v libx264 -pix_fmt yuv420p -crf 18 {D}paper_angel_parts_test.mp4'.split(),stdin=subprocess.PIPE)
for x in fr: pr.stdin.write(x.tobytes())
pr.stdin.close(); pr.wait()
for i in (0,15,30,45,60,90): cv2.imwrite(D+f'work/frame_{i:03d}.png',fr[i])
cv2.imwrite(D+'work/animframes.png',cv2.resize(np.hstack([fr[i] for i in (0,15,30,45)]),None,fx=0.5,fy=0.5))
print(Wf,H)
