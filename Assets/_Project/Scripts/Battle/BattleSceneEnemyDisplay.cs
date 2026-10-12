using UnityEngine;

namespace TypingRoguelike.Battle
{
    /// <summary>
    /// 3D シーン内（EnemySpawnPoint 等）に敵立ち絵スプライトを表示する試作用。
    /// 戦闘ロジックとは未接続。Resources の Boss 画像をそのまま使う。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleSceneEnemyDisplay : MonoBehaviour
    {
        [Tooltip("空なら Battle/Art/Boss_Dragon")]
        [SerializeField] string resourcesPath = "Battle/Art/Boss_Dragon";
        [SerializeField] Sprite overrideSprite;
        [SerializeField] bool faceMainCamera = true;
        [SerializeField] Vector2 scale = new(1.8f, 1.8f);
        [SerializeField] Vector3 localOffset = Vector3.zero;

        Transform _spriteRoot;
        SpriteRenderer _renderer;

        void Awake()
        {
            EnsureRenderer();
            ApplySprite();
        }

        void LateUpdate()
        {
            if (!faceMainCamera || _spriteRoot == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            _spriteRoot.rotation = Quaternion.LookRotation(_spriteRoot.position - cam.transform.position);
        }

        void EnsureRenderer()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer != null)
            {
                _spriteRoot = _renderer.transform;
                _spriteRoot.localPosition = localOffset;
                _spriteRoot.localScale = new Vector3(scale.x, scale.y, 1f);
                return;
            }

            var go = new GameObject("EnemySprite");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            _spriteRoot = go.transform;
            _renderer = go.AddComponent<SpriteRenderer>();
        }

        public void ConfigureForStage(string resourcesPathOverride, Vector2 scaleOverride, Vector3 offsetOverride)
        {
            if (!string.IsNullOrWhiteSpace(resourcesPathOverride))
                resourcesPath = resourcesPathOverride.Trim();
            scale = scaleOverride;
            localOffset = offsetOverride;
            EnsureRenderer();
            ApplySprite();
        }

        void ApplySprite()
        {
            if (_renderer == null)
                return;

            var sprite = overrideSprite;
            if (sprite == null && !string.IsNullOrWhiteSpace(resourcesPath))
                sprite = BattlePortraitResolver.LoadSpriteFromResources(resourcesPath.Trim());
            if (sprite == null)
                sprite = BattlePortraitResolver.LoadBossFromResources();

            _renderer.sprite = sprite;
            if (sprite == null)
                Debug.LogWarning(
                    $"[BattleSceneEnemyDisplay] スプライトがありません。Tools → Generate Battle Art か Resources/{resourcesPath} を確認してください。",
                    this);
        }
    }
}
