using UnityEngine;

namespace TypingRoguelike.Battle
{
    [CreateAssetMenu(fileName = "JackpotPresentation", menuName = "TypingRoguelike/Jackpot Presentation")]
    public sealed class JackpotPresentationDefinition : ScriptableObject
    {
        [Header("長さ（秒） 合計の初期値は 5.3")]
        public float blackoutSeconds = 0.8f;
        public float cutinSeconds = 1.2f;
        public float attackSeconds = 2f;
        public float totalSeconds = 1.3f;

        [Header("① ブラックアウト")]
        public Sprite lineSprite;
        public Sprite flareSprite;
        public Color lineColor = Color.white;
        public Color flareColor = Color.white;
        public float lineThickness = 64f;
        public float flareMaxSize = 880f;
        [Range(0.2f, 0.85f)] public float blackoutImpactNormalized = 0.6f;
        public AudioClip blackoutClip;
        [Range(0f, 1f)] public float blackoutVolume = 1f;
        public Material additiveMaterial;

        [Header("② 顔カットイン")]
        public Color bandColor = new Color(1f, 0.46f, 0.08f, 0.96f);
        public float bandAngle = -13f;
        public float bandHeight = 168f;
        public Sprite[] faceSprites = new Sprite[3];
        public Color placeholderShadow = new Color(0.16f, 0.1f, 0.08f, 1f);
        public AudioClip cutinClip;
        [Range(0f, 1f)] public float cutinVolume = 1f;

        [Header("③ 単語の刃")]
        public Color bladeColor = new Color(1f, 0.95f, 0.82f, 1f);
        public AudioClip hitClip;
        [Range(0f, 1f)] public float hitVolume = 0.95f;
        public float hitPitchMax = 1.3f;

        [Header("④ 総ダメージ")]
        public Font damageFont;
        public int damageFontSize = 300;
        public Color damageColor = Color.white;
        public AudioClip totalClip;
        [Range(0f, 1f)] public float totalVolume = 1f;
        [Range(0.45f, 0.9f)] public float totalCountPortion = 0.75f;

        [Header("BGM")]
        public float bgmFadeSeconds = 0.9f;

        public float TotalDuration =>
            Mathf.Max(0.05f, blackoutSeconds) +
            Mathf.Max(0.05f, cutinSeconds) +
            Mathf.Max(0.05f, attackSeconds) +
            Mathf.Max(0.05f, totalSeconds);
    }
}
