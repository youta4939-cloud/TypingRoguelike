#if UNITY_EDITOR
using TypingRoguelike.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TypingRoguelike.EditorTools
{
    public static class BattleStageSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/BattleStageScene.unity";

        [MenuItem("Tools/TypingRoguelike/Create Battle Stage Scene")]
        public static void CreateFromMenu()
        {
            CreateOrRebuild(saveScene: true, openScene: true);
            EditorUtility.DisplayDialog(
                "TypingRoguelike",
                "BattleStageScene を作成しました。\n" +
                "Game ビュー（Play なし）で 3D 舞台を確認できます。\n" +
                "※ 2D 戦闘 UI / RunFlow とは未接続です。",
                "OK");
        }

        /// <summary>Unity バッチモード用。</summary>
        public static void CreateBattleStageSceneBatch()
        {
            CreateOrRebuild(saveScene: true, openScene: false);
        }

        public static void CreateOrRebuild(bool saveScene, bool openScene)
        {
            Scene scene;
            if (System.IO.File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, openScene ? OpenSceneMode.Single : OpenSceneMode.Additive);
                if (!openScene)
                    EditorSceneManager.SetActiveScene(scene);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }

            var stage = Object.FindFirstObjectByType<BattleStageDirector>();
            GameObject stageGo;
            if (stage == null)
            {
                stageGo = new GameObject("BattleStage");
                stage = stageGo.AddComponent<BattleStageDirector>();
            }
            else
            {
                stageGo = stage.gameObject;
            }

            var cam = Camera.main;
            var light = Object.FindFirstObjectByType<Light>();

            stage.SetEditorReferences(cam, light);
            stage.RebuildStage();

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
                EditorSceneManager.SaveScene(scene, ScenePath);

            Selection.activeGameObject = stageGo;
        }
    }
}
#endif
