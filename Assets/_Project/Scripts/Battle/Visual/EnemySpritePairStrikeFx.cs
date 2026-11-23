using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle.Visual
{
  /// <summary>
  /// 着弾エフェクト。StrikeFX_v1 の連番＋局所フラッシュ／端帯。
  /// </summary>
  public sealed class EnemySpritePairStrikeFx : MonoBehaviour
  {
    const float FlashDuration = 0.42f;
    const float EdgeDuration = 0.50f;
    const float UiOverlayAlpha = 0.35f;

    Image _fxA;
    Image _fxB;
    Image _flash;
    Image _edge;

    float _flashTimer;
    float _edgeTimer;
    float _animTime;
    float _animDuration;
    float _fps = 30f;
    Sprite[] _frames;
    bool _animPlaying;
    bool _suppressEdge;
    bool _frozen;

    EnemyVisualPresetLibrary Library => EnemyVisualPresetLibrary.Instance;

    public static EnemySpritePairStrikeFx Create(RectTransform parent)
    {
      var root = new GameObject("SpritePairStrikeFx");
      root.transform.SetParent(parent, false);
      var rt = root.AddComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.one;
      rt.offsetMin = Vector2.zero;
      rt.offsetMax = Vector2.zero;
      var fx = root.AddComponent<EnemySpritePairStrikeFx>();
      fx.Build();
      return fx;
    }

    void Build()
    {
      if (_flash == null)
      {
        // Solid 四角だと斬撃マスクの透明部から「白い板」が見える。放射減衰にする。
        _flash = MakeImage("LocalFlash", new Color(1f, 1f, 1f, 0f), BattleUISprites.RadialGlow);
        _flash.rectTransform.sizeDelta = new Vector2(280f, 280f);
      }

      if (_edge == null)
      {
        _edge = MakeImage("EdgeBand", new Color(0.5f, 0.02f, 0.02f, 0f));
        _edge.rectTransform.anchorMin = new Vector2(1f, 0f);
        _edge.rectTransform.anchorMax = new Vector2(1f, 1f);
        _edge.rectTransform.pivot = new Vector2(1f, 0.5f);
        _edge.rectTransform.sizeDelta = new Vector2(16f, 0f);
      }

      if (_fxA == null)
      {
        _fxA = MakeImage("StrikeFxA", Color.white);
        _fxA.gameObject.SetActive(false);
        _fxA.preserveAspect = true;
      }

      if (_fxB == null)
      {
        _fxB = MakeImage("StrikeFxB_Ui35", Color.white);
        _fxB.gameObject.SetActive(false);
        _fxB.preserveAspect = true;
      }
    }

    void ClearTimers()
    {
      _flashTimer = 0f;
      _edgeTimer = 0f;
      StopAnim();
    }

    void StopAnim()
    {
      _animPlaying = false;
      _animTime = 0f;
      _frames = null;
      if (_fxA != null)
        _fxA.gameObject.SetActive(false);
      if (_fxB != null)
        _fxB.gameObject.SetActive(false);
    }

    Image MakeImage(string name, Color color, Sprite sprite = null)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var rt = go.AddComponent<RectTransform>();
      rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
      rt.pivot = new Vector2(0.5f, 0.5f);
      var img = go.AddComponent<Image>();
      img.sprite = sprite != null ? sprite : BattleUISprites.Solid;
      img.color = color;
      img.raycastTarget = false;
      return img;
    }

    /// <param name="enemyHeightEh">敵の表示高さ（px / UI単位）。</param>
    public void PlayStrike(EnemyDefinition enemy, EnemyAttackPattern pattern, float enemyHeightEh)
    {
      if (_flash == null || _edge == null || _fxA == null || _fxB == null)
        Build();
      if (_flash == null || _edge == null)
        return;

      var reg = enemy != null ? enemy.visual : EnemyVisualRegistration.Default;
      var kind = reg.strikeEffect;
      _suppressEdge = kind == EnemyStrikeEffectKind.Magic;

      if (kind == EnemyStrikeEffectKind.None)
      {
        StopAnim();
        _edgeTimer = 0f;
        // クリップ無しのときだけ局所フラッシュ（クリップ有りだと四角板に見える）
        _flashTimer = FlashDuration;
        return;
      }

      if (!_suppressEdge)
      {
        var paletteColors = EnemyVisualPaletteColors.Resolve(reg.effectPalette);
        _edge.color = new Color(paletteColors.EdgeGlow.r, paletteColors.EdgeGlow.g, paletteColors.EdgeGlow.b, 0f);
        _edgeTimer = EdgeDuration;
      }
      else
        _edgeTimer = 0f;

      var lib = Library;
      var intensity = lib != null
        ? lib.ResolveIntensity(enemy, pattern)
        : reg.effectIntensity;
      var intensityMul = lib != null ? lib.IntensityMultiplier(intensity) : 1f;
      if (reg.strikeScaleOverride > 0f)
        intensityMul = reg.strikeScaleOverride;

      if (TryStartClip(kind, reg.effectPalette, reg.strikeOffsetEh, enemyHeightEh, intensityMul))
      {
        // StrikeFX_v1 自体に着弾フラッシュがあるので、Solid 板フラッシュは出さない
        _flashTimer = 0f;
        if (_flash != null)
          _flash.color = new Color(1f, 1f, 1f, 0f);
      }
      else
      {
        _flashTimer = FlashDuration;
        Debug.LogWarning(
          $"EnemyStrikeFx: clip missing for {kind}. Tools → TypingRoguelike → Build Strike FX Catalog (v1) を実行してください。");
      }
    }

    /// <summary>後方互換（Eh 推定）。</summary>
    public void PlayStrike(EnemyDefinition enemy, EnemyAttackPattern pattern) =>
      PlayStrike(enemy, pattern, 320f);

    /// <summary>Swing ヒットストップ用。true の間はコマ送りを止める（フラッシュ等は継続）。</summary>
    public void SetFrozen(bool frozen) => _frozen = frozen;

    bool TryStartClip(
      EnemyStrikeEffectKind kind,
      EnemyEffectPalette palette,
      Vector2 strikeOffsetEh,
      float enemyHeightEh,
      float intensityMul)
    {
      var catalog = EnemyStrikeFxCatalog.Instance;
      var clip = catalog != null ? catalog.Get(kind) : null;
      if (clip == null || clip.FrameCount <= 0)
        return false;

      var rampMat = catalog.GetRampMaterial(palette);
      var rampOk = rampMat != null && rampMat.shader != null &&
                   !rampMat.shader.name.Contains("Error") &&
                   rampMat.shader.name.Contains("StrikeFxRamp");
      var preferMask = rampOk && clip.maskFrames != null && clip.maskFrames.Length > 0;
      var frames = clip.ResolveFrames(palette, preferMask);
      if (frames == null || frames.Length == 0)
      {
        preferMask = false;
        frames = clip.ResolveFrames(palette, preferMask: false);
      }
      if (frames == null || frames.Length == 0)
        return false;

      if (!preferMask && !rampOk && clip.maskFrames != null && clip.maskFrames.Length > 0)
        Debug.LogWarning("EnemyStrikeFx: StrikeFxRamp シェーダーが使えないため色付きフレームにフォールバックしました。");

      _frames = frames;
      _fps = clip.fps > 0.01f ? clip.fps : 30f;
      _animDuration = frames.Length / _fps;
      _animTime = 0f;
      _animPlaying = true;

      var eh = Mathf.Max(8f, enemyHeightEh);
      var size = clip.displaySizeEh * eh * Mathf.Max(0.1f, intensityMul);
      var pos = (clip.centerOffsetEh + strikeOffsetEh) * eh;

      SetupFxImage(_fxA, frames[0], size, pos, 1f, preferMask ? rampMat : null);
      SetupFxImage(_fxB, frames[0], size, pos, UiOverlayAlpha, preferMask ? rampMat : null);
      return true;
    }

    void SetupFxImage(Image img, Sprite sprite, float size, Vector2 anchoredPos, float alpha, Material mat)
    {
      if (img == null)
        return;
      img.sprite = sprite;
      img.preserveAspect = true;
      img.material = mat;
      img.color = new Color(1f, 1f, 1f, alpha);
      var rt = img.rectTransform;
      rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.08f);
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = anchoredPos;
      rt.sizeDelta = new Vector2(size, size);
      img.gameObject.SetActive(true);
      img.transform.SetAsLastSibling();
    }

    void Update()
    {
      if (!_animPlaying && _flashTimer <= 0f && _edgeTimer <= 0f)
        return;

      if (_flash == null || _edge == null || _fxA == null || _fxB == null)
        Build();

      var dt = Time.deltaTime;
      if (dt <= 0f)
        return;

      if (_animPlaying && !_frozen)
        TickAnim(dt);

      if (_flashTimer > 0f && _flash != null)
      {
        _flashTimer -= dt;
        var ts = FlashDuration - _flashTimer;
        var lib = Library;
        var peak = (lib != null ? lib.localFlashPeakMedium : 0.55f) * 0.40f;
        var flash = 0f;
        if (ts > 0.04f)
          flash = peak * Mathf.Exp(-Mathf.Pow((ts - 0.04f) / 0.04f, 2f));
        _flash.color = new Color(1f, 1f, 1f, flash);
      }

      if (_edgeTimer > 0f && _edge != null && !_suppressEdge)
      {
        _edgeTimer -= dt;
        var a = Mathf.Clamp01(_edgeTimer / EdgeDuration) * 0.30f * 0.35f;
        var c = _edge.color;
        c.a = a;
        _edge.color = c;
      }
      else if (_suppressEdge && _edge != null)
      {
        var c = _edge.color;
        c.a = 0f;
        _edge.color = c;
      }
    }

    void TickAnim(float dt)
    {
      _animTime += dt;
      if (_frames == null || _frames.Length == 0)
      {
        StopAnim();
        return;
      }

      var frame = Mathf.Clamp(Mathf.FloorToInt(_animTime * _fps), 0, _frames.Length - 1);
      var sprite = _frames[frame];
      if (_fxA != null && _fxA.sprite != sprite)
        _fxA.sprite = sprite;
      if (_fxB != null && _fxB.sprite != sprite)
        _fxB.sprite = sprite;

      if (_animTime >= _animDuration)
        StopAnim();
    }
  }
}
