using System;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle.Visual
{
  /// <summary>
  /// StrikeFX_v1 の連番スプライト／ランプ参照。Resources/Battle/EnemyStrikeFxCatalog。
  /// </summary>
  [CreateAssetMenu(fileName = "EnemyStrikeFxCatalog", menuName = "TypingRoguelike/Enemy Strike FX Catalog")]
  public sealed class EnemyStrikeFxCatalog : ScriptableObject
  {
    const string ResourcePath = "Battle/EnemyStrikeFxCatalog";
    static EnemyStrikeFxCatalog _cached;

    public StrikeClip slash;
    public StrikeClip thrust;
    public StrikeClip bite;
    public StrikeClip claw;
    public StrikeClip magic;

    [Tooltip("パレット順: Neutral, BloodRed, PoisonGreen, CursePurple, IceBlue, HolyGold")]
    public Material[] rampMaterials = Array.Empty<Material>();

    public static EnemyStrikeFxCatalog Instance
    {
      get
      {
        if (_cached != null)
          return _cached;
        _cached = Resources.Load<EnemyStrikeFxCatalog>(ResourcePath);
        return _cached;
      }
    }

    public StrikeClip Get(EnemyStrikeEffectKind kind) => kind switch
    {
      EnemyStrikeEffectKind.Slash => slash,
      EnemyStrikeEffectKind.Thrust => thrust,
      EnemyStrikeEffectKind.Bite => bite,
      EnemyStrikeEffectKind.Claw => claw,
      EnemyStrikeEffectKind.Magic => magic,
      _ => null
    };

    public Material GetRampMaterial(EnemyEffectPalette palette)
    {
      var i = (int)palette;
      if (rampMaterials == null || i < 0 || i >= rampMaterials.Length)
        return null;
      return rampMaterials[i];
    }

    [Serializable]
    public sealed class StrikeClip
    {
      public EnemyStrikeEffectKind kind;
      public Sprite[] maskFrames = Array.Empty<Sprite>();
      [Tooltip("パレット別の色付きフレーム。シェーダー失敗時のフォールバック")]
      public PaletteFrames[] coloredByPalette = Array.Empty<PaletteFrames>();
      public float fps = 30f;
      public int impactFrame = 1;
      public float displaySizeEh = 1.4f;
      /// <summary>Unity UI 空間（右＋X、上＋Y）。HOME=足元基準からの Eh 比。</summary>
      public Vector2 centerOffsetEh = new(0f, 0.44f);

      public float StartOffsetSec => fps > 0.01f ? -impactFrame / fps : 0f;
      public float DurationSec => fps > 0.01f ? FrameCount / fps : 0.4f;
      public int FrameCount =>
        maskFrames != null && maskFrames.Length > 0
          ? maskFrames.Length
          : ColoredFrameCount();

      int ColoredFrameCount()
      {
        if (coloredByPalette == null || coloredByPalette.Length == 0)
          return 0;
        for (var i = 0; i < coloredByPalette.Length; i++)
        {
          var f = coloredByPalette[i]?.frames;
          if (f != null && f.Length > 0)
            return f.Length;
        }
        return 0;
      }

      public Sprite[] ResolveFrames(EnemyEffectPalette palette, bool preferMask)
      {
        if (preferMask && maskFrames != null && maskFrames.Length > 0)
          return maskFrames;
        var colored = GetColored(palette);
        if (colored != null && colored.Length > 0)
          return colored;
        return maskFrames;
      }

      public Sprite[] GetColored(EnemyEffectPalette palette)
      {
        var i = (int)palette;
        if (coloredByPalette == null || i < 0 || i >= coloredByPalette.Length)
          return null;
        return coloredByPalette[i]?.frames;
      }
    }

    [Serializable]
    public sealed class PaletteFrames
    {
      public EnemyEffectPalette palette;
      public Sprite[] frames = Array.Empty<Sprite>();
    }
  }
}
