#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TypingRoguelike.Battle.Visual;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
  /// <summary>
  /// Assets/Effect/StrikeFX_v1 からカタログ・マテリアル・インポート設定を組み立てる。
  /// </summary>
  public static class EnemyStrikeFxCatalogBuilder
  {
    const string EffectRoot = "Assets/Effect/StrikeFX_v1";
    const string CatalogPath = "Assets/_Project/Resources/Battle/EnemyStrikeFxCatalog.asset";
    const string MatFolder = "Assets/_Project/Resources/Battle/StrikeFxMaterials";
    const string ShaderName = "Game/UI/StrikeFxRamp";

    static readonly string[] KindFolders = { "slash", "thrust", "bite", "claw", "magic" };
    static readonly EnemyStrikeEffectKind[] Kinds =
    {
      EnemyStrikeEffectKind.Slash,
      EnemyStrikeEffectKind.Thrust,
      EnemyStrikeEffectKind.Bite,
      EnemyStrikeEffectKind.Claw,
      EnemyStrikeEffectKind.Magic
    };

    // json の Y はサンプル座標（下が＋）→ Unity は上＋なので符号反転済み
    static readonly Vector2[] CenterOffsetEh =
    {
      new(0.01f, 0.444f),
      new(-0.01f, 0.534f),
      new(-0.02f, 0.454f),
      new(-0.01f, 0.444f),
      new(-0.01f, 0.464f)
    };

    static readonly float[] DisplaySizeEh = { 1.452f, 1.532f, 1.21f, 1.331f, 1.653f };
    static readonly int[] ImpactFrames = { 1, 1, 3, 1, 7 };
    static readonly int[] FrameCounts = { 13, 12, 14, 13, 17 };

    static readonly string[] PaletteNames =
    {
      "Neutral", "BloodRed", "PoisonGreen", "CursePurple", "IceBlue", "HolyGold"
    };

    [MenuItem("Tools/TypingRoguelike/Build Strike FX Catalog (v1)")]
    public static void Build()
    {
      if (!AssetDatabase.IsValidFolder(EffectRoot))
      {
        EditorUtility.DisplayDialog("Strike FX", $"フォルダがありません:\n{EffectRoot}", "OK");
        return;
      }

      PrototypeDataGenerator.EnsureFolderPublic("Assets/_Project/Resources/Battle");
      PrototypeDataGenerator.EnsureFolderPublic(MatFolder);

      ConfigureAllImports();
      AssetDatabase.Refresh();

      var mats = BuildRampMaterials();
      var catalog = AssetDatabase.LoadAssetAtPath<EnemyStrikeFxCatalog>(CatalogPath);
      if (catalog == null)
      {
        catalog = ScriptableObject.CreateInstance<EnemyStrikeFxCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
      }

      catalog.rampMaterials = mats;
      catalog.slash = BuildClip(0);
      catalog.thrust = BuildClip(1);
      catalog.bite = BuildClip(2);
      catalog.claw = BuildClip(3);
      catalog.magic = BuildClip(4);

      EditorUtility.SetDirty(catalog);
      AssetDatabase.SaveAssets();
      Selection.activeObject = catalog;

      var maskOk = catalog.slash?.maskFrames != null && catalog.slash.maskFrames.Length > 0;
      EditorUtility.DisplayDialog(
        "Strike FX",
        maskOk
          ? $"カタログを更新しました。\n{CatalogPath}\nslash mask コマ数={catalog.slash.maskFrames.Length}"
          : $"カタログは作りましたが mask スプライトが空です。\nPNG の Texture Type を Sprite にして再実行してください。",
        "OK");
    }

    static EnemyStrikeFxCatalog.StrikeClip BuildClip(int index)
    {
      var folder = KindFolders[index];
      var kind = Kinds[index];
      var count = FrameCounts[index];
      var clip = new EnemyStrikeFxCatalog.StrikeClip
      {
        kind = kind,
        fps = 30f,
        impactFrame = ImpactFrames[index],
        displaySizeEh = DisplaySizeEh[index],
        centerOffsetEh = CenterOffsetEh[index],
        maskFrames = LoadNumberedSprites($"{EffectRoot}/{folder}/frames_mask", $"strike_{folder}_mask_", count),
        coloredByPalette = new EnemyStrikeFxCatalog.PaletteFrames[PaletteNames.Length]
      };

      for (var p = 0; p < PaletteNames.Length; p++)
      {
        var pal = (EnemyEffectPalette)p;
        var frames = LoadNumberedSprites(
          $"{EffectRoot}/{folder}/frames_{PaletteNames[p]}",
          $"strike_{folder}_{PaletteNames[p]}_",
          count);
        if (frames.Length == 0)
        {
          var cell = folder == "magic" ? 768 : 512;
          frames = LoadSheetSprites(
            $"{EffectRoot}/{folder}/sheets/strike_{folder}_{PaletteNames[p]}_sheet.png",
            cell,
            count);
        }

        clip.coloredByPalette[p] = new EnemyStrikeFxCatalog.PaletteFrames
        {
          palette = pal,
          frames = frames
        };
      }

      return clip;
    }

    static Sprite[] LoadSheetSprites(string sheetPath, int cellPx, int count)
    {
      if (AssetDatabase.LoadMainAssetAtPath(sheetPath) == null)
        return Array.Empty<Sprite>();

      EnsureSheetSliced(sheetPath, cellPx, count);
      var sprites = new List<Sprite>(count);
      foreach (var a in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
      {
        if (a is Sprite s)
          sprites.Add(s);
      }

      sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
      if (sprites.Count > count)
        sprites.RemoveRange(count, sprites.Count - count);
      return sprites.ToArray();
    }

    static void EnsureSheetSliced(string sheetPath, int cellPx, int count)
    {
      var imp = AssetImporter.GetAtPath(sheetPath) as TextureImporter;
      if (imp == null)
        return;

      var cols = 5;
      var rows = Mathf.CeilToInt(count / (float)cols);
      var metas = new List<SpriteMetaData>(count);
      for (var i = 0; i < count; i++)
      {
        var col = i % cols;
        var rowFromTop = i / cols;
        var y = (rows - 1 - rowFromTop) * cellPx;
        metas.Add(new SpriteMetaData
        {
          name = $"{Path.GetFileNameWithoutExtension(sheetPath)}_{i:00}",
          rect = new Rect(col * cellPx, y, cellPx, cellPx),
          pivot = new Vector2(0.5f, 0.5f),
          alignment = (int)SpriteAlignment.Center
        });
      }

      imp.textureType = TextureImporterType.Sprite;
      imp.spriteImportMode = SpriteImportMode.Multiple;
      imp.mipmapEnabled = false;
      imp.alphaIsTransparency = true;
      imp.sRGBTexture = true;
      imp.spritesheet = metas.ToArray();
      var settings = new TextureImporterSettings();
      imp.ReadTextureSettings(settings);
      settings.spriteMeshType = SpriteMeshType.FullRect;
      imp.SetTextureSettings(settings);
      var platform = imp.GetDefaultPlatformTextureSettings();
      platform.maxTextureSize = Mathf.Max(platform.maxTextureSize, cellPx * cols);
      if (platform.maxTextureSize < 4096 && cellPx >= 768)
        platform.maxTextureSize = 4096;
      imp.SetPlatformTextureSettings(platform);
      imp.SaveAndReimport();
    }

    static Sprite[] LoadNumberedSprites(string folder, string prefix, int count)
    {
      var list = new List<Sprite>(count);
      for (var i = 0; i < count; i++)
      {
        var path = $"{folder}/{prefix}{i:00}.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
          // サブアセットとして入っている場合
          var assets = AssetDatabase.LoadAllAssetsAtPath(path);
          foreach (var a in assets)
          {
            if (a is Sprite s)
            {
              sprite = s;
              break;
            }
          }
        }

        if (sprite != null)
          list.Add(sprite);
      }

      return list.ToArray();
    }

    static Material[] BuildRampMaterials()
    {
      var shader = Shader.Find(ShaderName);
      var mats = new Material[PaletteNames.Length];
      for (var i = 0; i < PaletteNames.Length; i++)
      {
        var rampPath = $"{EffectRoot}/ramps/StrikeRamp_{PaletteNames[i]}.png";
        var ramp = AssetDatabase.LoadAssetAtPath<Texture2D>(rampPath);
        var matPath = $"{MatFolder}/StrikeFxRamp_{PaletteNames[i]}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
          mat = shader != null
            ? new Material(shader)
            : new Material(Shader.Find("UI/Default"));
          AssetDatabase.CreateAsset(mat, matPath);
        }
        else if (shader != null && mat.shader != shader)
          mat.shader = shader;

        if (ramp != null)
          mat.SetTexture("_RampTex", ramp);
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_HeatBoost", 1f);
        EditorUtility.SetDirty(mat);
        mats[i] = mat;
      }

      return mats;
    }

    static void ConfigureAllImports()
    {
      foreach (var folder in KindFolders)
      {
        ConfigureFolderSprites($"{EffectRoot}/{folder}/frames_mask", srgb: false, maxSize: 512);
        foreach (var pal in PaletteNames)
          ConfigureFolderSprites($"{EffectRoot}/{folder}/frames_{pal}", srgb: true, maxSize: folder == "magic" ? 1024 : 512);
      }

      ConfigureFolderTextures($"{EffectRoot}/ramps", srgb: true, clamp: true, maxSize: 256);
    }

    static void ConfigureFolderSprites(string folder, bool srgb, int maxSize)
    {
      if (!AssetDatabase.IsValidFolder(folder))
        return;
      foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
      {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null)
          continue;
        var dirty = false;
        if (imp.textureType != TextureImporterType.Sprite)
        {
          imp.textureType = TextureImporterType.Sprite;
          dirty = true;
        }
        if (imp.spriteImportMode != SpriteImportMode.Single)
        {
          imp.spriteImportMode = SpriteImportMode.Single;
          dirty = true;
        }
        if (imp.mipmapEnabled)
        {
          imp.mipmapEnabled = false;
          dirty = true;
        }
        if (imp.alphaIsTransparency != true)
        {
          imp.alphaIsTransparency = true;
          dirty = true;
        }
        if (imp.sRGBTexture != srgb)
        {
          imp.sRGBTexture = srgb;
          dirty = true;
        }
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
          settings.spriteMeshType = SpriteMeshType.FullRect;
          imp.SetTextureSettings(settings);
          dirty = true;
        }
        var platform = imp.GetDefaultPlatformTextureSettings();
        if (platform.maxTextureSize < maxSize)
        {
          platform.maxTextureSize = maxSize;
          imp.SetPlatformTextureSettings(platform);
          dirty = true;
        }
        if (dirty)
          imp.SaveAndReimport();
      }
    }

    static void ConfigureFolderTextures(string folder, bool srgb, bool clamp, int maxSize)
    {
      if (!AssetDatabase.IsValidFolder(folder))
        return;
      foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
      {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null)
          continue;
        var dirty = false;
        if (imp.textureType != TextureImporterType.Default)
        {
          imp.textureType = TextureImporterType.Default;
          dirty = true;
        }
        if (imp.mipmapEnabled)
        {
          imp.mipmapEnabled = false;
          dirty = true;
        }
        if (imp.sRGBTexture != srgb)
        {
          imp.sRGBTexture = srgb;
          dirty = true;
        }
        if (clamp && imp.wrapMode != TextureWrapMode.Clamp)
        {
          imp.wrapMode = TextureWrapMode.Clamp;
          dirty = true;
        }
        if (imp.filterMode != FilterMode.Bilinear)
        {
          imp.filterMode = FilterMode.Bilinear;
          dirty = true;
        }
        var platform = imp.GetDefaultPlatformTextureSettings();
        if (platform.maxTextureSize < maxSize)
        {
          platform.maxTextureSize = maxSize;
          imp.SetPlatformTextureSettings(platform);
          dirty = true;
        }
        if (dirty)
          imp.SaveAndReimport();
      }
    }
  }
}
#endif
