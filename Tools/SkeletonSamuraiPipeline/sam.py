from rembg import new_session, remove
from PIL import Image
import numpy as np,cv2,sys,json
im=Image.open('src.png').convert('RGB')
s=new_session('sam')
J=json.load(open('/workspace/art/enemies/skeleton_samurai_parts/sam_prompts_all.json'))
for name,(pos,neg) in J.items():
    if len(sys.argv)>1 and name not in sys.argv[1:]: continue
    pr=[{"type":"point","data":list(p),"label":1} for p in pos]+[{"type":"point","data":list(p),"label":0} for p in neg]
    remove(im,session=s,sam_prompt=pr,only_mask=True).save(f'sam_{name}.png'); print(name)
