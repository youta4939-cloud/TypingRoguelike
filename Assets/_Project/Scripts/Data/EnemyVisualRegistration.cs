using System;
using UnityEngine;

namespace TypingRoguelike.Data
{
    /// <summary>敵ごとの2枚絵＋演出プリセット登録。ランタイム解釈は後付け。</summary>
    [Serializable]
    public struct EnemyVisualRegistration
    {
        public EnemyGraphicMode graphicMode;
        [Tooltip("待機・立ち絵（未設定時は EnemyDefinition.battleIllustration）")]
        public Sprite idleIllustration;
        public Sprite attackIllustration;
        [Range(0.35f, 2.5f)] public float displayScale;
        [Tooltip("テクスチャ正規化座標。足元位置。クリックで設定")]
        public Vector2 feetPivotIdle;
        [Tooltip("攻撃絵の足元位置")]
        public Vector2 feetPivotAttack;
        [Tooltip("立ち絵の表示位置補正（Eh比。右が＋X、上が＋Y）")]
        public Vector2 poseOffsetIdle;
        [Tooltip("攻撃絵の表示位置補正（Eh比。立ち絵と初期位置が違うときに使う）")]
        public Vector2 poseOffsetAttack;
        [Tooltip("斬撃エフェクト中心の追加オフセット（Eh比。クリップ既定位置からの相対。右＋X、上＋Y）")]
        public Vector2 strikeOffsetEh;

        public EnemyMotionPreset motionPreset;
        public EnemyStrikeEffectKind strikeEffect;
        public EnemyEffectPalette effectPalette;
        public EnemyEffectIntensity effectIntensity;
        public EnemyDefeatPreset defeatPreset;
        public Color hitFlashColor;

        public float windupSecondsOverride;
        public float activeSecondsOverride;
        public float recoverySecondsOverride;
        public float strikeScaleOverride;

        public static EnemyVisualRegistration Default => new()
        {
            graphicMode = EnemyGraphicMode.SpritePair,
            displayScale = 1f,
            feetPivotIdle = new Vector2(0.5f, 0f),
            feetPivotAttack = new Vector2(0.5f, 0f),
            // 待機(Idle)はほぼ動かない。2枚絵の既定は確定済みの飛びかかり。
            motionPreset = EnemyMotionPreset.Lunge,
            strikeEffect = EnemyStrikeEffectKind.None,
            effectPalette = EnemyEffectPalette.Neutral,
            effectIntensity = EnemyEffectIntensity.Medium,
            defeatPreset = EnemyDefeatPreset.FadeOut,
            hitFlashColor = Color.white
        };

        public Sprite ResolveIdle(EnemyDefinition owner) =>
            idleIllustration != null ? idleIllustration : owner != null ? owner.battleIllustration : null;
    }
}