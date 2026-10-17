using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    public static class BigMouthAssembler
    {
        public static bool SpritesReady()
        {
            for (var i = 0; i < BigMouthParts.All.Length; i++)
            {
                if (LoadSprite(BigMouthParts.All[i]) == null)
                    return false;
            }

            return true;
        }

        public static BigMouthIdle CreateWorld(Transform parent)
        {
            var root = new GameObject("BigMouth");
            if (parent != null)
                root.transform.SetParent(parent, false);

            var group = root.AddComponent<SortingGroup>();
            group.sortingOrder = 0;
            var idle = root.AddComponent<BigMouthIdle>();
            idle.positionsArePixels = false;
            idle.ppu = BigMouthParts.Ppu;

            var body = BigMouthParts.Get(BigMouthPart.Body);
            Transform bodyTransform = null;
            var created = new Transform[BigMouthParts.All.Length];
            for (var i = 0; i < BigMouthParts.All.Length; i++)
            {
                var spec = BigMouthParts.All[i];
                var go = new GameObject(spec.Name);
                var host = spec.Part == BigMouthPart.Body || bodyTransform == null ? root.transform : bodyTransform;
                go.transform.SetParent(host, false);
                go.transform.localPosition = spec.Part == BigMouthPart.Body
                    ? Vector3.zero
                    : BigMouthParts.LocalOffset(spec, body, BigMouthParts.Ppu, false);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadSprite(spec);
                renderer.sortingOrder = spec.SortingOrder;
                created[i] = go.transform;
                if (spec.Part == BigMouthPart.Body)
                    bodyTransform = go.transform;
            }

            // 影と脚は胴体の兄弟。上のループは胴体より先に作るので、親を付け直す。
            for (var i = 0; i < BigMouthParts.All.Length; i++)
            {
                var spec = BigMouthParts.All[i];
                if (spec.Part == BigMouthPart.Body || bodyTransform == null)
                    continue;
                created[i].SetParent(spec.Part == BigMouthPart.Shadow || spec.Part == BigMouthPart.Legs
                    ? root.transform
                    : bodyTransform, false);
            }

            Bind(idle, created);
            return idle;
        }

        public static BigMouthIdle CreateUi(RectTransform parent)
        {
            if (parent == null || !SpritesReady())
                return null;

            var rootGo = new GameObject("BigMouth", typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            var root = rootGo.GetComponent<RectTransform>();
            var bodySpec = BigMouthParts.Get(BigMouthPart.Body);
            var feet = BigMouthParts.NormalizedPivot(bodySpec.PivotX, bodySpec.PivotY);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.02f);
            root.pivot = feet;
            root.sizeDelta = new Vector2(BigMouthParts.CanvasWidth, BigMouthParts.CanvasHeight);
            root.anchoredPosition = Vector2.zero;
            root.localScale = Vector3.one * 0.62f;

            var idle = rootGo.AddComponent<BigMouthIdle>();
            idle.positionsArePixels = true;
            idle.ppu = 1f;

            Transform body = null;
            var created = new Transform[BigMouthParts.All.Length];
            for (var i = 0; i < BigMouthParts.All.Length; i++)
            {
                var spec = BigMouthParts.All[i];
                var go = new GameObject(spec.Name, typeof(RectTransform));
                go.transform.SetParent(root, false);
                var rt = go.GetComponent<RectTransform>();
                PlaceOnFeet(rt, spec, bodySpec, feet);
                created[i] = rt;
                if (spec.Part == BigMouthPart.Body)
                    body = rt;
            }

            if (body != null)
            {
                var skin = new GameObject("01_body_skin_art", typeof(RectTransform));
                skin.transform.SetParent(body, false);
                var skinRt = skin.GetComponent<RectTransform>();
                var bodyPivot = BigMouthParts.NormalizedPivot(bodySpec.PivotX, bodySpec.PivotY);
                skinRt.anchorMin = skinRt.anchorMax = bodyPivot;
                skinRt.pivot = bodyPivot;
                skinRt.sizeDelta = new Vector2(BigMouthParts.CanvasWidth, BigMouthParts.CanvasHeight);
                skinRt.anchoredPosition = Vector2.zero;
                skinRt.localRotation = Quaternion.identity;
                skinRt.localScale = Vector3.one;
                var skinImage = skin.AddComponent<Image>();
                skinImage.sprite = LoadSprite(bodySpec);
                skinImage.preserveAspect = false;
                skinImage.raycastTarget = false;

                for (var i = 0; i < BigMouthParts.All.Length; i++)
                {
                    var spec = BigMouthParts.All[i];
                    if (spec.Part == BigMouthPart.Body)
                        continue;
                    var underRoot = spec.Part == BigMouthPart.Shadow || spec.Part == BigMouthPart.Legs;
                    var host = underRoot ? root : (RectTransform)body;
                    created[i].SetParent(host, false);
                    PlaceOnFeet((RectTransform)created[i], spec, bodySpec, host.pivot);
                }

                // 描画は兄弟順。口と歯を皮より奥、腕と耳飾りを手前にする。
                created[Index(BigMouthPart.Shadow)].SetSiblingIndex(0);
                created[Index(BigMouthPart.Legs)].SetSiblingIndex(1);
                body.SetSiblingIndex(2);
                created[Index(BigMouthPart.Mouth)].SetSiblingIndex(0);
                created[Index(BigMouthPart.TeethLower)].SetSiblingIndex(1);
                created[Index(BigMouthPart.TeethUpper)].SetSiblingIndex(2);
                skin.transform.SetSiblingIndex(3);
                created[Index(BigMouthPart.ArmL)].SetSiblingIndex(4);
                created[Index(BigMouthPart.ArmR)].SetSiblingIndex(5);
                created[Index(BigMouthPart.EarringL)].SetSiblingIndex(6);
                created[Index(BigMouthPart.EarringR)].SetSiblingIndex(7);
            }

            Bind(idle, created);
            return idle;
        }

        static void PlaceOnFeet(RectTransform rt, BigMouthParts.Spec spec, BigMouthParts.Spec bodySpec, Vector2 anchor)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = BigMouthParts.NormalizedPivot(spec.PivotX, spec.PivotY);
            rt.sizeDelta = new Vector2(BigMouthParts.CanvasWidth, BigMouthParts.CanvasHeight);
            rt.anchoredPosition = spec.Part == BigMouthPart.Body
                ? Vector2.zero
                : (Vector2)BigMouthParts.LocalOffset(spec, bodySpec, 1f, true);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            if (spec.Part == BigMouthPart.Body)
                return;

            var image = rt.GetComponent<Image>();
            if (image == null)
                image = rt.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(spec);
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        static void Bind(BigMouthIdle idle, Transform[] created)
        {
            idle.body = Find(created, BigMouthPart.Body);
            idle.mouth = Find(created, BigMouthPart.Mouth);
            idle.teethUpper = Find(created, BigMouthPart.TeethUpper);
            idle.teethLower = Find(created, BigMouthPart.TeethLower);
            idle.armL = Find(created, BigMouthPart.ArmL);
            idle.armR = Find(created, BigMouthPart.ArmR);
            idle.earringL = Find(created, BigMouthPart.EarringL);
            idle.earringR = Find(created, BigMouthPart.EarringR);
            idle.shadow = Find(created, BigMouthPart.Shadow);
        }

        static Transform Find(Transform[] created, BigMouthPart part)
        {
            return created[Index(part)];
        }

        static int Index(BigMouthPart part)
        {
            for (var i = 0; i < BigMouthParts.All.Length; i++)
            {
                if (BigMouthParts.All[i].Part == part)
                    return i;
            }

            return 0;
        }

        public static Sprite LoadSprite(BigMouthParts.Spec spec)
        {
            var path = BigMouthParts.ResourceFolder + spec.Name;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
                return sprite;

            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
                return null;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                BigMouthParts.NormalizedPivot(spec.PivotX, spec.PivotY),
                BigMouthParts.Ppu);
        }
    }
}
