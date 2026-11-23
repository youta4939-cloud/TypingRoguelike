using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public static class PaperAngelAssembler
    {
        static readonly Dictionary<string, Sprite> SpriteCache = new();

        public static bool SpritesReady()
        {
            for (var i = 0; i < PaperAngelParts.All.Length; i++)
            {
                var spec = PaperAngelParts.All[i];
                if (spec.Part == PaperAngelPart.SealGlow)
                    continue;
                if (LoadSprite(spec) == null)
                    return false;
            }

            return true;
        }

        public static PaperAngelIdle CreateUi(RectTransform parent)
        {
            if (parent == null || !SpritesReady())
                return null;

            var bodySpec = PaperAngelParts.BodySpec;
            var feet = PaperAngelParts.NormalizedPivot(bodySpec.PivotX, bodySpec.PivotY);

            var rootGo = new GameObject("PaperAngel", typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            var root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.02f);
            root.pivot = feet;
            root.sizeDelta = new Vector2(PaperAngelParts.CanvasWidth, PaperAngelParts.CanvasHeight);
            root.anchoredPosition = Vector2.zero;
            root.localScale = Vector3.one * PaperAngelParts.UiRootScale;

            var idle = rootGo.AddComponent<PaperAngelIdle>();
            idle.positionsArePixels = true;

            var created = new RectTransform[PaperAngelParts.All.Length];
            for (var i = 0; i < PaperAngelParts.All.Length; i++)
            {
                var spec = PaperAngelParts.All[i];
                var go = new GameObject(spec.Name, typeof(RectTransform));
                go.transform.SetParent(root, false);
                created[i] = go.GetComponent<RectTransform>();
            }

            var rig = new GameObject("Rig", typeof(RectTransform)).GetComponent<RectTransform>();
            rig.SetParent(root, false);
            PlaceStackLayer(rig, feet);

            for (var i = 0; i < PaperAngelParts.All.Length; i++)
            {
                var spec = PaperAngelParts.All[i];
                var rt = created[i];
                if (spec.OnRoot)
                {
                    PlaceStackLayer(rt, feet);
                    EnsureStackedImage(rt, spec);
                    continue;
                }

                rt.SetParent(rig, false);
                if (UsesShoulderPivot(spec.Part) || spec.Part == PaperAngelPart.Seal)
                    PlaceRotatorLayer(rt, spec, bodySpec, feet);
                else
                    PlaceStackLayer(rt, feet);
            }

            foreach (var part in new[]
                     {
                         PaperAngelPart.WingLInner, PaperAngelPart.WingLOuter, PaperAngelPart.WingRInner,
                         PaperAngelPart.WingROuter, PaperAngelPart.Feet, PaperAngelPart.FaceInner, PaperAngelPart.Body,
                         PaperAngelPart.Seal
                     })
                EnsureStackedImage(created[Index(part)], PaperAngelParts.Get(part));

            var glowRt = created[Index(PaperAngelPart.SealGlow)];
            PlaceStackLayer(glowRt, feet);
            glowRt.gameObject.SetActive(false);

            created[Index(PaperAngelPart.Shadow)].SetSiblingIndex(0);
            rig.SetSiblingIndex(1);

            idle.rig = rig;
            idle.shadow = created[Index(PaperAngelPart.Shadow)];
            idle.wingLInner = created[Index(PaperAngelPart.WingLInner)];
            idle.wingLOuter = created[Index(PaperAngelPart.WingLOuter)];
            idle.wingRInner = created[Index(PaperAngelPart.WingRInner)];
            idle.wingROuter = created[Index(PaperAngelPart.WingROuter)];
            idle.faceInner = created[Index(PaperAngelPart.FaceInner)];
            idle.sealPulse = created[Index(PaperAngelPart.Seal)];
            return idle;
        }

        static bool UsesShoulderPivot(PaperAngelPart part) =>
            part is PaperAngelPart.WingLInner or PaperAngelPart.WingLOuter
                or PaperAngelPart.WingRInner or PaperAngelPart.WingROuter
                or PaperAngelPart.FaceInner;

        /// <summary>1476×1908 を足元 Pivot で (0,0) 重ねる。</summary>
        static void PlaceStackLayer(RectTransform rt, Vector2 feetAnchor)
        {
            rt.anchorMin = rt.anchorMax = feetAnchor;
            rt.pivot = feetAnchor;
            rt.sizeDelta = new Vector2(PaperAngelParts.CanvasWidth, PaperAngelParts.CanvasHeight);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        /// <summary>羽・顔だけ Pivot 軸で回す（キャンバス位置は README の pivot どおり）。</summary>
        static void PlaceRotatorLayer(
            RectTransform rt,
            PaperAngelParts.Spec spec,
            PaperAngelParts.Spec bodySpec,
            Vector2 feetAnchor)
        {
            rt.anchorMin = rt.anchorMax = feetAnchor;
            rt.pivot = PaperAngelParts.NormalizedPivot(spec.PivotX, spec.PivotY);
            rt.sizeDelta = new Vector2(PaperAngelParts.CanvasWidth, PaperAngelParts.CanvasHeight);
            rt.anchoredPosition = (Vector2)PaperAngelParts.LocalOffset(spec, bodySpec, 1f, true);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        static void EnsureStackedImage(RectTransform rt, PaperAngelParts.Spec spec)
        {
            var image = rt.GetComponent<Image>();
            if (image == null)
                image = rt.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(spec);
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        static int Index(PaperAngelPart part)
        {
            for (var i = 0; i < PaperAngelParts.All.Length; i++)
            {
                if (PaperAngelParts.All[i].Part == part)
                    return i;
            }

            return 0;
        }

        public static Sprite LoadSprite(PaperAngelParts.Spec spec)
        {
            if (SpriteCache.TryGetValue(spec.Name, out var cached))
                return cached;

            var path = PaperAngelParts.ResourceFolder + spec.Name;
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
                return null;

            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null && CoversFullCanvas(sprite, texture))
            {
                SpriteCache[spec.Name] = sprite;
                return sprite;
            }

            var full = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                PaperAngelParts.NormalizedPivot(spec.PivotX, spec.PivotY),
                PaperAngelParts.Ppu);
            full.name = spec.Name;
            SpriteCache[spec.Name] = full;
            return full;
        }

        static bool CoversFullCanvas(Sprite sprite, Texture2D texture)
        {
            if (sprite == null || texture == null)
                return false;
            return Mathf.Abs(sprite.rect.width - texture.width) < 1.5f
                   && Mathf.Abs(sprite.rect.height - texture.height) < 1.5f;
        }
    }
}
