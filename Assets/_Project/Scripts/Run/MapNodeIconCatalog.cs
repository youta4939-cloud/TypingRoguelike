using System;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public enum MapNodeShape
    {
        Circle,
        Diamond,
        Square,
        Wide,
        Document
    }

    [Serializable]
    public sealed class MapNodeIconEntry
    {
        public DungeonNodeType type;
        public MapNodeShape shape = MapNodeShape.Circle;
        public Sprite shapeSprite;
        public Sprite picture;
        public string fallbackGlyph = "?";
        public Color color = Color.white;
    }

    /// <summary>マス種類と、形の画像・中の絵の対応。画像が空なら仮の図形と1字を使う。</summary>
    [CreateAssetMenu(fileName = "MapNodeIconCatalog", menuName = "TypingRoguelike/Map Node Icon Catalog")]
    public sealed class MapNodeIconCatalog : ScriptableObject
    {
        public MapNodeIconEntry[] entries;

        public MapNodeIconEntry Find(DungeonNodeType type)
        {
            if (entries == null)
                return null;
            foreach (var entry in entries)
            {
                if (entry != null && entry.type == type)
                    return entry;
            }

            return null;
        }
    }

    public readonly struct MapNodeVisual
    {
        public Sprite Shape { get; }
        public Sprite Picture { get; }
        public MapNodeShape ShapeKind { get; }
        public string Glyph { get; }
        public Color Color { get; }
        public bool Known { get; }

        public MapNodeVisual(Sprite shape, Sprite picture, MapNodeShape shapeKind, string glyph, Color color, bool known)
        {
            Shape = shape;
            Picture = picture;
            ShapeKind = shapeKind;
            Glyph = glyph;
            Color = color;
            Known = known;
        }
    }

    public static class MapNodeIcons
    {
        public static MapNodeIconCatalog Catalog =>
            _catalog != null ? _catalog : _catalog = Resources.Load<MapNodeIconCatalog>("Map/MapNodeIconCatalog");

        static MapNodeIconCatalog _catalog;

        public static MapNodeVisual Resolve(DungeonNodeType type, bool known)
        {
            if (!known)
            {
                return new MapNodeVisual(
                    MapNodeShapes.Get(MapNodeShape.Circle),
                    null,
                    MapNodeShape.Circle,
                    "?",
                    new Color(0.45f, 0.48f, 0.58f),
                    false);
            }

            var entry = Catalog != null ? Catalog.Find(type) : null;
            var kind = entry != null ? entry.shape : DefaultKind(type);
            var shape = entry != null && entry.shapeSprite != null ? entry.shapeSprite : MapNodeShapes.Get(kind);
            var picture = entry != null ? entry.picture : null;
            var glyph = entry != null && !string.IsNullOrEmpty(entry.fallbackGlyph) ? entry.fallbackGlyph : DefaultGlyph(type);
            var color = entry != null ? entry.color : DefaultColor(type);
            return new MapNodeVisual(shape, picture, kind, glyph, color, true);
        }

        public static string Glyph(DungeonNodeType type) => Resolve(type, true).Glyph;

        static MapNodeShape DefaultKind(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Danger => MapNodeShape.Circle,
            DungeonNodeType.Event or DungeonNodeType.EventV2 => MapNodeShape.Diamond,
            DungeonNodeType.Merchant => MapNodeShape.Diamond,
            DungeonNodeType.Treasure => MapNodeShape.Square,
            DungeonNodeType.Rest => MapNodeShape.Wide,
            DungeonNodeType.Mystery => MapNodeShape.Document,
            DungeonNodeType.Boss => MapNodeShape.Diamond,
            DungeonNodeType.Elevator => MapNodeShape.Square,
            _ => MapNodeShape.Circle
        };

        static string DefaultGlyph(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Start => "門",
            DungeonNodeType.Normal => "道",
            DungeonNodeType.Danger => "鎖",
            DungeonNodeType.Rest => "椅",
            DungeonNodeType.Merchant => "秤",
            DungeonNodeType.Treasure => "庫",
            DungeonNodeType.Elevator => "昇",
            DungeonNodeType.Boss => "王",
            DungeonNodeType.Combat => "戦",
            DungeonNodeType.Elite => "強",
            DungeonNodeType.Event or DungeonNodeType.EventV2 => "話",
            DungeonNodeType.Mystery => "封",
            _ => "?"
        };

        static Color DefaultColor(DungeonNodeType type) => type switch
        {
            DungeonNodeType.Start => new Color(0.93f, 0.78f, 0.35f),
            DungeonNodeType.Danger => new Color(0.96f, 0.42f, 0.32f),
            DungeonNodeType.Rest => new Color(0.35f, 0.82f, 0.48f),
            DungeonNodeType.Merchant => new Color(0.93f, 0.78f, 0.35f),
            DungeonNodeType.Treasure => new Color(0.35f, 0.85f, 0.92f),
            DungeonNodeType.Elevator => new Color(0.93f, 0.78f, 0.35f),
            DungeonNodeType.Boss => new Color(0.86f, 0.28f, 0.38f),
            DungeonNodeType.Event or DungeonNodeType.EventV2 => new Color(0.72f, 0.48f, 0.95f),
            DungeonNodeType.Mystery => new Color(0.62f, 0.5f, 0.78f),
            _ => new Color(0.55f, 0.58f, 0.68f)
        };
    }

    public static class MapNodeShapes
    {
        static Sprite _circle;
        static Sprite _diamond;
        static Sprite _square;
        static Sprite _wide;
        static Sprite _document;

        public static Sprite Get(MapNodeShape shape) => shape switch
        {
            MapNodeShape.Diamond => _diamond ??= Diamond(128),
            MapNodeShape.Square => _square ??= Square(128),
            MapNodeShape.Wide => _wide ??= Capsule(160, 72),
            MapNodeShape.Document => _document ??= Document(96, 128),
            _ => _circle ??= Circle(128)
        };

        static Sprite Circle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            var radius = size * 0.46f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.6f)));
                }
            }

            return Finish(tex);
        }

        static Sprite Diamond(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            var limit = size * 0.46f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(limit - d + 0.6f)));
                }
            }

            return Finish(tex);
        }

        static Sprite Square(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var inset = size * 0.12f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var inside = x >= inset && y >= inset && x < size - inset && y < size - inset;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside ? 1f : 0f));
                }
            }

            return Finish(tex);
        }

        static Sprite Capsule(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var radius = height * 0.46f;
            var centerY = (height - 1) * 0.5f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = 0f;
                    if (x < radius)
                        dx = radius - x;
                    else if (x > width - 1 - radius)
                        dx = x - (width - 1 - radius);
                    var dy = Mathf.Abs(y - centerY);
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, dx <= 0f && dy <= radius ? 1f : Mathf.Clamp01(radius - d + 0.6f)));
                }
            }

            return Finish(tex);
        }

        static Sprite Document(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var fold = width * 0.28f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var inPage = x > width * 0.08f && x < width * 0.92f && y > height * 0.06f && y < height * 0.94f;
                    var inFold = x > width * 0.92f - fold && y > height * 0.94f - fold && (width * 0.92f - x) + (height * 0.94f - y) < fold;
                    var a = inPage && !inFold ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            return Finish(tex);
        }

        static Sprite Finish(Texture2D tex)
        {
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
