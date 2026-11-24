using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleDungeonBackdrop : MaskableGraphic
    {
        const string ArtFolder = "Battle/Art/Backgrounds/";
        const string DefaultArt = "DungeonSidePassagePainted";
        static readonly string[] ArtNames =
        {
            DefaultArt,
            "DungeonSideDoorPainted",
            "DungeonRightTurnPainted",
            "DungeonGreatHallPainted"
        };
        // Keep cosmetic choices independent of gameplay's Unity random state.
        static readonly System.Random ArtRandom = new();
        static readonly Vector2 VanishingPoint = new(0.845f, 0.65f);
        const float NearFloor = -0.19f;
        const float WallHeight = 2.65f;
        const float LeftWall = -0.42f;
        const float RightWall = 2.05f;
        const int DepthRows = 10;
        Texture2D _art;

        public override Texture mainTexture => _art != null ? _art : base.mainTexture;

        public static void Ensure(RectTransform parent)
        {
            var savedFrame = parent.Find("DungeonBackdrop");
            if (savedFrame != null)
            {
                var savedStone = savedFrame.Find("StoneFloorAndWalls");
                if (savedStone != null && savedStone.TryGetComponent<CanvasRenderer>(out _)
                    && savedStone.TryGetComponent<BattleDungeonBackdrop>(out var backdrop))
                {
                    backdrop.LoadArt();
                    return;
                }

                // Older HUDs may have been saved before the renderer was required.
                DestroyImmediate(savedFrame.gameObject);
            }

            var frame = new GameObject("DungeonBackdrop", typeof(RectTransform), typeof(RectMask2D));
            frame.transform.SetParent(parent, false);
            frame.transform.SetAsFirstSibling();
            Stretch((RectTransform)frame.transform);

            var stone = new GameObject("StoneFloorAndWalls", typeof(RectTransform), typeof(CanvasRenderer));
            stone.transform.SetParent(frame.transform, false);
            Stretch((RectTransform)stone.transform);
            var newBackdrop = stone.AddComponent<BattleDungeonBackdrop>();
            newBackdrop.raycastTarget = false;
            newBackdrop.LoadArt();
        }

        void LoadArt()
        {
            if (_art != null)
                return;

            var artName = Application.isPlaying ? ArtNames[ArtRandom.Next(ArtNames.Length)] : DefaultArt;
            _art = Resources.Load<Texture2D>(ArtFolder + artName);
            if (_art == null && artName != DefaultArt)
                _art = Resources.Load<Texture2D>(ArtFolder + DefaultArt);

            SetMaterialDirty();
            SetVerticesDirty();
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (_art != null)
            {
                mesh.AddVert(rect.min, Color.white, Vector2.zero);
                mesh.AddVert(new Vector2(rect.xMax, rect.yMin), Color.white, Vector2.right);
                mesh.AddVert(rect.max, Color.white, Vector2.one);
                mesh.AddVert(new Vector2(rect.xMin, rect.yMax), Color.white, Vector2.up);
                mesh.AddTriangle(0, 1, 2);
                mesh.AddTriangle(0, 2, 3);
                return;
            }

            var floorStone = new Color32(63, 63, 73, 255);
            var wallStone = new Color32(53, 52, 63, 255);
            var farStone = new Color32(34, 36, 48, 255);
            var mortar = new Color32(26, 26, 34, 255);
            Quad(mesh, rect, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, farStone);

            // The rear wall closes the corridor at the last row of floor slabs.
            var far = Depth(DepthRows);
            for (var course = 0; course < 9; course++)
            {
                var bottom = NearFloor + course * WallHeight / 9f;
                var top = bottom + WallHeight / 9f;
                for (var column = -1; column < 7; column++)
                {
                    var offset = (course % 2) * 0.5f;
                    var left = Mathf.Clamp01((column + offset) / 7f);
                    var right = Mathf.Clamp01((column + 1f + offset) / 7f);
                    if (right <= left)
                        continue;
                    Block(mesh, rect,
                        Project(Mathf.Lerp(LeftWall, RightWall, left), bottom, far),
                        Project(Mathf.Lerp(LeftWall, RightWall, right), bottom, far),
                        Project(Mathf.Lerp(LeftWall, RightWall, right), top, far),
                        Project(Mathf.Lerp(LeftWall, RightWall, left), top, far),
                        Stone(farStone, course, column, 1f), mortar);
                }
            }

            for (var row = DepthRows - 1; row >= 0; row--)
            {
                var near = Depth(row);
                var next = Depth(row + 1);
                var light = Mathf.Lerp(0.69f, 1.13f, near);
                for (var column = 0; column < 8; column++)
                {
                    var left = Mathf.Lerp(LeftWall, RightWall, column / 8f);
                    var right = Mathf.Lerp(LeftWall, RightWall, (column + 1f) / 8f);
                    Block(mesh, rect, Project(left, NearFloor, near), Project(right, NearFloor, near),
                        Project(right, NearFloor, next), Project(left, NearFloor, next),
                        Stone(floorStone, row, column, light), mortar);
                }

                for (var course = 0; course < 9; course++)
                {
                    var offset = (course % 2) * 0.5f;
                    var wallNear = row == 0 ? 1f : Depth(row + offset);
                    var wallFar = Depth(Mathf.Min(DepthRows, row + 1f + offset));
                    var bottom = NearFloor + course * WallHeight / 9f;
                    var top = bottom + WallHeight / 9f;
                    Block(mesh, rect, Project(LeftWall, bottom, wallNear), Project(LeftWall, bottom, wallFar),
                        Project(LeftWall, top, wallFar), Project(LeftWall, top, wallNear),
                        Stone(wallStone, row, course, light), mortar);
                    Block(mesh, rect, Project(RightWall, bottom, wallFar), Project(RightWall, bottom, wallNear),
                        Project(RightWall, top, wallNear), Project(RightWall, top, wallFar),
                        Stone(wallStone, row, course, light * 0.82f), mortar);
                }
            }
        }

        static float Depth(float row) => 1f / (1f + row * 0.45f);

        static Vector2 Project(float x, float y, float depth) =>
            Vector2.LerpUnclamped(VanishingPoint, new Vector2(x, y), depth);

        static Color Stone(Color baseColor, int row, int column, float light)
        {
            var stone = baseColor * (light * (0.91f + ((row * 13 + column * 7 + 17) % 9) * 0.023f));
            stone.a = 1f;
            return stone;
        }

        static void Block(VertexHelper mesh, Rect rect, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color stone, Color mortar)
        {
            var center = (a + b + c + d) * 0.25f;
            Quad(mesh, rect, a, b, c, d, mortar);
            a = Vector2.Lerp(a, center, 0.042f);
            b = Vector2.Lerp(b, center, 0.042f);
            c = Vector2.Lerp(c, center, 0.042f);
            d = Vector2.Lerp(d, center, 0.042f);
            Quad(mesh, rect, a, b, c, d, stone * new Color(0.87f, 0.87f, 0.89f), stone);
            Quad(mesh, rect, Vector2.Lerp(d, a, 0.095f), Vector2.Lerp(c, b, 0.095f), c, d,
                stone * new Color(1.13f, 1.13f, 1.13f));
        }

        static void Quad(VertexHelper mesh, Rect rect, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color color) => Quad(mesh, rect, a, b, c, d, color, color);

        static void Quad(VertexHelper mesh, Rect rect, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color bottom, Color top)
        {
            var index = mesh.currentVertCount;
            mesh.AddVert(rect.min + Vector2.Scale(a, rect.size), bottom, Vector2.zero);
            mesh.AddVert(rect.min + Vector2.Scale(b, rect.size), bottom, Vector2.zero);
            mesh.AddVert(rect.min + Vector2.Scale(c, rect.size), top, Vector2.zero);
            mesh.AddVert(rect.min + Vector2.Scale(d, rect.size), top, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
