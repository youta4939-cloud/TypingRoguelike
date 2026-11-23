using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
    public struct EnemySpritePairPose
    {
        public Vector2 OffsetEh;
        public float ScaleX;
        public float ScaleY;
        public float RotationDeg;
        /// <summary>足元支点の水平シアー（正＝上端が右）。描画は近似で Rotation に折り込み可。</summary>
        public float SkewX;
        public bool UseAttackSprite;
        public float Alpha;
        public Color Tint;
        public bool StrikeThisFrame;
        public bool ShakeThisFrame;
        /// <summary>Swing ヒットストップ中など、ストライク再生をポーズする。</summary>
        public bool FreezeStrikeFx;

        public static EnemySpritePairPose Idle => new()
        {
            ScaleX = 1f,
            ScaleY = 1f,
            RotationDeg = 0f,
            SkewX = 0f,
            UseAttackSprite = false,
            Alpha = 1f,
            Tint = Color.white
        };
    }
}