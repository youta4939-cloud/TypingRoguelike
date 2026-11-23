#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using TypingRoguelike.Battle;
using TypingRoguelike.RouteLottery;
using TypingRoguelike.Run;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.EditorTools
{
    public static class ScreenPrefabBuilder
    {
        const string Folder = "Assets/_Project/Resources/UI";

        [MenuItem("Tools/TypingRoguelike/Build Screen Prefabs")]
        public static void BuildFromMenu()
        {
            Build();
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "TypingRoguelike",
                    "拠点、盤面、戦闘、出来事のプレハブを作りました。\n" + Folder,
                    "OK");
            }
        }

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            BuildHub();
            BuildBoard();
            BuildBattle();
            BuildStory();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Screen prefabs saved under " + Folder);
        }

        static void BuildHub()
        {
            var host = new GameObject("HubHost");
            try
            {
                var overlay = new RunOverlayUI();
                var panel = overlay.BuildHubPrefabSource(host.transform);
                if (panel == null)
                    throw new System.InvalidOperationException("拠点プレハブの元画面が作れません");
                PrefabUtility.SaveAsPrefabAsset(panel, Folder + "/Hub.prefab");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static void BuildBoard()
        {
            var host = new GameObject("BoardHost");
            try
            {
                var presenter = host.AddComponent<RouteLotteryPresenter>();
                presenter.ExportPrefabRoots();
                var build = host.transform.Find("Build");
                var lottery = host.transform.Find("Lottery");
                if (build == null || lottery == null)
                    throw new System.InvalidOperationException("盤面プレハブの元画面が作れません");
                PersistFonts(build.gameObject);
                PersistFonts(lottery.gameObject);
                PrefabUtility.SaveAsPrefabAsset(build.gameObject, Folder + "/BoardBuild.prefab");
                PrefabUtility.SaveAsPrefabAsset(lottery.gameObject, Folder + "/BoardLottery.prefab");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static void BuildBattle()
        {
            var canvas = BattleUIBuilder.BuildPrefabSource();
            try
            {
                if (canvas == null)
                    throw new System.InvalidOperationException("戦闘プレハブの元画面が作れません");
                PersistFonts(canvas);
                PrefabUtility.SaveAsPrefabAsset(canvas, Folder + "/BattleHud.prefab");
            }
            finally
            {
                if (canvas != null)
                    Object.DestroyImmediate(canvas.transform.root.gameObject);
            }
        }

        [MenuItem("Tools/TypingRoguelike/Migrate Event UI To Prefab")]
        public static void MigrateStoryEvent()
        {
            BuildStory();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/StoryEvent.prefab"));
            Debug.Log("Event UI prefab ready: " + Folder + "/StoryEvent.prefab");
        }

        static void BuildStory()
        {
            // Existing authored layouts must never be overwritten by a general rebuild.
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/StoryEvent.prefab");
            if (existing != null && existing.GetComponent<StoryEventPrefabView>() != null) return;
            var ui = new LayerPathUI();
            var panel = ui.BuildStoryPrefabSource();
            try
            {
                if (panel == null)
                    throw new System.InvalidOperationException("出来事プレハブの元画面が作れません");
                PersistFonts(panel);
                PersistStorySprites(panel);
                PrefabUtility.SaveAsPrefabAsset(panel, Folder + "/StoryEvent.prefab");
            }
            finally
            {
                if (panel != null && panel.transform.root != null)
                    Object.DestroyImmediate(panel.transform.root.gameObject);
            }
        }

        static void PersistStorySprites(GameObject panel)
        {
            const string artFolder = "Assets/_Project/UI/StoryEvent";
            Directory.CreateDirectory(artFolder);
            var saved = new Dictionary<int, Sprite>();
            foreach (var image in panel.GetComponentsInChildren<Image>(true))
            {
                var sprite = image.sprite;
                if (sprite == null || EditorUtility.IsPersistent(sprite)) continue;
                if (!saved.TryGetValue(sprite.GetInstanceID(), out var persistent))
                {
                    var path = artFolder + "/" + (sprite == BattleUISprites.HorizontalGradient ? "ReadingGradient" : "Solid") + ".png";
                    var prior = RenderTexture.active;
                    var target = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0);
                    Texture2D copy = null;
                    try
                    {
                        Graphics.Blit(sprite.texture, target);
                        RenderTexture.active = target;
                        copy = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                        copy.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        copy.Apply();
                        File.WriteAllBytes(path, copy.EncodeToPNG());
                    }
                    finally
                    {
                        RenderTexture.active = prior;
                        RenderTexture.ReleaseTemporary(target);
                        if (copy != null) Object.DestroyImmediate(copy);
                    }
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                    persistent = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    saved[sprite.GetInstanceID()] = persistent;
                }
                image.sprite = persistent;
            }
        }

        static void PersistFonts(GameObject panel)
        {
            var saved = new Dictionary<int, TMP_FontAsset>();
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                var font = text.font;
                if (font == null || EditorUtility.IsPersistent(font))
                    continue;
                if (!saved.TryGetValue(font.GetInstanceID(), out var asset))
                {
                    var safe = font.name.Replace(" (Runtime)", "").Replace(" ", "");
                    var path = "Assets/_Project/Resources/Battle/Fonts/" + safe + "_SDF.asset";
                    if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) == null)
                    {
                        AssetDatabase.CreateAsset(font, path);
                        if (font.material != null && !EditorUtility.IsPersistent(font.material))
                            AssetDatabase.AddObjectToAsset(font.material, path);
                        if (font.atlasTextures != null)
                        {
                            foreach (var tex in font.atlasTextures)
                            {
                                if (tex != null && !EditorUtility.IsPersistent(tex))
                                    AssetDatabase.AddObjectToAsset(tex, path);
                            }
                        }

                        AssetDatabase.SaveAssets();
                    }

                    asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                    saved[font.GetInstanceID()] = asset;
                }

                if (asset != null)
                    text.font = asset;
            }
        }
    }
}
#endif
