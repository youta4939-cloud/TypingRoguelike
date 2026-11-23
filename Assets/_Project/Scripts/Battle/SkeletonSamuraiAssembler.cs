using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public static class SkeletonSamuraiAssembler
    {
        const string MaskResource = SkeletonSamuraiParts.ResourceFolder + "sway_mask_kimono";
        static readonly Dictionary<string, Sprite> SpriteCache = new();
        static Material _kimonoMaterial;

        static readonly string[] Required =
        {
            "01_shadow", "02_legs", "03_kimono_lining", "04_kimono", "05_torso",
            "06_scabbards", "07_sash_strip_L", "08_sash_rope", "09_sash_strip_R",
            "10_arm_R", "11_arm_L_katana", "12_skull", "13_hat",
            "14_hat_talisman_L", "15_hat_chain_L_outer", "16_hat_chain_L_inner",
            "17_hat_talisman_R", "18_hat_chain_R"
        };

        public static bool SpritesReady()
        {
            for (var i = 0; i < Required.Length; i++)
            {
                if (LoadSprite(Required[i]) == null)
                    return false;
            }

            return true;
        }

        public static SkeletonSamuraiIdle CreateUi(RectTransform parent)
        {
            if (parent == null || !SpritesReady())
                return null;

            var feetN = SkeletonSamuraiParts.NormalizedPivot(
                SkeletonSamuraiParts.FeetX, SkeletonSamuraiParts.FeetY);
            var waistN = SkeletonSamuraiParts.NormalizedPivot(
                SkeletonSamuraiParts.WaistX, SkeletonSamuraiParts.WaistY);
            var neckN = SkeletonSamuraiParts.NormalizedPivot(
                SkeletonSamuraiParts.NeckX, SkeletonSamuraiParts.NeckY);

            var rootGo = new GameObject("SkeletonSamurai", typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            var root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.02f);
            root.pivot = feetN;
            root.sizeDelta = new Vector2(SkeletonSamuraiParts.CanvasWidth, SkeletonSamuraiParts.CanvasHeight);
            root.anchoredPosition = Vector2.zero;
            root.localScale = Vector3.one * SkeletonSamuraiParts.UiRootScale;

            var idle = rootGo.AddComponent<SkeletonSamuraiIdle>();
            idle.rootShift = root;

            var torso = MakeRig(root, feetN, waistN,
                SkeletonSamuraiParts.Offset(SkeletonSamuraiParts.Waist, SkeletonSamuraiParts.Feet), "Torso");
            var skull = MakeRig(torso, new Vector2(0.5f, 0.5f), neckN,
                SkeletonSamuraiParts.Offset(SkeletonSamuraiParts.Neck, SkeletonSamuraiParts.Waist), "Skull");

            AddImage(root, feetN, "01_shadow", SkeletonSamuraiParts.Feet);
            AddImage(root, feetN, "02_legs", SkeletonSamuraiParts.Feet);
            AddImage(root, feetN, "03_kimono_lining", SkeletonSamuraiParts.Feet);
            var kimono = AddImage(root, feetN, "04_kimono", SkeletonSamuraiParts.Feet);
            ApplyKimonoSway(kimono);

            AddImage(torso, new Vector2(0.5f, 0.5f), "05_torso", SkeletonSamuraiParts.Waist);
            AddImage(torso, new Vector2(0.5f, 0.5f), "06_scabbards", SkeletonSamuraiParts.Waist);
            idle.sashL = AddSwing(torso, SkeletonSamuraiParts.Waist, new Vector2Int(406, 647), "07_sash_strip_L");
            idle.sashRope = AddSwing(torso, SkeletonSamuraiParts.Waist, new Vector2Int(433, 579), "08_sash_rope");
            idle.sashR = AddSwing(torso, SkeletonSamuraiParts.Waist, new Vector2Int(523, 677), "09_sash_strip_R");
            idle.armR = AddSwing(torso, SkeletonSamuraiParts.Waist, new Vector2Int(718, 546), "10_arm_R");
            idle.armL = AddSwing(torso, SkeletonSamuraiParts.Waist, new Vector2Int(316, 790), "11_arm_L_katana");
            skull.SetSiblingIndex(torso.childCount - 1);

            torso.SetSiblingIndex(4);
            idle.torso = torso;

            AddImage(skull, new Vector2(0.5f, 0.5f), "12_skull", SkeletonSamuraiParts.Neck);
            AddImage(skull, new Vector2(0.5f, 0.5f), "13_hat", SkeletonSamuraiParts.Neck);
            idle.skull = skull;
            idle.talismanL = AddSwing(skull, SkeletonSamuraiParts.Neck, new Vector2Int(319, 240), "14_hat_talisman_L");
            idle.chainLOuter = AddSwing(skull, SkeletonSamuraiParts.Neck, new Vector2Int(265, 232), "15_hat_chain_L_outer");
            idle.chainLInner = AddSwing(skull, SkeletonSamuraiParts.Neck, new Vector2Int(364, 250), "16_hat_chain_L_inner");
            idle.talismanR = AddSwing(skull, SkeletonSamuraiParts.Neck, new Vector2Int(675, 232), "17_hat_talisman_R");
            idle.chainR = AddSwing(skull, SkeletonSamuraiParts.Neck, new Vector2Int(743, 212), "18_hat_chain_R");
            return idle;
        }

        static RectTransform MakeRig(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = position;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            return rt;
        }

        static Image AddImage(Transform parent, Vector2 anchor, string spriteName, Vector2Int spaceOrigin)
        {
            var go = new GameObject(spriteName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            var feet = SkeletonSamuraiParts.NormalizedPivot(
                SkeletonSamuraiParts.FeetX, SkeletonSamuraiParts.FeetY);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = feet;
            rt.sizeDelta = new Vector2(SkeletonSamuraiParts.CanvasWidth, SkeletonSamuraiParts.CanvasHeight);
            rt.anchoredPosition = SkeletonSamuraiParts.Offset(SkeletonSamuraiParts.Feet, spaceOrigin);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            var image = go.AddComponent<Image>();
            image.sprite = LoadSprite(spriteName);
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        static Transform AddSwing(Transform parent, Vector2Int parentJoint, Vector2Int joint, string spriteName)
        {
            var pivot = SkeletonSamuraiParts.NormalizedPivot(joint.x, joint.y);
            var rig = MakeRig(parent, new Vector2(0.5f, 0.5f), pivot,
                SkeletonSamuraiParts.Offset(joint, parentJoint), spriteName + "_pivot");
            AddImage(rig, new Vector2(0.5f, 0.5f), spriteName, joint);
            return rig;
        }

        static void ApplyKimonoSway(Image image)
        {
            if (image == null)
                return;
            var shader = Shader.Find("TypingRoguelike/KimonoSway");
            var mask = Resources.Load<Texture2D>(MaskResource);
            if (shader == null || mask == null)
                return;
            if (_kimonoMaterial == null)
            {
                _kimonoMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _kimonoMaterial.SetTexture("_MaskTex", mask);
                _kimonoMaterial.SetVector("_Canvas", new Vector4(
                    SkeletonSamuraiParts.CanvasWidth, SkeletonSamuraiParts.CanvasHeight, 0f, 0f));
                _kimonoMaterial.SetVector("_Amp", new Vector4(3.5f, 0.8f, 0f, 0f));
                _kimonoMaterial.SetFloat("_Period", 2f);
            }

            image.material = _kimonoMaterial;
        }

        public static Sprite LoadSprite(string name)
        {
            if (SpriteCache.TryGetValue(name, out var cached))
                return cached;

            var path = SkeletonSamuraiParts.ResourceFolder + name;
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
                return null;

            var imported = Resources.Load<Sprite>(path);
            var expected = SkeletonSamuraiParts.NormalizedPivot(
                SkeletonSamuraiParts.FeetX, SkeletonSamuraiParts.FeetY);
            if (imported != null
                && Mathf.Abs(imported.rect.width - texture.width) < 1.5f
                && Mathf.Abs(imported.rect.height - texture.height) < 1.5f
                && Vector2.Distance(imported.pivot, expected) < 0.02f)
            {
                SpriteCache[name] = imported;
                return imported;
            }

            var full = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                SkeletonSamuraiParts.NormalizedPivot(SkeletonSamuraiParts.FeetX, SkeletonSamuraiParts.FeetY),
                SkeletonSamuraiParts.Ppu);
            full.name = name;
            SpriteCache[name] = full;
            return full;
        }
    }
}
