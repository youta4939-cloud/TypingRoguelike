using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TypingRoguelike.Battle
{
    /// <summary>
    /// 3D バトル舞台（背景・床・敵位置・カメラ）を組み立てる。
    /// BattleStageScene 専用。RunFlow / 2D 戦闘 UI とは未接続。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BattleStageDirector : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] Camera stageCamera;
        [SerializeField] Light keyLight;

        [Header("カメラ（斜め俯瞰）")]
        [SerializeField] Vector3 cameraPosition = new(0f, 3f, -5f);
        [SerializeField] Vector3 cameraEuler = new(20f, -12f, 0f);
        [SerializeField] Vector3 sceneFocusPoint = new(0f, 1.4f, 2.6f);

        [Header("背景")]
        [SerializeField] string backgroundResourcesPath = "Battle/Art/Backgrounds/Battle_Background";
        [SerializeField] float backgroundDepthZ = 9.5f;
        [SerializeField] float backgroundHeight = 3.2f;
        [SerializeField] float backgroundScaleMultiplier = 1.28f;

        [Header("床")]
        [SerializeField] Vector3 floorCenter = new(0f, 0f, 2.2f);
        [SerializeField] Vector3 floorScale = new(1.15f, 1f, 1.05f);
        [SerializeField] Color floorTint = new(0.12f, 0.1f, 0.14f, 1f);

        [Header("敵（奥）")]
        [SerializeField] Vector3 enemyWorldPosition = new(0.65f, 0f, 4.35f);
        [SerializeField] Vector2 enemySpriteScale = new(1.15f, 1.15f);
        [SerializeField] string enemySpriteResourcesPath = "Battle/Art/Boss_Dragon";

        [Header("アリーナ")]
        [SerializeField] bool showArenaRing = true;
        [SerializeField] Vector2 arenaRingRadius = new(3.4f, 2.35f);
        [SerializeField] Color arenaRingColor = new(0.93f, 0.69f, 0.24f, 0.45f);

        Transform _environmentRoot;
        Transform _combatFloorRoot;
        Transform _charactersRoot;

        Material _backdropMaterial;
        Material _floorMaterial;
        Material _ringMaterial;
        bool _applyQueued;

        void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                ScheduleApplyPresentation();
#endif
        }

        void Awake()
        {
            ApplyPresentation();
        }

        void LateUpdate()
        {
            FaceEnemyToCamera();
        }

#if UNITY_EDITOR
        void ScheduleApplyPresentation()
        {
            if (_applyQueued)
                return;
            _applyQueued = true;
            EditorApplication.delayCall += DeferredApplyPresentation;
        }

        void DeferredApplyPresentation()
        {
            _applyQueued = false;
            if (this == null)
                return;
            ApplyPresentation();
        }
#endif

        public void ApplyPresentation()
        {
            ResolveReferences();
            CacheHierarchyRoots();
            ApplyCamera();
            ApplyLighting();
            ApplyFog();
            RefreshExistingEnemyDisplay();
            FaceEnemyToCamera();
        }

#if UNITY_EDITOR
        public void SetEditorReferences(Camera cam, Light light)
        {
            stageCamera = cam;
            keyLight = light;
            EditorUtility.SetDirty(this);
        }
#endif

        [ContextMenu("Rebuild Stage")]
        public void RebuildStage()
        {
            ReleaseRuntimeMaterials();
            ResolveReferences();
            EnsureHierarchyRoots();
            ClearRootChildren(_environmentRoot);
            ClearRootChildren(_combatFloorRoot);
            ClearRootChildren(_charactersRoot);

            BuildBackdrop();
            BuildFloor();
            BuildArenaRing();
            BuildEnemy();

            ApplyPresentation();
        }

        void ResolveReferences()
        {
            if (stageCamera == null)
                stageCamera = Camera.main;
            if (keyLight == null)
                keyLight = FindFirstObjectByType<Light>();
        }

        void EnsureHierarchyRoots()
        {
            _environmentRoot = EnsureChild(transform, "Environment");
            _combatFloorRoot = EnsureChild(transform, "CombatFloor");
            _charactersRoot = EnsureChild(transform, "Characters");
        }

        void CacheHierarchyRoots()
        {
            _environmentRoot = transform.Find("Environment");
            _combatFloorRoot = transform.Find("CombatFloor");
            _charactersRoot = transform.Find("Characters");
        }

        void RefreshExistingEnemyDisplay()
        {
            if (_charactersRoot == null)
                return;

            var enemy = _charactersRoot.Find("Enemy");
            if (enemy == null)
                return;

            var display = enemy.GetComponent<BattleSceneEnemyDisplay>();
            if (display == null)
                return;

            display.ConfigureForStage(enemySpriteResourcesPath, enemySpriteScale, Vector3.zero);
            AlignEnemyFeetToFloor(enemy);
        }

        void ApplyCamera()
        {
            if (stageCamera == null)
                return;

            stageCamera.transform.position = cameraPosition;
            stageCamera.transform.rotation = Quaternion.Euler(cameraEuler);
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = BattleUITheme.BgVoid;
            stageCamera.fieldOfView = 46f;
            stageCamera.nearClipPlane = 0.2f;
            stageCamera.farClipPlane = 120f;
        }

        void ApplyLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.09f, 0.14f);

            if (keyLight == null)
                return;

            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.92f, 0.88f);
            keyLight.intensity = 1.05f;
            keyLight.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
        }

        void ApplyFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = BattleUITheme.BgVoid;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 65f;
        }

        void BuildBackdrop()
        {
            var backdrop = CreateQuad(_environmentRoot, "Backdrop");
            var cam = stageCamera != null ? stageCamera : Camera.main;
            if (cam == null)
                return;

            var camPos = cam.transform.position;
            var pos = new Vector3(sceneFocusPoint.x, backgroundHeight, sceneFocusPoint.z + backgroundDepthZ);
            backdrop.transform.position = pos;

            var toCam = camPos - pos;
            if (toCam.sqrMagnitude < 0.001f)
                toCam = -cam.transform.forward;
            backdrop.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);

            var planeDist = Vector3.Distance(camPos, pos);
            var fovRad = cam.fieldOfView * Mathf.Deg2Rad;
            var height = 2f * planeDist * Mathf.Tan(fovRad * 0.5f) * backgroundScaleMultiplier;
            var width = height * cam.aspect;
            backdrop.transform.localScale = new Vector3(width, height, 1f);

            var sprite = BattlePortraitResolver.LoadSpriteFromResources(backgroundResourcesPath);
            var renderer = backdrop.GetComponent<MeshRenderer>();
            var mat = GetOrCreateBackdropMaterial(sprite != null ? sprite.texture : null);
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.white);
                renderer.sharedMaterial = mat;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(_combatFloorRoot, false);
            floor.transform.position = floorCenter;
            floor.transform.localScale = floorScale;

            var collider = floor.GetComponent<Collider>();
            if (collider != null)
                DestroyObject(collider);

            var renderer = floor.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateFloorMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        void BuildArenaRing()
        {
            if (!showArenaRing)
                return;

            var ringGo = new GameObject("ArenaRing");
            ringGo.transform.SetParent(_combatFloorRoot, false);
            ringGo.transform.position = new Vector3(floorCenter.x, floorCenter.y + 0.04f, floorCenter.z);

            var line = ringGo.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.045f;
            line.numCornerVertices = 6;
            line.numCapVertices = 6;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GetOrCreateRingMaterial();
            line.startColor = arenaRingColor;
            line.endColor = arenaRingColor;

            const int segments = 72;
            line.positionCount = segments;
            for (var i = 0; i < segments; i++)
            {
                var t = i / (float)segments * Mathf.PI * 2f;
                var x = Mathf.Cos(t) * arenaRingRadius.x;
                var z = Mathf.Sin(t) * arenaRingRadius.y;
                line.SetPosition(i, new Vector3(x, 0f, z));
            }
        }

        void BuildEnemy()
        {
            var enemyRoot = new GameObject("Enemy");
            enemyRoot.transform.SetParent(_charactersRoot, false);
            enemyRoot.transform.position = enemyWorldPosition;

            var display = enemyRoot.AddComponent<BattleSceneEnemyDisplay>();
            display.ConfigureForStage(enemySpriteResourcesPath, enemySpriteScale, Vector3.zero);
            AlignEnemyFeetToFloor(enemyRoot.transform);

            FaceEnemyToCamera();
        }

        void AlignEnemyFeetToFloor(Transform enemyRoot)
        {
            var renderer = enemyRoot.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
                return;

            var floorY = floorCenter.y + 0.03f;
            var delta = floorY - renderer.bounds.min.y;
            if (Mathf.Abs(delta) > 0.001f)
                enemyRoot.position += new Vector3(0f, delta, 0f);
        }

        void FaceEnemyToCamera()
        {
            var cam = stageCamera != null ? stageCamera : Camera.main;
            if (cam == null || _charactersRoot == null)
                return;

            var enemy = _charactersRoot.Find("Enemy");
            if (enemy == null)
                return;

            var sprite = enemy.Find("EnemySprite");
            if (sprite == null)
                return;

            sprite.rotation = Quaternion.LookRotation(sprite.position - cam.transform.position);
        }

        Material GetOrCreateBackdropMaterial(Texture texture)
        {
            if (_backdropMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Texture");
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

        Material GetOrCreateFloorMaterial()
        {
            if (_floorMaterial != null)
                return _floorMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            _floorMaterial = shader != null ? new Material(shader) : null;
            if (_floorMaterial == null)
                return null;

            if (_floorMaterial.HasProperty("_BaseMap"))
                _floorMaterial.SetTexture("_BaseMap", null);
            if (_floorMaterial.HasProperty("_BaseColor"))
                _floorMaterial.SetColor("_BaseColor", floorTint);
            else
                _floorMaterial.color = floorTint;

            if (_floorMaterial.HasProperty("_Smoothness"))
                _floorMaterial.SetFloat("_Smoothness", 0.22f);
            if (_floorMaterial.HasProperty("_Metallic"))
                _floorMaterial.SetFloat("_Metallic", 0.05f);

            return _floorMaterial;
        }

        void ReleaseRuntimeMaterials()
        {
            DestroyObject(_backdropMaterial);
            DestroyObject(_floorMaterial);
            DestroyObject(_ringMaterial);
            _backdropMaterial = null;
            _floorMaterial = null;
            _ringMaterial = null;
        }

        Material GetOrCreateRingMaterial()
        {
            if (_ringMaterial != null)
                return _ringMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default");
            _ringMaterial = shader != null ? new Material(shader) : null;
            return _ringMaterial;
        }

        static GameObject CreateQuad(Transform parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                DestroyObject(collider);
            return go;
        }

        static Transform EnsureChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null)
                return t;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void ClearRootChildren(Transform root)
        {
            if (root == null)
                return;

            for (var i = root.childCount - 1; i >= 0; i--)
                DestroyObject(root.GetChild(i).gameObject);
        }

        static void DestroyObject(Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(obj);
            else
#endif
                Destroy(obj);
        }
    }
}
