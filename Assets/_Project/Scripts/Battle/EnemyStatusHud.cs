using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public sealed class EnemyStatusHud : MonoBehaviour
    {
        static readonly Color Ivory = new(.95f,.91f,.81f);
        readonly List<BreakShieldGraphic> _shields = new();
        readonly List<float> _bursts = new();
        TMP_Text _hp, _break;
        RectTransform _recovery;
        EnemyHpStrip _bar;
        float _ratio=1, _trailRatio=1, _trailDelay;

        public static EnemyStatusHud Create(RectTransform parent, string title)
        {
            var root=BattleHudKit.Place(parent,"EnemyStatusHud",new Vector2(.60f,1),new Vector2(.5f,1),new Vector2(0,-16),new Vector2(860,178));
            var hud=root.gameObject.AddComponent<EnemyStatusHud>();
            hud.Build(root,title);
            return hud;
        }
        TMP_Text Text(RectTransform parent,string name,string value,float size,Vector2 pos,Vector2 box,bool right=false,bool numeric=false)
        {
            var rt=BattleHudKit.Place(parent,name,new Vector2(0,1),new Vector2(0,1),pos,box);
            var text=BattleHudKit.Text(rt,name+"Text",value,numeric?BattleHudKit.Condensed:BattleHudKit.Mincho,size,Ivory,
                right?TextAlignmentOptions.BottomRight:TextAlignmentOptions.MidlineLeft,Vector2.zero,Vector2.one);
            text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.NoWrap;
            if(numeric) text.fontStyle=FontStyles.Italic;
            BattleHudKit.Outline(text,.06f,Color.black,.65f);
            return text;
        }
        void Build(RectTransform root,string title)
        {
            var name=Text(root,"EnemyName",title,34,new Vector2(28,0),new Vector2(430,62));
            var slash=BattleHudKit.Place(root,"NameAccent",new Vector2(0,1),new Vector2(0,1),new Vector2(7,-12),new Vector2(6,62));
            BattleHudKit.AddImage(slash,BattleUISprites.Solid,new Color(.8f,.035f,.08f)); slash.localRotation=Quaternion.Euler(0,0,-26);
            var line=BattleHudKit.Place(root,"NameUnderline",new Vector2(0,1),new Vector2(0,1),new Vector2(28,-61),new Vector2(Mathf.Min(410,name.GetPreferredValues(title).x),1));
            BattleHudKit.AddImage(line,BattleUISprites.Solid,Ivory*.8f);
            _hp=Text(root,"HpValue","",68,new Vector2(445,-2),new Vector2(415,66),true,true);
            Text(root,"HpLabel","HP",24,new Vector2(6,-72),new Vector2(56,36),false,true);
            var track=BattleHudKit.Place(root,"HpTrack",new Vector2(0,1),new Vector2(0,1),new Vector2(68,-76),new Vector2(792,30));
            _bar=track.gameObject.AddComponent<EnemyHpStrip>();
            _bar.raycastTarget=false;
            _break=Text(root,"BreakLabel","BREAK",26,new Vector2(6,-120),new Vector2(125,45),false,true);
            _recovery=BattleHudKit.Place(root,"Recovery",new Vector2(0,1),new Vector2(0,1),new Vector2(140,-171),new Vector2(700,3));
            BattleHudKit.AddImage(_recovery,BattleUISprites.Solid,new Color(.86f,.34f,.20f));
            _recovery.gameObject.SetActive(false);
        }
        public void Configure(EnemyBreakState state)
        {
            foreach(var s in _shields) Destroy(s.gameObject);
            _shields.Clear(); _bursts.Clear();
            var n=state.Requirements.Count;
            _break.gameObject.SetActive(n>0);
            var spacing=Mathf.Min(66,700f/Mathf.Max(1,n));
            for(var i=0;i<n;i++)
            {
                var rt=BattleHudKit.Place(transform,"Shield"+i,new Vector2(0,1),new Vector2(.5f,1),new Vector2(156+i*spacing,-121),new Vector2(Mathf.Min(44,spacing-5),47));
                var shield=rt.gameObject.AddComponent<BreakShieldGraphic>(); shield.raycastTarget=false;
                shield.Face=FaceColor(state.Requirements[i]); _shields.Add(shield); _bursts.Add(-1);
            }
        }
        public static Color FaceColor(ElementColor color) => color switch {
            ElementColor.Red=>new Color(.74f,.035f,.06f),ElementColor.Blue=>new Color(.08f,.30f,.58f),
            ElementColor.Yellow=>new Color(.77f,.48f,.065f),ElementColor.Green=>new Color(.09f,.48f,.24f),
            _=>new Color(.43f,.12f,.55f)};
        public void SetHP(int current,int max)
        {
            var ratio=Mathf.Clamp01(current/(float)Mathf.Max(1,max));
            if(ratio<_ratio) _trailDelay=.25f;
            if(ratio>_ratio) _trailRatio=ratio;
            _ratio=ratio; _bar.Ratio=ratio; _bar.SetVerticesDirty();
            _hp.text=$"{Mathf.Max(0,current)}<size=48%><color=#CDC7B9> / {Mathf.Max(1,max)}</color></size>";
        }
        public void Shatter(int index)
        {
            if(index<0 || index>=_shields.Count) return;
            _bursts[index]=0; _shields[index].Burst=0; _shields[index].Broken=true; _shields[index].SetVerticesDirty();
        }
        // Controller supplies battle delta: pause/countdown/jackpot freeze visual effects too.
        public void Tick(EnemyBreakState state,float dt)
        {
            if(_trailDelay>0) _trailDelay-=dt;
            else _trailRatio=Mathf.MoveTowards(_trailRatio,_ratio,dt*.8f);
            _bar.TrailRatio=_trailRatio; _bar.SetVerticesDirty();
            for(var i=0;i<_shields.Count;i++)
            {
                var shield=_shields[i]; var changed=shield.Broken!=state.Consumed[i];
                shield.Broken=state.Consumed[i];
                if (!shield.Broken && _bursts[i] >= 0) { _bursts[i]=-1; shield.Burst=-1; changed=true; }
                if(_bursts[i]>=0)
                {
                    _bursts[i]+=dt;
                    shield.Burst=_bursts[i]/.48f;
                    if(shield.Burst>=1) { _bursts[i]=-1; shield.Burst=-1; }
                    changed=true;
                }
                if(changed) shield.SetVerticesDirty();
            }
            _break.color=state.IsBroken?new Color(1,.25f,.13f):Ivory;
            _recovery.gameObject.SetActive(state.IsBroken);
            _recovery.sizeDelta=new Vector2(700*state.Remaining/state.Duration,3);
        }
    }

    /// <summary>斜めの両端・細い縁・滑らかな陰影。文字ラベルや区切り線には枠を付けない。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class EnemyHpStrip : MaskableGraphic
    {
        public EnemyHpStrip()
        {
            useLegacyMeshGeneration = false;
        }

        public float Ratio=1, TrailRatio=1;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r=rectTransform.rect; var cut=Mathf.Min(12,r.width*.1f);
            var a=new Vector2(r.xMin,r.yMin); var b=new Vector2(r.xMin+cut,r.yMax);
            var c=new Vector2(r.xMax,r.yMax); var d=new Vector2(r.xMax-cut,r.yMin);
            Quad(vh,a,b,c,d,new Color(.025f,.016f,.02f,.94f),new Color(.025f,.016f,.02f,.94f));
            Fill(vh,r,cut,TrailRatio,new Color(.48f,.15f,.12f),new Color(.65f,.27f,.19f));
            Fill(vh,r,cut,Ratio,new Color(.32f,.008f,.021f),new Color(.96f,.035f,.07f));
            var outline=new Color(.89f,.86f,.77f);
            Edge(vh,a,b,outline); Edge(vh,b,c,outline); Edge(vh,c,d,outline); Edge(vh,d,a,outline);
        }
        static void Fill(VertexHelper vh,Rect r,float cut,float ratio,Color low,Color high)
        {
            ratio=Mathf.Clamp01(ratio);
            if(ratio<=0) return;
            var w=(r.width-cut-4)*ratio;
            var x=r.xMin+2; var y=r.yMin+2; var top=r.yMax-2;
            Quad(vh,new Vector2(x,y),new Vector2(x+cut,top),new Vector2(x+cut+w,top),new Vector2(x+w,y),low,high);
        }
        static void Edge(VertexHelper vh,Vector2 a,Vector2 b,Color col)
        {
            var n=new Vector2(-(b-a).y,(b-a).x).normalized*.65f;
            Quad(vh,a+n,a-n,b-n,b+n,col,col);
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color low,Color high)
        {
            var n=vh.currentVertCount;
            vh.AddVert(a,low,Vector2.zero); vh.AddVert(b,high,Vector2.zero);
            vh.AddVert(c,high,Vector2.zero); vh.AddVert(d,low,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
