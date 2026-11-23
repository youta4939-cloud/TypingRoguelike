import numpy as np,cv2,json,sys
D='/workspace/art/enemies/paper_angel_parts/'
P=json.load(open(D+'parts_pivots.json'))['parts']
order=['01_shadow','02_wing_L_inner','03_wing_L_outer','04_wing_R_inner','05_wing_R_outer','06_feet','07_face_inner','08_body','09_seal']
Lr={n:cv2.imread(D+n+'.png',-1).astype(np.float32) for n in order+['10_seal_glow_fx']}
orig=cv2.imread(D+'work/up4.png').astype(np.float32); bg=np.load(D+'work/bg.npy')
h,w=orig.shape[:2]
def comp(base,T={},skip=()):
    c=base.copy()
    for n in order:
        if n in skip: continue
        L=Lr[n]
        if n in T: L=cv2.warpAffine(L,T[n],(c.shape[1],c.shape[0]),flags=cv2.INTER_LINEAR,borderValue=0)
        a=L[...,3:]/255; c=c*(1-a)+L[...,:3]*a
    return c
rc=comp(bg)
diff=np.abs(rc-orig).mean(2)
print('mean abs diff',diff.mean(),'p99',np.percentile(diff,99))
dark=np.full_like(orig,(30,22,24)); white=np.full_like(orig,235)
cv2.imwrite(D+'recompose_check.png',cv2.resize(np.hstack([orig,rc,comp(dark,skip=('01_shadow',)),comp(white,skip=('01_shadow',))]).astype(np.uint8),None,fx=0.33,fy=0.33,interpolation=cv2.INTER_AREA))
def R(piv,deg): return cv2.getRotationMatrix2D(tuple(map(float,piv)),deg,1.0)
out=[]
for d in (14,-14):
    T={'02_wing_L_inner':R(P['02_wing_L_inner']['pivot'],d*0.7),'03_wing_L_outer':R(P['03_wing_L_outer']['pivot'],d),
       '04_wing_R_inner':R(P['04_wing_R_inner']['pivot'],-d*0.7),'05_wing_R_outer':R(P['05_wing_R_outer']['pivot'],-d)}
    out.append(comp(np.full_like(orig,(60,200,60)),T,skip=('01_shadow',)))
cv2.imwrite(D+'debug_wings_extreme_pm14deg.png',cv2.resize(np.hstack(out).astype(np.uint8),None,fx=0.4,fy=0.4,interpolation=cv2.INTER_AREA))
