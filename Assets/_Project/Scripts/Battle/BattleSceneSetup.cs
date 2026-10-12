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
            ApplyVoidCameraBackground();

            if (!TryGetComponent<RunFlowController>(out _))
                gameObject.AddComponent<RunFlowController>();

            Debug.Log($"[TypingRoguelike] BattleSceneSetup 起動（シーン: {gameObject.scene.name}）");
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
