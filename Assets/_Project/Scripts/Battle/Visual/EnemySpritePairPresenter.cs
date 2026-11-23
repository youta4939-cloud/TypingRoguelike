using TypingRoguelike.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Battle.Visual
{
  public enum SpritePairPresenterMode
  {
    Idle,
    Attacking,
    HitReact,
    Defeated
  }

  public sealed class EnemySpritePairPresenter : MonoBehaviour
  {
    EnemyDefinition _enemy;
    EnemyVisualPresetLibrary _library;

    RectTransform _body;
    Image _image;
    EnemySpritePairStrikeFx _strikeFx;
    EnemySpritePairShake _shake;
    RectTransform _feetAnchor;

    Sprite _idleSprite;
    Sprite _attackSprite;

    SpritePairPresenterMode _mode = SpritePairPresenterMode.Idle;
    float _modeTime;
    EnemyAttackPattern _attackPattern;
    bool _strikeFired;
    bool _strikeFxStarted;
    /// <summary>true の間は攻撃タイマー（Remaining）が時計の正。着弾後に false へ。</summary>
    bool _clockLockedToSwing;
    float _attackSpeedMul = 1f;
    float _breathTime;
    bool _enraged;
    bool _broken;
    bool _paused;
    float _displayScale = 1f;
    Vector2 _pivotIdle = new(0.5f, 0f);
    Vector2 _pivotAttack = new(0.5f, 0f);
    Vector2 _offsetIdle;
    Vector2 _offsetAttack;
    float _ehPixels = 320f;

    public Image MainImage => _image;
    public static bool UsesSpritePair(EnemyDefinition enemy) =>
      enemy != null && enemy.visual.graphicMode == EnemyGraphicMode.SpritePair;

    EnemyVisualPresetLibrary Library => _library != null ? _library : (_library = EnemyVisualPresetLibrary.Instance);

    public static EnemySpritePairPresenter Create(RectTransform enemyHitRoot, BattleVisualContext visuals)
    {
      var enemy = visuals?.Enemy;
      if (!UsesSpritePair(enemy))
        return null;

      var host = new GameObject("SpritePairPresenter");
      host.transform.SetParent(enemyHitRoot, false);
      var hostRt = host.AddComponent<RectTransform>();
      hostRt.anchorMin = Vector2.zero;
      hostRt.anchorMax = Vector2.one;
      hostRt.offsetMin = Vector2.zero;
      hostRt.offsetMax = Vector2.zero;

      var feet = new GameObject("FeetAnchor");
      feet.transform.SetParent(host.transform, false);
      var feetRt = feet.AddComponent<RectTransform>();
      feetRt.anchorMin = feetRt.anchorMax = new Vector2(0.5f, 0.08f);
      feetRt.pivot = new Vector2(0.5f, 0f);
      feetRt.sizeDelta = Vector2.zero;

      var body = new GameObject("Body");
      body.transform.SetParent(feet.transform, false);
      var bodyRt = body.AddComponent<RectTransform>();
      bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0.5f, 0f);
      bodyRt.pivot = new Vector2(0.5f, 0f);
      bodyRt.sizeDelta = new Vector2(360f, 360f);

      var imgGo = new GameObject("Sprite");
      imgGo.transform.SetParent(body.transform, false);
      var imgRt = imgGo.AddComponent<RectTransform>();
      imgRt.anchorMin = Vector2.zero;
      imgRt.anchorMax = Vector2.one;
      imgRt.offsetMin = Vector2.zero;
      imgRt.offsetMax = Vector2.zero;
      var image = imgGo.AddComponent<Image>();
      image.preserveAspect = true;
      image.raycastTarget = false;

      var presenter = host.AddComponent<EnemySpritePairPresenter>();
      presenter._body = bodyRt;
      presenter._feetAnchor = feetRt;
      presenter._image = image;
      presenter._library = EnemyVisualPresetLibrary.Instance;
      presenter._strikeFx = EnemySpritePairStrikeFx.Create(bodyRt);
      presenter._shake = host.AddComponent<EnemySpritePairShake>();
      presenter._shake.Configure(bodyRt, null);
      presenter.Bind(enemy);
      return presenter;
    }

    public void Bind(EnemyDefinition enemy)
    {
      _enemy = enemy;
      if (enemy == null || _image == null || _body == null)
        return;
      var v = enemy.visual;
      _displayScale = v.displayScale > 0f ? v.displayScale : 1f;
      _pivotIdle = v.feetPivotIdle;
      _pivotAttack = v.feetPivotAttack;
      _offsetIdle = v.poseOffsetIdle;
      _offsetAttack = v.poseOffsetAttack;
      _idleSprite = v.ResolveIdle(enemy);
      _attackSprite = v.attackIllustration != null ? v.attackIllustration : _idleSprite;
      _image.sprite = _idleSprite;
      ApplyPivot(_pivotIdle);
      _body.localScale = Vector3.one * _displayScale;
      RecalcEh();
      EnsureFx();
    }

    void EnsureFx()
    {
      _library ??= EnemyVisualPresetLibrary.Instance;
      if (_shake == null)
      {
        _shake = GetComponent<EnemySpritePairShake>();
        if (_shake == null)
          _shake = gameObject.AddComponent<EnemySpritePairShake>();
        if (_body != null)
          _shake.Configure(_body, null);
      }

      if (_strikeFx == null && _body != null)
        _strikeFx = EnemySpritePairStrikeFx.Create(_body);
    }

    void RecalcEh()
    {
      if (_image == null || _idleSprite == null)
      {
        _ehPixels = 320f * _displayScale;
        return;
      }

      var tr = _idleSprite.rect;
      var aspect = tr.width / Mathf.Max(1f, tr.height);
      var h = 360f * _displayScale;
      var w = h * aspect;
      if (w > 380f)
      {
        w = 380f;
        h = w / aspect;
      }

      if (_body != null)
        _body.sizeDelta = new Vector2(w, h);
      _ehPixels = h;
    }

    void ApplyPivot(Vector2 pivot)
    {
      if (_image == null)
        return;
      _image.rectTransform.pivot = new Vector2(pivot.x, pivot.y);
    }

    public void SetEnraged(bool enraged) => _enraged = enraged;

    public void SetBroken(bool broken) => _broken = broken;

    public void SetPaused(bool paused) => _paused = paused;

    public void SetAttackSpeedMul(float mul) =>
      _attackSpeedMul = mul > 0.01f ? mul : 1f;

    public void BeginAttack(EnemyAttackPattern pattern, float timeOffset = 0f)
    {
      if (_enemy == null || _image == null)
        return;
      EnsureFx();
      _mode = SpritePairPresenterMode.Attacking;
      _attackPattern = pattern;
      _modeTime = Mathf.Max(0f, timeOffset);
      _strikeFired = false;
      _strikeFxStarted = false;
      _clockLockedToSwing = true;
      EvaluateAttackFrame();
    }

    /// <summary>
    /// 攻撃タイマー連動。motionTime = ImpactLead - Remaining。
    /// Remaining=ImpactLead → 0、Remaining=0 → ImpactLead（着弾）。
    /// </summary>
    public void SetAttackMotionTime(float motionTime)
    {
      if (_mode != SpritePairPresenterMode.Attacking || !_clockLockedToSwing)
        return;
      var impact = CurrentTimeToImpact();
      // 着弾フレームは NotifySwingImpact まで予約（先走り防止）
      _modeTime = Mathf.Clamp(motionTime, 0f, Mathf.Max(0f, impact - 0.001f));
      EvaluateAttackFrame();
    }

    /// <summary>タイマ0着弾の瞬間。ポーズを着弾に合わせ斬撃を発火し、以降は復帰を自由進行。</summary>
    public void NotifySwingImpact()
    {
      if (_mode != SpritePairPresenterMode.Attacking)
        return;
      _modeTime = CurrentTimeToImpact();
      _clockLockedToSwing = false;
      EvaluateAttackFrame();
      if (!_strikeFired)
      {
        _strikeFired = true;
        var lib = Library;
        if (lib != null && _enemy != null)
        {
          var intensity = lib.ResolveIntensity(_enemy, _attackPattern);
          var mul = lib.IntensityMultiplier(intensity);
          var shakeMul = _enemy.visual.motionPreset == EnemyMotionPreset.CastMagic
                         || _enemy.visual.strikeEffect == EnemyStrikeEffectKind.Magic
            ? 0.6f
            : _enemy.visual.motionPreset == EnemyMotionPreset.SwingInPlace
              ? 1.2f
              : 1f;
          _shake?.Trigger(lib.shakeAmpMediumPx * mul * shakeMul);
        }
        if (!_strikeFxStarted)
          StartStrikeFxNow();
      }
    }

    float CurrentTimeToImpact()
    {
      var lib = Library;
      if (lib == null || _enemy == null)
        return 0.5f;
      return Mathf.Max(0.05f, lib.ResolveAttackTiming(_enemy, _attackPattern).TimeToImpact);
    }

    void EvaluateAttackFrame()
    {
      try
      {
        TickAttack();
      }
      catch (System.Exception ex)
      {
        Debug.LogWarning("EnemySpritePairPresenter attack eval failed: " + ex.Message);
        AbortAttackToIdle();
      }
    }

    public void PlayHitReaction()
    {
      if (_mode == SpritePairPresenterMode.Defeated)
        return;
      _mode = SpritePairPresenterMode.HitReact;
      _modeTime = 0f;
    }

    public void PlayDefeat()
    {
      _mode = SpritePairPresenterMode.Defeated;
      _modeTime = 0f;
    }

    void AbortAttackToIdle()
    {
      _mode = SpritePairPresenterMode.Idle;
      _modeTime = 0f;
      _strikeFired = true;
      _strikeFxStarted = true;
      _clockLockedToSwing = false;
      _strikeFx?.SetFrozen(false);
      if (_image != null && _idleSprite != null)
        _image.sprite = _idleSprite;
      ApplyPivot(_pivotIdle);
    }

    void Update()
    {
      if (_enemy == null || _image == null)
        return;

      // BattleController のポーズは timeScale を止めないので明示フラグを見る
      if (_paused)
        return;

      var dt = Time.deltaTime;
      if (dt <= 0f)
        return;

      try
      {
        if (_mode == SpritePairPresenterMode.Idle)
        {
          _modeTime += dt;
          TickIdle(dt);
        }
        else if (_mode == SpritePairPresenterMode.Attacking)
        {
          if (_clockLockedToSwing)
          {
            // タイマー正: フレーム間は攻撃速度倍率で補間。着弾は NotifySwingImpact まで進めない
            var impact = CurrentTimeToImpact();
            _modeTime = Mathf.Min(impact - 0.001f, _modeTime + dt * _attackSpeedMul);
            TickAttack();
          }
          else
          {
            // 着弾後の復帰・着地は自由進行
            _modeTime += dt;
            TickAttack();
          }
        }
        else if (_mode == SpritePairPresenterMode.HitReact)
        {
          _modeTime += dt;
          TickHit();
        }
        else
        {
          _modeTime += dt;
          TickDefeat();
        }
      }
      catch (System.Exception ex)
      {
        Debug.LogWarning("EnemySpritePairPresenter Update failed: " + ex.Message);
        AbortAttackToIdle();
      }
    }

    void TryStartStrikeFx(float timeToImpact)
    {
      if (_strikeFxStarted || _enemy == null || _strikeFx == null)
        return;
      var kind = _enemy.visual.strikeEffect;
      if (kind == EnemyStrikeEffectKind.None)
        return;

      var startOffset = 0f;
      var catalog = EnemyStrikeFxCatalog.Instance;
      var clip = catalog != null ? catalog.Get(kind) : null;
      if (clip != null)
        startOffset = clip.StartOffsetSec;

      if (_modeTime + 0.0001f >= timeToImpact + startOffset)
        StartStrikeFxNow();
    }

    void StartStrikeFxNow()
    {
      if (_strikeFxStarted || _strikeFx == null)
        return;
      _strikeFxStarted = true;
      _strikeFx.PlayStrike(_enemy, _attackPattern, _ehPixels);
    }

    void TickIdle(float dt)
    {
      _breathTime += dt;
      var lib = Library;
      if (lib == null || _body == null)
        return;
      var breath = lib.SampleBreath(_breathTime, _enraged);
      ApplyPose(breath, false);
      if (_broken)
        ApplyBreakOverlay();
    }

    void TickAttack()
    {
      var lib = Library;
      if (lib == null || _enemy == null || _body == null || _image == null)
      {
        AbortAttackToIdle();
        return;
      }

      EnsureFx();

      var timing = lib.ResolveAttackTiming(_enemy, _attackPattern);
      TryStartStrikeFx(timing.TimeToImpact);

      var pose = lib.SampleAttackPose(_enemy, _attackPattern, _modeTime, _strikeFired);
      if (pose.StrikeThisFrame && !_strikeFired)
      {
        _strikeFired = true;
        var intensity = lib.ResolveIntensity(_enemy, _attackPattern);
        var mul = lib.IntensityMultiplier(intensity);
        var shakeMul = _enemy.visual.motionPreset == EnemyMotionPreset.CastMagic
                       || _enemy.visual.strikeEffect == EnemyStrikeEffectKind.Magic
          ? 0.6f
          : _enemy.visual.motionPreset == EnemyMotionPreset.SwingInPlace
            ? 1.2f
            : 1f;
        _shake?.Trigger(lib.shakeAmpMediumPx * mul * shakeMul);
        if (!_strikeFxStarted)
          StartStrikeFxNow();
      }

      _strikeFx?.SetFrozen(pose.FreezeStrikeFx);
      ApplyPose(pose, pose.UseAttackSprite);
      var total = lib.ResolveTotalAttackDuration(_enemy, _attackPattern);
      if (_modeTime >= total)
        AbortAttackToIdle();
    }

    void TickHit()
    {
      var lib = Library;
      if (lib == null || _enemy == null)
      {
        AbortAttackToIdle();
        return;
      }

      var flash = _enemy.visual.hitFlashColor;
      var intensity = _enemy.visual.effectIntensity;
      var pose = lib.SamplePlayerHit(_modeTime, flash, intensity);
      ApplyPose(pose, false);
      if (_modeTime > 0.35f)
        AbortAttackToIdle();
    }

    void TickDefeat()
    {
      var lib = Library;
      if (lib == null || _enemy == null || _image == null)
        return;
      var pose = lib.SampleDefeat(_enemy.visual.defeatPreset, _modeTime);
      ApplyPose(pose, false);
      _image.color = new Color(_image.color.r, _image.color.g, _image.color.b, pose.Alpha);
    }

    void ApplyPose(EnemySpritePairPose pose, bool attackSprite)
    {
      if (_image == null || _body == null)
        return;

      _image.sprite = attackSprite && _attackSprite != null ? _attackSprite : _idleSprite;
      ApplyPivot(attackSprite ? _pivotAttack : _pivotIdle);
      var regOffset = attackSprite ? _offsetAttack : _offsetIdle;
      var offsetPx = new Vector2(
        (pose.OffsetEh.x + regOffset.x) * _ehPixels,
        (pose.OffsetEh.y + regOffset.y) * _ehPixels);
      _body.anchoredPosition = offsetPx;
      var tilt = _broken && Library != null ? Library.breakTiltDeg : 0f;
      // skew は足元シアー。UI Image では rot 近似（仕様 §2.4）
      var skewApproxDeg = 0.5f * pose.SkewX * Mathf.Rad2Deg;
      _body.localRotation = Quaternion.Euler(0f, 0f, pose.RotationDeg + skewApproxDeg + tilt);
      _body.localScale = new Vector3(
        _displayScale * pose.ScaleX,
        _displayScale * pose.ScaleY,
        _displayScale);
      var c = pose.Tint;
      c.a = pose.Alpha > 0f ? pose.Alpha : 1f;
      _image.color = c;
    }

    void ApplyBreakOverlay()
    {
      if (_image == null || Library == null)
        return;
      var c = _image.color;
      c *= Library.breakDarken;
      _image.color = c;
    }

    public float ResolveImpactLead(EnemyAttackPattern pattern)
    {
      var lib = Library;
      if (lib == null || _enemy == null)
        return EnemyLungeMotion.ImpactTime;
      return Mathf.Max(0.05f, lib.ResolveAttackTiming(_enemy, pattern).TimeToImpact);
    }
  }
}