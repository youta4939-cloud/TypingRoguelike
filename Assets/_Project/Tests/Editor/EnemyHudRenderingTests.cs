using NUnit.Framework;
using TypingRoguelike.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Tests
{
    public sealed class EnemyHudRenderingTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void CustomHudGraphics_HaveRendererAndSubmitVisibleMesh(bool shield)
        {
            var root = new GameObject("HudRenderingTest", typeof(RectTransform), typeof(Canvas));
            var go = new GameObject("Graphic", typeof(RectTransform));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                go.transform.SetParent(root.transform, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);
                Graphic graphic = shield
                    ? go.AddComponent<BreakShieldGraphic>()
                    : go.AddComponent<EnemyHpStrip>();
                var renderer = go.GetComponent<CanvasRenderer>();
                Assert.IsNotNull(renderer, "Renderer must exist before the first canvas rebuild.");
                var mode = typeof(Graphic).GetProperty("useLegacyMeshGeneration",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsFalse((bool)mode.GetValue(graphic), "Custom VertexHelper geometry must use the current mesh pipeline.");
                Canvas.ForceUpdateCanvases();
                renderer.cull = false;
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
                var mesh = renderer.GetMesh();
                Assert.IsNotNull(mesh);
                Assert.Greater(mesh.vertexCount, 0);
                Assert.Greater(mesh.bounds.size.x, 0);
                Assert.Greater(mesh.bounds.size.y, 0);
                Assert.IsTrue(System.Array.Exists(mesh.colors32, c => c.a > 0));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
