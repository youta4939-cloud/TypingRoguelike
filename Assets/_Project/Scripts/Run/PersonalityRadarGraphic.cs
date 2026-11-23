using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>性格の段階を頂点の長さにして塗る。頂点数は渡された配列に従う。</summary>
    public sealed class PersonalityRadarGraphic : MaskableGraphic
    {
        float[] _values = { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };
        int _rings = 5;

        public void SetShape(float[] values, int rings)
        {
            _values = values != null && values.Length >= 3 ? values : new[] { 0.2f, 0.2f, 0.2f };
            _rings = rings < 2 ? 2 : rings;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.46f;
            if (radius <= 1f)
                return;
            var count = _values.Length;
            var grid = new Color(0.85f, 0.75f, 0.45f, 0.28f);
            var fill = new Color(0.95f, 0.72f, 0.28f, 0.55f);
            var edge = new Color(1f, 0.88f, 0.5f, 0.95f);
            for (var ring = 1; ring <= _rings; ring++)
                AddLoop(vh, center, radius * ring / _rings, null, grid, 1.1f);
            for (var i = 0; i < count; i++)
                AddLine(vh, center, center + Vertex(i, count, 1f, radius), 1.1f, grid);
            AddFan(vh, center, radius, _values, fill);
            AddLoop(vh, center, radius, _values, edge, 2f);
        }

        static Vector2 Vertex(int index, int count, float scale, float radius)
        {
            var angle = (90f - 360f * index / count) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * Mathf.Clamp01(scale);
        }

        void AddFan(VertexHelper vh, Vector2 center, float radius, float[] scales, Color color)
        {
            var count = scales.Length;
            var start = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (var i = 0; i < count; i++)
                vh.AddVert(center + Vertex(i, count, scales[i], radius), color, Vector2.zero);
            for (var i = 0; i < count; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
        }

        void AddLoop(VertexHelper vh, Vector2 center, float radius, float[] scales, Color color, float thickness)
        {
            var count = scales != null ? scales.Length : _values.Length;
            for (var i = 0; i < count; i++)
            {
                var a = scales != null ? scales[i] : 1f;
                var b = scales != null ? scales[(i + 1) % count] : 1f;
                AddLine(vh, center + Vertex(i, count, a, radius), center + Vertex((i + 1) % count, count, b, radius), thickness, color);
            }
        }

        static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var delta = b - a;
            if (delta.sqrMagnitude <= 0.01f)
                return;
            var normal = new Vector2(-delta.y, delta.x).normalized * thickness;
            var start = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
