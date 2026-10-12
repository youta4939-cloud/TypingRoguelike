using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    /// <summary>豪華ウィンドウ枠・装飾の生成。</summary>
    public static class BattleUIChrome
    {
        public static RectTransform CreateLuxuryWindow(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string title,
            Color accent,
            BattleUITemplateDefinition template = null)
        {
            template ??= BattleUITemplateLoader.Default;
            var root = BattleUIBuilder.CreatePanelPublic(parent, name, anchorMin, anchorMax, BattleUITheme.BgPanel,
                template?.windowPanel);
            if (template?.windowPanel == null)
                BattleUIBuilder.AddOutlinePublic(root.gameObject, accent, 2);

            var header = BattleUIBuilder.CreatePanelPublic(root, "HeaderBar", new Vector2(0, 1), new Vector2(1, 1),
                Color.Lerp(accent, Color.black, 0.55f));
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.sizeDelta = new Vector2(0, 32);
            headerRt.anchoredPosition = new Vector2(0, -16);

            var titleText = BattleUIBuilder.CreateLabelPublic(header.transform, "Title", title, 13, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, BattleUITheme.TextPrimary);
            titleText.fontStyle = FontStyle.Bold;
            BattleUITheme.AddTextShadow(titleText, new Color(0, 0, 0, 0.9f), new Vector2(2, -2));

            var shine = BattleUIBuilder.CreatePanelPublic(root, "HeaderShine", new Vector2(0, 1), new Vector2(1, 1),
                new Color(1, 1, 1, 0.06f));
            var shineRt = shine.GetComponent<RectTransform>();
            shineRt.sizeDelta = new Vector2(0, 3);
            shineRt.anchoredPosition = new Vector2(0, -30);

            var body = BattleUIBuilder.CreatePanelPublic(root, "Body", new Vector2(0, 0), new Vector2(1, 1),
                new Color(0, 0, 0, 0.15f), null);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.offsetMin = new Vector2(10, 10);
            bodyRt.offsetMax = new Vector2(-10, -38);

            AddCornerGems(root, accent);
            return root;
        }

        public static Image CreateCircularPortrait(Transform parent, Sprite sprite, Color ringColor, float size = 72f)
        {
            var frame = new GameObject("PortraitFrame");
            frame.transform.SetParent(parent, false);
            var frt = frame.AddComponent<RectTransform>();
            frt.sizeDelta = new Vector2(size + 8, size + 8);

            var ring = frame.AddComponent<Image>();
            BattleUITheme.StylePanel(ring, ringColor);
            ring.sprite = BattleUISprites.Circle;
            ring.type = Image.Type.Simple;

            var maskGo = new GameObject("Mask");
            maskGo.transform.SetParent(frame.transform, false);
            var mrt = maskGo.AddComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(4, 4);
            mrt.offsetMax = new Vector2(-4, -4);
            var mask = maskGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var maskImg = maskGo.AddComponent<Image>();
            maskImg.sprite = BattleUISprites.Circle;
            BattleUITheme.StylePanel(maskImg, Color.white);

            var imgGo = new GameObject("Portrait");
            imgGo.transform.SetParent(maskGo.transform, false);
            var irt = imgGo.AddComponent<RectTransform>();
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;
            var portrait = imgGo.AddComponent<Image>();
            portrait.sprite = sprite != null ? sprite : BattleUISprites.Circle;
            portrait.preserveAspect = true;
            portrait.color = sprite != null ? Color.white : new Color(0.35f, 0.35f, 0.4f);
            return portrait;
        }

        public static Image CreateGlow(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = BattleUISprites.RadialGlow;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreatePortraitPanel(Transform parent, Sprite sprite, BattleUITemplateDefinition template, float width = 72f, float height = 96f)
        {
            template ??= BattleUITemplateLoader.Default;
            var frameGo = new GameObject("PortraitFrame");
            frameGo.transform.SetParent(parent, false);
            var frt = frameGo.AddComponent<RectTransform>();
            frt.sizeDelta = new Vector2(width, height);

            var frame = frameGo.AddComponent<Image>();
            BattleUIImage.ApplyTemplatePanel(frame, Color.white, template, template?.portraitFrame);

            var imgGo = new GameObject("Portrait");
            imgGo.transform.SetParent(frameGo.transform, false);
            var irt = imgGo.AddComponent<RectTransform>();
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(6, 6);
            irt.offsetMax = new Vector2(-6, -6);
            var portrait = imgGo.AddComponent<Image>();
            portrait.sprite = sprite;
            portrait.preserveAspect = true;
            portrait.color = sprite != null ? Color.white : new Color(0.35f, 0.35f, 0.4f);
            return portrait;
        }

        static void AddCornerGems(RectTransform root, Color accent)
        {
            var corners = new[]
            {
                (new Vector2(0, 1), new Vector2(0, 1)),
                (new Vector2(1, 1), new Vector2(1, 1)),
                (new Vector2(0, 0), new Vector2(0, 0)),
                (new Vector2(1, 0), new Vector2(1, 0))
            };
            for (var i = 0; i < corners.Length; i++)
            {
                var gem = BattleUIBuilder.CreatePanelPublic(root, $"Gem{i}", corners[i].Item1, corners[i].Item1,
                    Color.Lerp(accent, Color.white, 0.35f));
                var rt = gem.GetComponent<RectTransform>();
                rt.pivot = corners[i].Item2;
                rt.sizeDelta = new Vector2(10, 10);
                rt.anchoredPosition = new Vector2(
                    corners[i].Item2.x < 0.5f ? 6 : -6,
                    corners[i].Item2.y < 0.5f ? 6 : -6);
            }
        }
    }

    /// <summary>スキル条件・候補表示用のオーブ列。</summary>
    public static class BattleUIOrbPattern
    {
        public static RectTransform CreateHost(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("OrbPattern");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static void Apply(RectTransform host, ElementColor[] pattern, float orbSize = 16f, int highlightIndex = -1)
        {
            if (host == null)
                return;

            ClearChildren(host);
            if (pattern == null || pattern.Length == 0)
                return;

            var layout = host.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = host.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 4;
                layout.childAlignment = TextAnchor.MiddleRight;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            for (var i = 0; i < pattern.Length; i++)
                CreateOrb(host.transform, pattern[i], orbSize, i == highlightIndex);
        }

        static void CreateOrb(Transform parent, ElementColor color, float size, bool highlight)
        {
            var go = new GameObject("Orb");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = size;
            le.minHeight = size;

            var img = go.AddComponent<Image>();
            BattleUITheme.EnsureSprite(img);
            img.color = ElementColorUtility.ToUnityColor(color);

            var shineGo = new GameObject("InnerShine");
            shineGo.transform.SetParent(go.transform, false);
            var shineRt = shineGo.AddComponent<RectTransform>();
            shineRt.anchorMin = new Vector2(0.18f, 0.52f);
            shineRt.anchorMax = new Vector2(0.55f, 0.86f);
            shineRt.offsetMin = Vector2.zero;
            shineRt.offsetMax = Vector2.zero;
            var shine = shineGo.AddComponent<Image>();
            shine.sprite = BattleUISprites.Circle;
            shine.color = new Color(1f, 1f, 1f, 0.42f);
            shine.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = highlight ? new Color(1f, 0.95f, 0.35f, 0.95f) : new Color(1f, 1f, 1f, 0.28f);
            outline.effectDistance = highlight ? new Vector2(2, -2) : new Vector2(1, -1);
        }

        static void ClearChildren(Transform t)
        {
            for (var i = t.childCount - 1; i >= 0; i--)
                Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
