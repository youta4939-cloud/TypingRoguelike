using System.Collections;
using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    /// <summary>SampleScene 等に置く。Play 時に RunFlow（マップ探索→戦闘→報酬）を起動。</summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class BattleSceneSetup : MonoBehaviour
    {
        void Awake()
        {
            EnsureRunFlowHost();
            Debug.Log($"[TypingRoguelike] BattleSceneSetup 起動（シーン: {gameObject.scene.name}）");
        }

        void Start() => StartCoroutine(BootstrapRunUiNextFrame());

        static IEnumerator BootstrapRunUiNextFrame()
        {
            yield return null;
            ApplyVoidCameraBackground();
            var flow = Object.FindAnyObjectByType<RunFlowController>(FindObjectsInactive.Include);
            if (flow == null)
            {
                Debug.LogError(
                    "[TypingRoguelike] RunFlowController がありません。Assets/Scenes/SampleScene を開いて Play してください。");
                yield break;
            }

            flow.EnsureHubVisible();
        }

        void EnsureRunFlowHost()
        {
            ApplyVoidCameraBackground();
            if (!TryGetComponent<RunFlowController>(out _))
                gameObject.AddComponent<RunFlowController>();
        }

        static void ApplyVoidCameraBackground()
        {
            var cam = Camera.main;
            if (cam == null)
                return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BattleUITheme.BgVoid;
        }
    }
}
