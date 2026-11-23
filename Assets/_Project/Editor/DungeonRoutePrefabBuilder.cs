#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using TypingRoguelike.RouteLottery;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.EditorTools
{
    public static class DungeonRoutePrefabBuilder
    {
        const string Folder = "Assets/_Project/Resources/UI";
        const string PrefabPath = Folder + "/DungeonRoute.prefab";

        [MenuItem("Tools/TypingRoguelike/Build Dungeon Route Prefab")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog("TypingRoguelike", "進行画面のプレハブを作りました。\n" + PrefabPath, "OK");
        }

        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            var canvasGo = new GameObject("DungeonRouteCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("DungeonRoute", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            var presenter = panel.AddComponent<RouteLotteryPresenter>();
            presenter.BuildPrefabSource();
            Object.DestroyImmediate(presenter);

            var cardsGo = new GameObject("Cards", typeof(RectTransform));
            cardsGo.transform.SetParent(panel.transform, false);
            var cardsRt = cardsGo.GetComponent<RectTransform>();
            cardsRt.anchorMin = cardsRt.anchorMax = new Vector2(0f, 1f);
            cardsRt.pivot = new Vector2(0f, 1f);
            cardsRt.anchoredPosition = new Vector2(152f, -232f);
            cardsRt.sizeDelta = new Vector2(1700f, 660f);

            var view = panel.AddComponent<DungeonRouteView>();
            view.cards = cardsRt;
            view.gap = 20f;
            view.currentWidth = 424f;
            view.currentHeight = 632f;
            view.normalWidth = 380f;
            view.normalHeight = 620f;
            view.normalDrop = 8f;
            view.cardSlots = new RectTransform[4];
            var x = 0f;
            for (var i = 0; i < 4; i++)
            {
                var card = panel.transform.Find("Card" + i) as RectTransform;
                if (card == null)
                    continue;
                card.SetParent(cardsRt, false);
                var current = i == 0;
                var width = current ? view.currentWidth : view.normalWidth;
                var height = current ? view.currentHeight : view.normalHeight;
                var drop = current ? 0f : view.normalDrop;
                card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
                card.pivot = new Vector2(0.5f, 0.5f);
                card.sizeDelta = new Vector2(width, height);
                card.anchoredPosition = new Vector2(x + width * 0.5f, -(drop + height * 0.5f));
                foreach (var component in card.GetComponents<Component>())
                {
                    if (component is RectTransform or CanvasRenderer)
                        continue;
                    Object.DestroyImmediate(component);
                }

                var renderer = card.GetComponent<CanvasRenderer>();
                if (renderer != null)
                    Object.DestroyImmediate(renderer);

                view.cardSlots[i] = card;
                x += width + view.gap;
            }

            view.floorTitle = FindText(panel.transform, "FloorTitle");
            view.coins = FindText(panel.transform, "Coins");
            view.hpValue = FindText(panel.transform, "HpValue");
            view.hpMax = FindText(panel.transform, "HpMax");
            view.madValue = FindText(panel.transform, "MadValue");
            view.madMax = FindText(panel.transform, "MadMax");
            view.hpFill = FindImage(panel.transform, "HpMeter/Fill");
            view.madFill = FindImage(panel.transform, "MadMeter/Fill");
            view.trackLine = panel.transform.Find("TrackLine") as RectTransform;
            view.trackNumbers = new TMP_Text[4];
            view.dots = new Image[4];
            view.portraits = new Image[3];
            view.portraitNames = new TMP_Text[3];
            view.portraitTrust = new TMP_Text[3];
            for (var i = 0; i < 4; i++)
            {
                view.trackNumbers[i] = FindText(panel.transform, "TrackNo" + i);
                view.dots[i] = FindImage(panel.transform, "Dot" + i);
            }

            for (var i = 0; i < 3; i++)
            {
                view.portraits[i] = FindImage(panel.transform, "Portrait" + i + "/Mask/Face");
                view.portraitNames[i] = FindText(panel.transform, "Name" + i);
                view.portraitTrust[i] = FindText(panel.transform, "Trust" + i);
            }

            var menuHit = panel.transform.Find("MenuHit");
            view.menuButton = menuHit != null ? menuHit.GetComponent<Button>() : null;
            var menuIcon = panel.transform.Find("MenuIcon");
            view.menuIcon = menuIcon != null ? menuIcon.gameObject : null;
            var menuRoot = panel.transform.Find("MenuBlocker");
            view.menuRoot = menuRoot != null ? menuRoot.gameObject : null;
            view.menuItems = new Button[4];
            for (var i = 0; i < 4; i++)
            {
                var item = panel.transform.Find("MenuBlocker/Menu/Item" + i);
                view.menuItems[i] = item != null ? item.GetComponent<Button>() : null;
            }

            if (view.hpValue != null)
                view.hpValue.text = "1000";
            if (view.hpMax != null)
                view.hpMax.text = "/ 1000";
            if (view.hpFill != null)
                view.hpFill.fillAmount = 1f;
            if (view.madValue != null)
                view.madValue.text = "0";
            if (view.madMax != null)
                view.madMax.text = "/ 100";
            if (view.floorTitle != null)
                view.floorTitle.text = "第1層";

            PersistFonts(panel);
            PrefabUtility.SaveAsPrefabAsset(panel, PrefabPath);
            Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static TMP_Text FindText(Transform root, string path)
        {
            var child = root.Find(path);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        static Image FindImage(Transform root, string path)
        {
            var child = root.Find(path);
            return child != null ? child.GetComponent<Image>() : null;
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
