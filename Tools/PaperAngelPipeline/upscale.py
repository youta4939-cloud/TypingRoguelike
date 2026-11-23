import numpy as np,cv2,onnxruntime as ort
SRC='/cursor/stores/store-ca231ee5-5a96-4485-a07e-1658642ba9f2/grok-bot/agents/4c9bafee-3694-409b-9f7b-8e2edfcae237/attachments/01M5GXVA0NS3EN52KSVKVN309H.png'
im=cv2.imread(SRC,cv2.IMREAD_UNCHANGED)[...,:3]
cv2.imwrite('work/src.png',im)
s=ort.InferenceSession('/workspace/models/realesrgan_x4.onnx',providers=['CPUExecutionProvider'])
T=64;O=12;st=T-2*O
P=O+T
pad=cv2.copyMakeBorder(im,P,P,P,P,cv2.BORDER_REFLECT)
h,w=im.shape[:2];out=np.zeros((h*4,w*4,3),np.float32)
rgb=pad[...,::-1].astype(np.float32)/255
for y in range(0,h,st):
  for x in range(0,w,st):
    t=rgb[P+y-O:P+y-O+T,P+x-O:P+x-O+T].transpose(2,0,1)[None]
    r=s.run(None,{'input.1':np.ascontiguousarray(t)})[0][0].transpose(1,2,0)
    c=r[O*4:(O+st)*4,O*4:(O+st)*4]
    hh=min(st,h-y)*4;ww=min(st,w-x)*4
    out[y*4:y*4+hh,x*4:x*4+ww]=c[:hh,:ww]
out=np.clip(out[...,::-1]*255,0,255).astype(np.uint8)
cv2.imwrite('work/up4.png',out);print(out.shape)
