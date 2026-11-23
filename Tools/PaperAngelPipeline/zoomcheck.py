exec(open('check.py').read().split("rc=comp(bg)")[0])
def R(piv,deg): return cv2.getRotationMatrix2D(tuple(map(float,piv)),deg,1.0)
rows=[]
for d in (14,-14,0):
    T={'02_wing_L_inner':R(P['02_wing_L_inner']['pivot'],d*0.7),'03_wing_L_outer':R(P['03_wing_L_outer']['pivot'],d),
       '04_wing_R_inner':R(P['04_wing_R_inner']['pivot'],-d*0.7),'05_wing_R_outer':R(P['05_wing_R_outer']['pivot'],-d)}
    c=comp(np.full_like(orig,(60,200,60)),T,skip=('01_shadow',))
    rows.append(np.hstack([c[300:1100,250:740],c[300:1100,700:1190]]))
cv2.imwrite(D+'work/zoom_roots2.png',cv2.resize(np.vstack(rows).astype(np.uint8),None,fx=0.55,fy=0.55,interpolation=cv2.INTER_AREA))
d=np.abs(comp(bg)-orig).mean(2); cv2.imwrite(D+'work/diff.png',cv2.resize(np.clip(d*8,0,255).astype(np.uint8),None,fx=0.4,fy=0.4))
