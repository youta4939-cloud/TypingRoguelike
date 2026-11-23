using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>空／次／点灯。炎・熾火・器の色を1スロット分持つ。</summary>
    public sealed class StockSlot : MonoBehaviour
    {
        public Image Cold;
        public Image Mouth;
        public Image Halo;
        public Image Smoke;
        public Image Flame;
        public Image ShaderHalo;
        public RawImage ShaderFlame;
        public FlameSheetPlayer Sheet;
        public FlameShaderDriver Shader;
        public Image Tint;
        public Vector2 Ball;
        public Vector2 MouthPos;
        public Vector2 TintPos;
        public Vector2 BallAbs;
        public float Offset;
        public GemColor Gem;
        public bool Lit;
        public bool Next;
        public float Alpha = 1f;
        public float Scale = 1f;
        public float Boost = 1f;
        public Vector2 Slide;
        public bool ShaderReady;
        public GemColor ShaderBound;

        public void Tick(float time, FlameRoute route, Sprite[] frames)
        {
            bool showFlame = Lit && Alpha > 0.01f;
            if (Cold != null)
            {
                Cold.enabled = !Lit && !Next;
                Cold.rectTransform.anchoredPosition = MouthPos + Slide;
                var cold = Color.white;
                cold.a = 0.5f;
                Cold.color = cold;
            }

            float pulse = 0.75f + 0.25f * Mathf.Sin(3.3f * time);
            bool showNext = !Lit && Next;
            if (Mouth != null)
            {
                Mouth.enabled = showNext;
                Mouth.rectTransform.anchoredPosition = MouthPos + Slide;
                var color = Color.white;
                color.a = 0.85f * pulse;
                Mouth.color = color;
            }
            if (Halo != null)
            {
                Halo.enabled = showNext;
                Halo.rectTransform.anchoredPosition = MouthPos + new Vector2(0f, 10f) + Slide;
                var color = Color.white;
                color.a = 0.35f * pulse;
                Halo.color = color;
            }
            if (Smoke != null)
            {
                Smoke.enabled = showNext;
                Smoke.rectTransform.anchoredPosition = MouthPos + new Vector2(3f * Mathf.Sin(-1.6f * time), 4f) + Slide;
                Smoke.color = Color.white;
            }

            bool shader = route == FlameRoute.Shader && FlameShaderDriver.Supported;
            if (Sheet != null && frames != null)
                Sheet.Bind(Flame, frames, Offset);
            if (Flame != null)
            {
                Flame.enabled = showFlame && !shader;
                Flame.rectTransform.anchoredPosition = Ball + Slide;
                Flame.rectTransform.localScale = new Vector3(Scale, Scale, 1f);
                var color = Color.white;
                color.a = Alpha;
                Flame.color = color;
                if (Flame.enabled && Sheet != null)
                    Sheet.Tick(time);
            }
            if (ShaderFlame != null)
            {
                ShaderFlame.enabled = showFlame && shader;
                ShaderFlame.rectTransform.anchoredPosition = Ball + Slide;
                ShaderFlame.rectTransform.localScale = new Vector3(Scale, Scale, 1f);
                var color = Color.white;
                color.a = Alpha;
                ShaderFlame.color = color;
            }
            if (ShaderHalo != null)
            {
                ShaderHalo.enabled = showFlame && shader;
                ShaderHalo.rectTransform.anchoredPosition = Ball + Slide;
                ShaderHalo.rectTransform.localScale = new Vector3(Scale, Scale, 1f);
                var color = Color.white;
                color.a = 0.6f * Boost * Alpha;
                ShaderHalo.color = color;
            }
            if (Tint != null)
            {
                Tint.enabled = showFlame;
                Tint.rectTransform.anchoredPosition = TintPos + Slide;
                var color = GemPalette.Mid(Gem);
                color.a = 0.22f * Boost * Alpha;
                Tint.color = color;
            }
        }
    }
}
