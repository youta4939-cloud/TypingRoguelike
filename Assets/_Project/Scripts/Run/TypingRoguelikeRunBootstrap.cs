using TypingRoguelike.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TypingRoguelike.Run
{
    /// <summary>SampleScene に RunFlow が無い場合の保険。Play 開始直後に1回だけ実行。</summary>
    static class TypingRoguelikeRunBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterFirstSceneLoad()
        {
            var scene = SceneManager.GetActiveScene();
            if (UnityEngine.Object.FindAnyObjectByType<RunFlowController>(FindObjectsInactive.Include) != null)
                return;

            var host = new GameObject("TypingRoguelike_RunHost");
            host.AddComponent<BattleSceneSetup>();
            Debug.LogWarning(
                $"[TypingRoguelike] シーン「{scene.name}」に RunFlowController がありません。自動生成しました。" +
                "通常は Assets/Scenes/SampleScene を開いて Play してください。");
        }
    }
}
