using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>解像度非依存の盾。5片の実形状が中心から飛び、暗い破片として残る。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BreakShieldGraphic : MaskableGraphic
    {
        public BreakShieldGraphic()
        {
            useLegacyMeshGeneration = false;
        }

        public Color Face = new Color(0.7f, 0.07f, 0.09f);
        public bool Broken;
        public float Burst = -1f;
        // Curved crown, tapered heater shield. Same outline for both states.
        static readonly Vector2[] Shape = {
            new(-.46f,.34f), new(-.25f,.43f), new(0,.47f), new(.25f,.43f), new(.46f,.34f),
            new(.44f,.05f), new(.36f,-.18f), new(.20f,-.36f), new(0,-.50f),
            new(-.20f,-.36f), new(-.36f,-.18f), new(-.44f,.05f)
        };
        static readonly int[] Cuts = { 0, 3, 5, 8, 10, 12 };
        static readonly Vector2[] Directions = { new(-.4f,.8f),new(.8f,.4f),new(.6f,-.5f),new(-.3f,-.8f),new(-.9f,.1f) };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            var broken = Broken || Burst >= 0f;
            var t = Mathf.Clamp01(Burst);
            var flight = Burst >= 0f ? Mathf.Sin(t * Mathf.PI) : 0f;
            var face = broken ? new Color(.10f,.105f,.115f,1) : Face;
            if (Burst >= 0f) face = Color.Lerp(Color.Lerp(Face,Color.white,.65f),face,Mathf.Clamp01(t*4));
            for (var shard=0; shard<5; shard++)
            {
                var shift = broken ? Directions[shard] * (.012f + flight*.16f) : Vector2.zero;
                var center = new Vector2(.025f,-.025f);
                var first=Cuts[shard]; var end=Cuts[shard+1];
                var hole = broken ? .15f : 0f;
                for (var k=first;k<end;k++)
                {
                    var a=Shape[k%Shape.Length]; var b=Shape[(k+1)%Shape.Length];
                    // Pull seam edges inward, leaving four/five broad fracture channels.
                    if(broken && k==first) a=Vector2.Lerp(a,b,.065f);
                    if(broken && k==end-1) b=Vector2.Lerp(b,a,.065f);
                    var ai=Vector2.Lerp(center,a,hole); var bi=Vector2.Lerp(center,b,hole);
                    var rim=broken ? new Color(.22f,.23f,.25f) : new Color(.40f,.40f,.38f);
                    var edgeLight=broken ? .035f : .25f;
                    var light=(a.y+b.y)*.5f>0 ? edgeLight : -edgeLight*.4f;
                    rim += new Color(light,light,light,0);
                    Quad(vh,r,a+shift,b+shift,b*.91f+shift,a*.91f+shift,rim,rim*.66f);
                    var shade=1f + (a.y+b.y)*.21f - (a.x+b.x)*.13f;
                    Quad(vh,r,ai+shift,bi+shift,b*.91f+shift,a*.91f+shift,face*.70f,face*shade);
                }
                if(Burst>=0 && t<.9f)
                {
                    // Separate small falling chips; vertex alpha fades naturally.
                    var p=Directions[shard]*(.18f+t*.7f)+new Vector2(0,-t*t*.7f);
                    var chip=Color.Lerp(Face,new Color(.2f,.2f,.21f),t); chip.a=1-t;
                    Triangle(vh,r,p+new Vector2(-.026f,0),p+new Vector2(.02f,.028f),p+new Vector2(.012f,-.025f),chip);
                }
            }
        }
        static Vector2 Pixel(Rect r,Vector2 p) => r.center+new Vector2(p.x*r.width,p.y*r.height);
        static void Quad(VertexHelper vh,Rect r,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color inner,Color outer)
        {
            // Color multiplication affects alpha too; all persistent surfaces stay opaque.
            inner.a=outer.a=1;
            var n=vh.currentVertCount;
            vh.AddVert(Pixel(r,a),inner,Vector2.zero); vh.AddVert(Pixel(r,b),inner,Vector2.zero);
            vh.AddVert(Pixel(r,c),outer,Vector2.zero); vh.AddVert(Pixel(r,d),outer,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
        static void Triangle(VertexHelper vh,Rect r,Vector2 a,Vector2 b,Vector2 c,Color col)
        {
            var n=vh.currentVertCount;
            vh.AddVert(Pixel(r,a),col,Vector2.zero); vh.AddVert(Pixel(r,b),col,Vector2.zero); vh.AddVert(Pixel(r,c),col,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);
        }
    }
}
