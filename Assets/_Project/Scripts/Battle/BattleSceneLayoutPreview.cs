using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TypingRoguelike.Battle
{
    /// <summary>
    /// Edit モードの Game ビュー用レイアウト試作。Play 中・戦闘 UI には触れない。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BattleSceneLayoutPreview : MonoBehaviour
    {
        const string PreviewEnvName = "EditPreview_Environment";
        const string PreviewBackdropName = "EditPreview_Backdrop";
        const string PreviewSpriteName = "EditPreview_EnemySprite";
        const string PreviewRingName = "EditPreview_FieldRing";

        [Header("有効化")]
        [SerializeField] bool previewEnabled = true;
        [SerializeField] bool hideSceneCanvasInPreview = true;
        [SerializeField] bool hidePlaceholderMeshes = true;

        [Header("参照（空なら自動検索）")]
        [SerializeField] Camera targetCamera;
        [SerializeField] Transform enemySpawnPoint;
        [SerializeField] Transform floor;

        [Header("カメラ（斜め俯瞰）")]
        [SerializeField] Vector3 cameraPosition = new(0f, 3f, -5f);
        [SerializeField] Vector3 cameraEuler = new(20f, -12f, 0f);
        [SerializeField] Vector3 sceneFocusPoint = new(0f, 1.2f, 2f);

        [Header("背景（Edit プレビューのみ）")]
        [SerializeField] bool showBackground = true;
        [SerializeField] string backgroundResourcesPath = "Battle/Art/Backgrounds/Battle_Background";
        [SerializeField] float backgroundDistance = 6f;
        [SerializeField] float backgroundScaleMultiplier = 1.15f;

        [Header("敵（画面右寄り・こちらを向く）")]
        [SerializeField] Vector3 enemyWorldPosition = new(2.2f, 1f, 1.2f);
        [SerializeField] Vector2 enemySpriteScale = new(1.8f, 1.8f);
        [SerializeField] string enemySpriteResourcesPath = "Battle/Art/Boss_Dragon";

        [Header("床の楕円リング（演出）")]
        [SerializeField] bool showFieldRing = true;
        [SerializeField] Vector2 ringRadius = new(3.2f, 2.1f);
        [SerializeField] float ringHeight = 0.03f;
        [SerializeField] Color ringColor = new(0.85f, 0.72f, 0.35f, 0.85f);

        Canvas _hiddenCanvas;
        bool _canvasWasActive;
        Material _ringMaterial;
        Material _backdropMaterial;
        bool _refreshQueued;

        void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update += OnEditorUpdate;
#endif
            ScheduleRefresh();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
#endif
            CleanupPreviewArtifacts();
        }

        void OnValidate() => ScheduleRefresh();

        void Update()
        {
            if (Application.isPlaying)
                CleanupPreviewArtifacts();
        }

#if UNITY_EDITOR
        void OnEditorUpdate()
        {
            if (Application.isPlaying || !previewEnabled)
                return;

            if (targetCamera != null)
            {
                targetCamera.transform.position = cameraPosition;
                targetCamera.transform.rotation = Quaternion.Euler(cameraEuler);
            }
        }
#endif

        void ScheduleRefresh()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || _refreshQueued)
                return;

            _refreshQueued = true;
            EditorApplication.delayCall += RunScheduledRefresh;
#else
            RefreshIfEditMode();
#endif
        }

#if UNITY_EDITOR
        void RunScheduledRefresh()
        {
            _refreshQueued = false;
            if (this == null)
                return;

            RefreshIfEditMode();
        }
#endif

        [ContextMenu("Refresh Layout Preview")]
        public void RefreshIfEditMode()
        {
            if (Application.isPlaying || !previewEnabled)
            {
                CleanupPreviewArtifacts();
                return;
            }

            EnsureReferences();
            ApplyCamera();
            ApplyCanvasVisibility(hide: hideSceneCanvasInPreview);
            ApplyBackgroundPreview();
            ApplyEnemyPreview();
            ApplyFieldRing();
            RequestGameViewRepaint();
        }

        void EnsureReferences()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (enemySpawnPoint == null)
            {
                var go = GameObject.Find("EnemySpawnPoint");
                if (go != null)
                    enemySpawnPoint = go.transform;
            }

            if (floor == null)
            {
                var go = GameObject.Find("Floor");
                if (go != null)
                    floor = go.transform;
            }
        }

        void ApplyCamera()
        {
            if (targetCamera == null)
                return;

            targetCamera.transform.position = cameraPosition;
            targetCamera.transform.rotation = Quaternion.Euler(cameraEuler);
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = BattleUITheme.BgVoid;
        }

        void ApplyCanvasVisibility(bool hide)
        {
            if (_hiddenCanvas == null)
            {
                var canvasGo = GameObject.Find("Canvas");
                if (canvasGo != null)
                    _hiddenCanvas = canvasGo.GetComponent<Canvas>();
            }

            if (_hiddenCanvas == null)
                return;

            if (hide)
            {
                if (_hiddenCanvas.gameObject.activeSelf)
                {
                    _canvasWasActive = true;
                    _hiddenCanvas.gameObject.SetActive(false);
                }
            }
            else if (_canvasWasActive)
            {
                _hiddenCanvas.gameObject.SetActive(true);
                _canvasWasActive = false;
            }
        }

        void ApplyBackgroundPreview()
        {
            if (!showBackground)
            {
                DestroyPreviewEnvironment();
                return;
            }

            if (targetCamera == null)
                return;

            var envRoot = EnsurePreviewEnvironmentRoot();
            var backdropGo = EnsurePreviewObject(envRoot.transform, PreviewBackdropName);

            EnsureQuadMesh(backdropGo);

            var sprite = BattlePortraitResolver.LoadSpriteFromResources(backgroundResourcesPath);
            var renderer = backdropGo.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = backdropGo.AddComponent<MeshRenderer>();

            var backdropMat = ResolveBackdropMaterial(sprite?.texture);
            if (backdropMat != null)
                renderer.sharedMaterial = backdropMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var camPos = targetCamera.transform.position;
            var toFocus = sceneFocusPoint - camPos;
            if (toFocus.sqrMagnitude < 0.001f)
                toFocus = targetCamera.transform.forward;
            var viewDir = toFocus.normalized;

            // 焦点より奥（カメラから見て後ろ）に置く。以前の「- viewDir」は手前側に飛んでいた。
            var dist = Mathf.Max(0.5f, backgroundDistance);
            var pos = sceneFocusPoint + viewDir * dist;
            backdropGo.transform.SetParent(envRoot.transform, true);
            backdropGo.transform.position = pos;
            backdropGo.transform.rotation = Quaternion.LookRotation(-viewDir, Vector3.up);

            var planeDist = Mathf.Max(0.5f, Vector3.Dot(pos - camPos, viewDir));
            var fovRad = targetCamera.fieldOfView * Mathf.Deg2Rad;
            var height = 2f * planeDist * Mathf.Tan(fovRad * 0.5f) * backgroundScaleMultiplier;
            var width = height * targetCamera.aspect;
            backdropGo.transform.localScale = new Vector3(width, height, 1f);

            if (sprite == null)
            {
                Debug.LogWarning(
                    $"[BattleSceneLayoutPreview] 背景を読み込めません: Resources/{backgroundResourcesPath}",
                    this);
            }
        }

        static void EnsureQuadMesh(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                return;

            if (mf == null)
                mf = go.AddComponent<MeshFilter>();

            var temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
        }

        Material ResolveBackdropMaterial(Texture texture)
        {
            if (_backdropMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Texture")
                             ?? Shader.Find("Sprites/Default");
                if (shader != null)
                    _backdropMaterial = new Material(shader);
            }

            if (_backdropMaterial != null && texture != null)
            {
                if (_backdropMaterial.HasProperty("_BaseMap"))
                    _backdropMaterial.SetTexture("_BaseMap", texture);
                else
                    _backdropMaterial.mainTexture = texture;
            }

            return _backdropMaterial;
        }

        GameObject EnsurePreviewEnvironmentRoot()
        {
            var existing = GameObject.Find(PreviewEnvName);
            if (existing != null)
                return existing;

            var go = new GameObject(PreviewEnvName);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            return go;
        }

        void ApplyEnemyPreview()
        {
            var previewRoot = EnsurePreviewChild(transform, "EditPreview_Root");
            previewRoot.position = enemyWorldPosition;

            var sprite = BattlePortraitResolver.LoadSpriteFromResources(enemySpriteResourcesPath);
            if (sprite == null)
                sprite = BattlePortraitResolver.LoadBossFromResources();

            var spriteGo = EnsurePreviewObject(previewRoot, PreviewSpriteName);
            var renderer = spriteGo.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = spriteGo.AddComponent<SpriteRenderer>();

            renderer.sprite = sprite;
            renderer.sortingOrder = 10;

            var spriteTf = spriteGo.transform;
            spriteTf.localPosition = new Vector3(0f, 1f, 0f);
            spriteTf.localScale = new Vector3(enemySpriteScale.x, enemySpriteScale.y, 1f);

            if (targetCamera != null)
                spriteTf.rotation = Quaternion.LookRotation(spriteTf.position - targetCamera.transform.position);

            if (sprite == null)
            {
                Debug.LogWarning(
                    "[BattleSceneLayoutPreview] ドラゴン画像を読み込めません。Resources/Battle/Art/Boss_Dragon を確認してください。",
                    this);
            }

            if (!hidePlaceholderMeshes || enemySpawnPoint == null)
                return;

            foreach (Transform child in enemySpawnPoint)
            {
                foreach (var r in child.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;
            }
        }

        void ApplyFieldRing()
        {
            if (!showFieldRing)
            {
                DestroyPreviewChild(PreviewRingName);
                return;
            }

            var parent = floor != null ? floor : transform;
            var ringGo = EnsurePreviewObject(parent, PreviewRingName);
            ringGo.transform.localPosition = new Vector3(0f, ringHeight, 0f);
            ringGo.transform.localRotation = Quaternion.identity;

            var line = ringGo.GetComponent<LineRenderer>();
            if (line == null)
                line = ringGo.AddComponent<LineRenderer>();

            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.12f;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var ringMat = ResolveRingMaterial();
            if (ringMat != null)
                line.sharedMaterial = ringMat;
            line.startColor = ringColor;
            line.endColor = ringColor;

            const int segments = 64;
            if (line.positionCount != segments)
                line.positionCount = segments;
            for (var i = 0; i < segments; i++)
            {
                var t = i / (float)segments * Mathf.PI * 2f;
                var x = Mathf.Cos(t) * ringRadius.x;
                var z = Mathf.Sin(t) * ringRadius.y;
                line.SetPosition(i, new Vector3(x, 0f, z));
            }
        }

        Material ResolveRingMaterial()
        {
            if (_ringMaterial != null)
                return _ringMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            _ringMaterial = shader != null ? new Material(shader) : null;
            return _ringMaterial;
        }

        Transform EnsurePreviewChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        GameObject EnsurePreviewObject(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject(name);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.transform.SetParent(parent, false);
            return go;
        }

        void DestroyPreviewChild(string name)
        {
            if (floor != null)
            {
                var t = floor.Find(name);
                if (t != null)
                    DestroyImmediate(t.gameObject);
            }
        }

        void CleanupPreviewArtifacts()
        {
            ApplyCanvasVisibility(hide: false);
            DestroyPreviewEnvironment();
            DestroyPreviewUnder(transform, "EditPreview_Root");
            DestroyPreviewUnder(floor, PreviewRingName);
            RestorePlaceholderMeshes();
        }

        static void DestroyPreviewEnvironment()
        {
            var env = GameObject.Find(PreviewEnvName);
            if (env != null)
                DestroyImmediate(env);
        }

        static void DestroyPreviewUnder(Transform parent, string name)
        {
            if (parent == null)
                return;

            var t = parent.Find(name);
            if (t != null)
                DestroyImmediate(t.gameObject);
        }

        void RestorePlaceholderMeshes()
        {
            if (enemySpawnPoint == null)
                return;

            foreach (Transform child in enemySpawnPoint)
            {
                foreach (var r in child.GetComponentsInChildren<Renderer>(true))
                    r.enabled = true;
            }
        }

        static void RequestGameViewRepaint()
        {
#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
#endif
        }
    }
}
