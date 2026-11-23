import numpy as np,cv2,json
D='/workspace/art/enemies/skeleton_samurai_parts/'
P=json.load(open(D+'parts_pivots.json')); order=list(P['parts'].keys())
Lr={n:cv2.imread(D+n+'.png',-1).astype(np.float32) for n in order}
orig=cv2.imread(D+'work/src.png').astype(np.float32); h,w=orig.shape[:2]
def comp(base,T={},skip=(),K=None):
    c=base.copy()
    for n in order:
        if n in skip: continue
        Lp=Lr[n] if not (K is not None and n=='04_kimono') else K
        if n in T: Lp=cv2.warpAffine(Lp,T[n],(w,h),flags=cv2.INTER_LINEAR,borderValue=0)
        al=Lp[...,3:]/255; c=c*(1-al)+Lp[...,:3]*al
    return c
white=np.full_like(orig,254)
rc=comp(white)
d=np.abs(rc-orig).mean(2); print('mean abs diff vs original',d.mean(),'p99',np.percentile(d,99),'max',d.max())
cv2.imwrite(D+'work/diff.png',np.clip(d*6,0,255).astype(np.uint8))
black=np.full_like(orig,(14,10,12)); red=np.full_like(orig,(22,18,92))
pan=[orig,rc,comp(black,skip=('01_shadow',)),comp(red,skip=('01_shadow',))]
cv2.imwrite(D+'recompose_check.png',cv2.resize(np.hstack(pan).astype(np.uint8),None,fx=0.4,fy=0.4,interpolation=cv2.INTER_AREA))
np.save(D+'work/pan_black.npy',pan[2])
