import cv2,numpy as np,json
u=cv2.imread('up4.png').astype(np.float32)
Lm=cv2.GaussianBlur(u.mean(2),(0,0),1.5)
gx=np.abs(cv2.Sobel(Lm,cv2.CV_32F,1,0,ksize=3))
def interp(pts,ys):
    p=np.array(pts,float); return np.interp(ys,p[:,1],p[:,0])
def snap(pts,y0,y1,win=12):
    ys=np.arange(y0,y1); xs=interp(pts,ys); out=[]
    for y,x in zip(ys,xs):
        x=int(round(x)); seg=gx[y,x-win:x+win+1]; out.append(x-win+int(np.argmax(seg)))
    out=np.array(out,float)
    from scipy.ndimage import median_filter,uniform_filter1d
    out=uniform_filter1d(median_filter(out,31),9)
    return ys,out
L=[(720,361),(650,450),(603,527),(361,1058)]
R=[(720,361),(794,444),(839,522),(1005,833),(1172,1150)]
yl,xl=snap(L,370,1058); yr,xr=snap(R,370,1150)
np.save('edgeL.npy',np.stack([yl,xl])); np.save('edgeR.npy',np.stack([yr,xr]))
o=u.copy().astype(np.uint8)
cv2.polylines(o,[np.stack([xl,yl],1).astype(np.int32)],False,(0,0,255),1)
cv2.polylines(o,[np.stack([xr,yr],1).astype(np.int32)],False,(255,0,0),1)
cv2.imwrite('snap_L.png',cv2.resize(o[400:1100,250:750],None,fx=1.2,fy=1.2))
cv2.imwrite('snap_R.png',cv2.resize(o[400:1180,700:1250],None,fx=1.1,fy=1.1))
