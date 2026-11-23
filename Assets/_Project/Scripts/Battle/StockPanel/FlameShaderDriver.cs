using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>ルート A。使えない環境では Sheet に戻す。</summary>
    public sealed class FlameShaderDriver : MonoBehaviour
    {
        public const string ShaderName = "StockPanel/UI_FlameSway";

        public static bool Supported { get; private set; }

        Material _material;

        public static void Warmup()
        {
            var shader = Shader.Find(ShaderName);
            Supported = shader != null && shader.isSupported;
        }

        public void Bind(RawImage image, Texture lumA, Texture lumB, Texture ramp, float phase)
        {
            if (image == null)
                return;
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported)
            {
                Supported = false;
                image.enabled = false;
                return;
            }

            if (_material != null)
                Destroy(_material);
            _material = new Material(shader);
            _material.SetTexture("_LumB", lumB);
            _material.SetTexture("_Ramp", ramp);
            _material.SetFloat("_Phase", phase);
            image.texture = lumA;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
            image.material = _material;
            image.color = Color.white;
        }

        void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }
    }
}
