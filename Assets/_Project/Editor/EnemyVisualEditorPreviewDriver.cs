#if UNITY_EDITOR
using TypingRoguelike.Battle.Visual;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    public sealed class EnemyVisualEditorPreviewDriver
    {
        readonly EnemyVisualPresetLibrary _library = EnemyVisualPresetLibrary.Instance;

        PreviewPhase _phase = PreviewPhase.Idle;
        double _phaseStart;
        float _time;
        /// <summary>プレビュー枠が小さいので、モーション変位を強調して見やすくする（戦闘は等倍）。</summary>
        const float PreviewMotionBoost = 2.75f;

        public enum PreviewPhase
        {
            Idle,
            Attack,
            Hit,
            Defeat,
            Align
        }

        public PreviewPhase Phase => _phase;
        public float Time => _time;

        public void PlayIdle()
        {
            _phase = PreviewPhase.Idle;
            _time = 0f;
            EditorApplication.update -= Tick;
            RepaintPreviewHosts();
        }

        public void PlayAttack()
        {
            _phase = PreviewPhase.Attack;
            _phaseStart = EditorApplication.timeSinceStartup;
            _time = 0f;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            RepaintPreviewHosts();
        }

        public void PlayHit()
        {
            _phase = PreviewPhase.Hit;
            _phaseStart = EditorApplication.timeSinceStartup;
            _time = 0f;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            RepaintPreviewHosts();
        }

        public void PlayDefeat()
        {
            _phase = PreviewPhase.Defeat;
            _phaseStart = EditorApplication.timeSinceStartup;
            _time = 0f;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            RepaintPreviewHosts();
        }

        public void PlayAlign()
        {
            _phase = PreviewPhase.Align;
            _time = 0f;
            EditorApplication.update -= Tick;
            RepaintPreviewHosts();
        }

        public void OnDisable() => EditorApplication.update -= Tick;

        static void RepaintPreviewHosts()
        {
            EnemyDefinitionEditorWindow.RepaintAllEnemyWindows();
            EnemyVisualPreviewWindow.RepaintAll();
        }

        void Tick()
        {
            _time = (float)(EditorApplication.timeSinceStartup - _phaseStart);
            RepaintPreviewHosts();

            if (_phase == PreviewPhase.Hit && _time > 0.45f)
                PlayIdle();
            else if (_phase == PreviewPhase.Defeat && _time > 1.2f)
                PlayIdle();
            else if (_phase == PreviewPhase.Attack)
            {
              // プレビューは Lunge 既定長＋余裕。専用モーションは Sample 側が長いので余裕を多めに
              var maxDur = Mathf.Max(
                EnemyLungeMotion.TotalAttackDuration(EnemyLungeMotion.DefaultWindup),
                EnemyRushMotion.TotalAttackDuration(EnemyRushMotion.DefaultWindup),
                EnemySwingMotion.TotalAttackDuration(EnemySwingMotion.DefaultWindup),
                EnemyCastMotion.TotalAttackDuration(EnemyCastMotion.DefaultWindup));
              if (_time > maxDur + 0.35f)
                PlayIdle();
            }
        }

        static EnemyAttackPattern PreviewPattern => new()
        {
            damage = 1f,
            intervalSeconds = 2f
        };

        static Vector2 SafePivot(Vector2 pivot)
        {
            // 誤クリックで頭付近(0.7+)になると絵が枠外へ消える。プレビュー描画時は下端寄りにクランプ。
            return new Vector2(
                Mathf.Clamp(pivot.x, 0f, 1f),
                Mathf.Clamp(pivot.y, 0f, 0.25f));
        }

        static EnemySpritePairPose Boost(EnemySpritePairPose pose)
        {
            pose.OffsetEh *= PreviewMotionBoost;
            pose.RotationDeg *= PreviewMotionBoost;
            pose.SkewX *= PreviewMotionBoost;
            // スケール変化も少し強調
            pose.ScaleX = 1f + (pose.ScaleX - 1f) * 1.35f;
            pose.ScaleY = 1f + (pose.ScaleY - 1f) * 1.35f;
            return pose;
        }

        public void DrawPreview(
            EnemyDefinition enemy,
            Rect rect,
            float displayScale,
            bool showAlignGhost,
            bool showStrikePoseEdit = false)
        {
            if (enemy == null)
                return;
            var idle = enemy.visual.ResolveIdle(enemy);
            var attack = enemy.visual.attackIllustration;
            if (idle == null)
                return;

            if (_phase is PreviewPhase.Attack or PreviewPhase.Hit or PreviewPhase.Defeat)
                _time = (float)(EditorApplication.timeSinceStartup - _phaseStart);

            var eh = Mathf.Max(8f, (rect.height - 24f) * displayScale);
            var motionName = enemy.visual.motionPreset.ToString();
            var status = "";
            var strikeNote = "";

            // 攻撃再生中は位置合わせゴーストを絶対に混ぜない
            if (_phase == PreviewPhase.Attack)
            {
                var timing = _library.ResolveAttackTiming(enemy, PreviewPattern);
                var pose = Boost(_library.SampleAttackPose(enemy, PreviewPattern, _time, _time >= timing.TimeToImpact));
                var wantAttack = pose.UseAttackSprite || _time >= timing.WindupSeconds - 0.001f;
                var sprite = wantAttack && attack != null ? attack : idle;
                var pivot = SafePivot(wantAttack ? enemy.visual.feetPivotAttack : enemy.visual.feetPivotIdle);
                var offset = wantAttack ? enemy.visual.poseOffsetAttack : enemy.visual.poseOffsetIdle;
                // 異常オフセットもプレビューでは抑える（データは変えない）
                offset = new Vector2(offset.x, Mathf.Clamp(offset.y, -0.2f, 0.25f));

                EnemyDefinitionEditorVisualHelpers.DrawSpritePose(sprite, rect, pivot, eh, pose, offset, Color.white);
                // 戦闘同様、斬撃は足元基準＋敵ポーズ移動に追従
                var follow = pose.OffsetEh + offset;
                strikeNote = DrawStrikeFxPreview(enemy, rect, eh, timing.TimeToImpact, animated: true, followOffsetEh: follow);
                status = wantAttack && attack != null
                    ? $"【攻撃再生】{motionName} 攻撃絵 t={_time:0.00}s"
                    : wantAttack
                        ? $"【攻撃再生】{motionName} 攻撃絵未設定 t={_time:0.00}s"
                        : $"【攻撃再生】{motionName} ため t={_time:0.00}/{timing.WindupSeconds:0.00}s";
            }
            else if (_phase == PreviewPhase.Align)
            {
                EnemyDefinitionEditorVisualHelpers.DrawSpritePose(
                    idle, rect, SafePivot(enemy.visual.feetPivotIdle), eh, EnemySpritePairPose.Idle,
                    enemy.visual.poseOffsetIdle, new Color(1f, 1f, 1f, 0.35f));
                if (attack != null)
                {
                    EnemyDefinitionEditorVisualHelpers.DrawSpritePose(
                        attack, rect, SafePivot(enemy.visual.feetPivotAttack), eh, EnemySpritePairPose.Idle,
                        enemy.visual.poseOffsetAttack, new Color(1f, 0.85f, 0.55f, 0.95f));
                }
                status = "【位置合わせ】薄＝立ち / 濃＝攻撃";
            }
            else if (_phase == PreviewPhase.Hit)
            {
                var pose = Boost(_library.SamplePlayerHit(_time, enemy.visual.hitFlashColor, enemy.visual.effectIntensity));
                EnemyDefinitionEditorVisualHelpers.DrawSpritePose(
                    idle, rect, SafePivot(enemy.visual.feetPivotIdle), eh, pose,
                    enemy.visual.poseOffsetIdle, Color.white);
                status = "【被弾】";
            }
            else if (_phase == PreviewPhase.Defeat)
            {
                var pose = _library.SampleDefeat(enemy.visual.defeatPreset, _time);
                EnemyDefinitionEditorVisualHelpers.DrawSpritePose(
                    idle, rect, SafePivot(enemy.visual.feetPivotIdle), eh, pose,
                    enemy.visual.poseOffsetIdle, Color.white);
                status = "【撃破】";
            }
            else
            {
                // Idle
                if (showAlignGhost && attack != null)
                {
                    EnemyDefinitionEditorVisualHelpers.DrawSpritePose(
                        idle, rect, SafePivot(enemy.visual.feetPivotIdle), eh, EnemySpritePairPose.Idle,
                        enemy.visual.poseOffsetIdle, new Color(1f, 1f, 1f, 0.35f));
                    EnemyDefinitionEditorVisualHelpers.DrawSpritePose(
                        attack, rect, SafePivot(enemy.visual.feetPivotAttack), eh, EnemySpritePairPose.Idle,
                        enemy.visual.poseOffsetAttack, new Color(1f, 0.85f, 0.55f, 0.95f));
                    status = "【待機】ゴースト表示中。攻撃を押すとモーション再生";
                }
                else
                {
                    var pose = _library.SampleBreath((float)EditorApplication.timeSinceStartup, false);
                    EnemyDefinitionEditorVisualHelpers.DrawSpritePose(
                        idle, rect, SafePivot(enemy.visual.feetPivotIdle), eh, pose,
                        enemy.visual.poseOffsetIdle, Color.white);
                    status = $"【待機】呼吸のみ / 設定モーション={motionName}";
                }

                if (showStrikePoseEdit)
                {
                    var follow = enemy.visual.poseOffsetIdle;
                    strikeNote = DrawStrikeFxPreview(
                        enemy, rect, eh, timeToImpact: 0f, animated: false, followOffsetEh: follow);
                }
            }

            if (!string.IsNullOrEmpty(strikeNote))
                status = string.IsNullOrEmpty(status) ? strikeNote : status + "  " + strikeNote;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 20f), new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f), status, EditorStyles.whiteMiniLabel);
        }

        /// <returns>ステータス用短文。描画しなければ空。</returns>
        string DrawStrikeFxPreview(
            EnemyDefinition enemy,
            Rect rect,
            float eh,
            float timeToImpact,
            bool animated,
            Vector2 followOffsetEh)
        {
            var kind = enemy.visual.strikeEffect;
            if (kind == EnemyStrikeEffectKind.None)
                return "";

            var catalog = EnemyStrikeFxCatalog.Instance;
            var clip = catalog != null ? catalog.Get(kind) : null;
            if (clip == null || clip.FrameCount <= 0)
                return "【斬撃】カタログ未ビルド";

            // プレビューはシェーダー無しで色付きフレーム優先
            var frames = clip.ResolveFrames(enemy.visual.effectPalette, preferMask: false);
            if (frames == null || frames.Length == 0)
                frames = clip.maskFrames;
            if (frames == null || frames.Length == 0)
                return "【斬撃】フレーム無し";

            var fps = clip.fps > 0.01f ? clip.fps : 30f;
            int frameIndex;
            if (animated)
            {
                var start = timeToImpact + clip.StartOffsetSec;
                if (_time < start)
                    return "";
                frameIndex = Mathf.Clamp(Mathf.FloorToInt((_time - start) * fps), 0, frames.Length - 1);
                if (_time - start > frames.Length / fps)
                    return "";
            }
            else
            {
                frameIndex = Mathf.Clamp(clip.impactFrame, 0, frames.Length - 1);
            }

            var sprite = frames[frameIndex];
            if (sprite == null)
                return "";

            var intensity = _library != null
                ? _library.IntensityMultiplier(enemy.visual.effectIntensity)
                : 1f;
            if (enemy.visual.strikeScaleOverride > 0f)
                intensity = enemy.visual.strikeScaleOverride;

            var size = clip.displaySizeEh * eh * Mathf.Max(0.1f, intensity);
            var centerEh = clip.centerOffsetEh + enemy.visual.strikeOffsetEh + followOffsetEh;
            EnemyDefinitionEditorVisualHelpers.DrawCenteredSprite(
                sprite, rect, eh, centerEh, size, Color.white);
            EnemyDefinitionEditorVisualHelpers.DrawCrosshair(rect, eh, centerEh);

            return animated
                ? $"【斬撃】{kind} f={frameIndex}"
                : $"【斬撃位置】{kind} 着弾コマ  offset=({enemy.visual.strikeOffsetEh.x:0.00},{enemy.visual.strikeOffsetEh.y:0.00})";
        }
    }

    internal static class EnemyDefinitionEditorVisualHelpers
    {
        public static void DrawSpritePose(
            Sprite sprite,
            Rect rect,
            Vector2 feetPivot,
            float h,
            EnemySpritePairPose pose,
            Vector2 poseOffsetEh,
            Color tint)
        {
            if (sprite?.texture == null || rect.width < 4f || rect.height < 4f)
                return;
            h = Mathf.Max(8f, h);
            var tr = sprite.textureRect;
            var tex = sprite.texture;
            var aspect = tr.width / Mathf.Max(1f, tr.height);
            var drawH = Mathf.Max(8f, h * Mathf.Abs(pose.ScaleY));
            var drawW = Mathf.Max(8f, drawH * aspect * Mathf.Abs(pose.ScaleX));

            var feetX = rect.xMin + rect.width * 0.5f + (pose.OffsetEh.x + poseOffsetEh.x) * h;
            var feetY = rect.yMax - 18f - (pose.OffsetEh.y + poseOffsetEh.y) * h;
            var x = feetX - drawW * feetPivot.x;
            var y = feetY - drawH * (1f - feetPivot.y);
            var draw = new Rect(x, y, drawW, drawH);

            var prev = GUI.matrix;
            var rot = pose.RotationDeg + 0.5f * pose.SkewX * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(rot, new Vector2(feetX, feetY));
            var gc = GUI.color;
            var alpha = (pose.Alpha > 0f ? pose.Alpha : 1f) * tint.a;
            GUI.color = new Color(tint.r * pose.Tint.r, tint.g * pose.Tint.g, tint.b * pose.Tint.b, alpha);
            GUI.DrawTextureWithTexCoords(draw, tex, new Rect(
                tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            GUI.color = gc;
            GUI.matrix = prev;
        }

        public static void DrawCenteredSprite(
            Sprite sprite,
            Rect rect,
            float eh,
            Vector2 centerOffsetEh,
            float sizePx,
            Color tint)
        {
            if (sprite?.texture == null || rect.width < 4f)
                return;
            var feetX = rect.xMin + rect.width * 0.5f;
            var feetY = rect.yMax - 18f;
            var cx = feetX + centerOffsetEh.x * eh;
            var cy = feetY - centerOffsetEh.y * eh;
            var draw = new Rect(cx - sizePx * 0.5f, cy - sizePx * 0.5f, sizePx, sizePx);
            var tr = sprite.textureRect;
            var tex = sprite.texture;
            var gc = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(draw, tex, new Rect(
                tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            GUI.color = gc;
        }

        public static void DrawCrosshair(Rect rect, float eh, Vector2 centerOffsetEh)
        {
            var feetX = rect.xMin + rect.width * 0.5f;
            var feetY = rect.yMax - 18f;
            var cx = feetX + centerOffsetEh.x * eh;
            var cy = feetY - centerOffsetEh.y * eh;
            var col = new Color(1f, 0.85f, 0.2f, 0.85f);
            EditorGUI.DrawRect(new Rect(cx - 8f, cy - 1f, 16f, 2f), col);
            EditorGUI.DrawRect(new Rect(cx - 1f, cy - 8f, 2f, 16f), col);
        }
    }
}
#endif