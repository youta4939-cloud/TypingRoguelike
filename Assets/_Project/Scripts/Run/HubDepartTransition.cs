using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubDepartTransition : MaskableGraphic
    {
        const float SweepSeconds = .15f;
        const float HoldSeconds = .035f;
        float _left, _right;

        public void Play(bool doorsClosed, Action change, Action finished) =>
            StartCoroutine(Run(doorsClosed, change, finished));

        IEnumerator Run(bool doorsClosed, Action change, Action finished)
        {
            try
            {
                if (!doorsClosed)
                    yield return Sweep(false);
                _right = 1f;
                SetVerticesDirty();
                // Present a fully black frame before replacing the underlying screen.
                yield return null;
                change?.Invoke();
                yield return new WaitForSecondsRealtime(HoldSeconds);
                yield return Sweep(true);
            }
            finally
            {
                finished?.Invoke();
                Destroy(gameObject);
            }
        }

        IEnumerator Sweep(bool revealing)
        {
            for (float elapsed = 0; elapsed < SweepSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float position = elapsed / SweepSeconds;
                if (revealing) _left = position;
                else _right = position;
                SetVerticesDirty();
                yield return null;
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            float slant = rect.width * .055f;
            float start = rect.xMin - slant;
            float left = Mathf.Lerp(start, rect.xMax, _left);
            float right = Mathf.Lerp(start, rect.xMax, _right);
            mesh.AddVert(new Vector2(left, rect.yMin), Color.black, Vector2.zero);
            mesh.AddVert(new Vector2(left + slant, rect.yMax), Color.black, Vector2.zero);
            mesh.AddVert(new Vector2(right + slant, rect.yMax), Color.black, Vector2.zero);
            mesh.AddVert(new Vector2(right, rect.yMin), Color.black, Vector2.zero);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 3);
        }
    }
}
