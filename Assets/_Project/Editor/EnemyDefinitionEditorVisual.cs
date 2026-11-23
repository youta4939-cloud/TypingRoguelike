#if UNITY_EDITOR
using TypingRoguelike.Battle.Visual;
using TypingRoguelike.Data;
using UnityEditor;
using UnityEngine;

namespace TypingRoguelike.EditorTools
{
    internal sealed class EnemyDefinitionEditorVisual
    {
        const float PreviewHeightDefault = 140f;
        enum PreviewEditMode
        {
            None = 0,
            PivotIdle = 1,
            PivotAttack = 2,
            OffsetIdle = 3,
            OffsetAttack = 4,
            OffsetStrike = 5
        }

        PreviewEditMode _editMode = PreviewEditMode.None;
        bool _alignGhost;
        bool _advancedFoldout;
        bool _presetFoldout = true;
        bool _alignFoldout = true;
        EnemyVisualEditorPreviewDriver _preview;

        static readonly string[] GraphicModeLabels = { "パーツリグ", "立ち絵2枚", "絵文字のみ" };
        static readonly string[] MotionLabels = { "なし", "待機", "飛びかかり", "突進", "その場振り", "魔法" };
        static readonly string[] StrikeLabels = { "なし", "斬撃", "突き", "噛み", "爪", "魔法" };
        static readonly string[] PaletteLabels = { "無彩色", "血赤", "毒緑", "呪紫", "氷青", "聖金" };
        static readonly string[] IntensityLabels = { "小", "中", "大" };
        static readonly string[] DefeatLabels = { "フェード", "灰", "崩れる", "霧" };

        void EnsurePreview() => _preview ??= new EnemyVisualEditorPreviewDriver();

        public bool ShowsPinnedPreview(EnemyDefinition e) =>
            e != null && e.visual.graphicMode == EnemyGraphicMode.SpritePair;

        /// <summary>スクロール内：画像登録とプリセットのみ（プレビューは別）。開閉を変えたら true。</summary>
        public bool DrawForm(EnemyDefinition e, ref bool previewExpanded)
        {
            if (e == null)
                return false;
            EnsurePreview();
            EnsureVisual(e);
            var changed = false;

            EditorGUILayout.LabelField("④ 画像・モーション", EditorStyles.boldLabel);

            e.visual.graphicMode = (EnemyGraphicMode)EditorGUILayout.Popup(
                "表示モード",
                (int)e.visual.graphicMode,
                GraphicModeLabels);

            if (e.visual.graphicMode == EnemyGraphicMode.PartRig)
            {
                e.battleVisual = (EnemyBattleVisualKind)EditorGUILayout.EnumPopup("リグ種別", e.battleVisual);
                EditorGUILayout.LabelField("パーツアニメは従来どおり。立ち絵欄は出ません。", EditorStyles.miniLabel);
                return false;
            }

            if (e.visual.graphicMode == EnemyGraphicMode.EmojiOnly)
            {
                e.emojiNormal = EditorGUILayout.TextField("絵文字(通常)", e.emojiNormal);
                e.emojiEnraged = EditorGUILayout.TextField("絵文字(狂暴)", e.emojiEnraged);
                e.emojiDefeated = EditorGUILayout.TextField("絵文字(撃破)", e.emojiDefeated);
                return false;
            }

            e.battleVisual = EnemyBattleVisualKind.StaticIllustration;
            var idleSprite = e.visual.idleIllustration != null ? e.visual.idleIllustration : e.battleIllustration;
            idleSprite = (Sprite)EditorGUILayout.ObjectField("立ち絵（待機）", idleSprite, typeof(Sprite), false);
            e.visual.idleIllustration = idleSprite;
            e.battleIllustration = idleSprite;
            e.visual.attackIllustration = (Sprite)EditorGUILayout.ObjectField(
                "攻撃絵",
                e.visual.attackIllustration,
                typeof(Sprite),
                false);
            e.visual.displayScale = EditorGUILayout.Slider("表示倍率", e.visual.displayScale, 0.35f, 2.5f);

            _alignFoldout = EditorGUILayout.Foldout(_alignFoldout, "位置合わせ（足元・表示オフセット）", true);
            if (_alignFoldout)
            {
                if (e.visual.feetPivotAttack.y > 0.35f || Mathf.Abs(e.visual.poseOffsetAttack.y) > 0.35f)
                {
                    EditorGUILayout.HelpBox(
                        "攻撃の足元ピボット／オフセットが大きすぎます（いま攻撃Yピボット≈" +
                        $"{e.visual.feetPivotAttack.y:0.00}, オフセットY≈{e.visual.poseOffsetAttack.y:0.00}）。\n" +
                        "これだと飛びかかりが壊れて見えます。「攻撃の位置を初期化」を押してから、" +
                        "プレビュー編集は『位置ドラッグ：攻撃』で微調整してください（足元クリックは画像の下の方）。",
                        MessageType.Warning);
                }

                e.visual.feetPivotIdle = EditorGUILayout.Vector2Field("足元ピボット（立ち）", e.visual.feetPivotIdle);
                e.visual.feetPivotAttack = EditorGUILayout.Vector2Field("足元ピボット（攻撃）", e.visual.feetPivotAttack);
                e.visual.poseOffsetIdle = EditorGUILayout.Vector2Field("表示オフセット（立ち）Eh", e.visual.poseOffsetIdle);
                e.visual.poseOffsetAttack = EditorGUILayout.Vector2Field("表示オフセット（攻撃）Eh", e.visual.poseOffsetAttack);
                e.visual.strikeOffsetEh = EditorGUILayout.Vector2Field("斬撃エフェクト位置 Eh", e.visual.strikeOffsetEh);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("攻撃の位置を初期化", GUILayout.Width(140)))
                {
                    e.visual.feetPivotAttack = new Vector2(0.5f, 0f);
                    e.visual.poseOffsetAttack = Vector2.zero;
                    EditorUtility.SetDirty(e);
                }
                if (GUILayout.Button("立ちの位置を初期化", GUILayout.Width(140)))
                {
                    e.visual.feetPivotIdle = new Vector2(0.5f, 0f);
                    e.visual.poseOffsetIdle = Vector2.zero;
                    EditorUtility.SetDirty(e);
                }
                if (GUILayout.Button("斬撃位置を初期化", GUILayout.Width(140)))
                {
                    e.visual.strikeOffsetEh = Vector2.zero;
                    EditorUtility.SetDirty(e);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(
                    "足元ピボットYはだいたい 0〜0.1。斬撃位置はプレビュー編集『斬撃ドラッグ』でも調整可（クリップ既定＋この値）。",
                    EditorStyles.wordWrappedMiniLabel);
            }

            DrawAssetWarnings(e);

            _presetFoldout = EditorGUILayout.Foldout(_presetFoldout, "演出プリセット（モーション・斬撃・色）", true);
            if (_presetFoldout)
                DrawPresetFields(e);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            var openWinBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
            if (GUILayout.Button("別窓でプレビュー（戦闘風）", GUILayout.Height(28)))
            {
                EnemyVisualPreviewWindow.Open(e);
                changed = true;
            }
            GUI.backgroundColor = openWinBg;
            var openLabel = previewExpanded ? "下部プレビューを閉じる" : "下部にも簡易プレビュー";
            if (GUILayout.Button(openLabel, GUILayout.Height(28)))
            {
                previewExpanded = !previewExpanded;
                changed = true;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "推奨:「別窓でプレビュー」＝戦闘UI風の大きい枠。下部簡易プレビューも従来どおり使えます。",
                MessageType.Info);
            return changed;
        }

        /// <summary>スクロール外の下部固定プレビュー。canvasHeight は描画領域のみ。</summary>
        public void DrawPinnedPreview(EnemyDefinition e, float canvasHeight = PreviewHeightDefault)
        {
            if (!ShowsPinnedPreview(e))
                return;

            EnsurePreview();
            EnsureVisual(e);
            canvasHeight = Mathf.Max(80f, canvasHeight);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawPreviewControls(e);

            var playHeight = _preview.Phase == EnemyVisualEditorPreviewDriver.PreviewPhase.Attack
                ? Mathf.Max(canvasHeight, 220f)
                : canvasHeight;
            var rect = GUILayoutUtility.GetRect(1f, playHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.11f, 0.13f));
            DrawPreviewCanvas(e, rect);
            EditorGUILayout.EndVertical();
        }

        /// <summary>再生ボタン・編集モードなど（別窓／下部共通）。</summary>
        public void DrawPreviewControls(EnemyDefinition e)
        {
            if (e == null)
                return;
            EnsurePreview();
            EnsureVisual(e);

            if (e.visual.feetPivotAttack.y > 0.35f)
            {
                EditorGUILayout.HelpBox(
                    $"攻撃の足元ピボットY={e.visual.feetPivotAttack.y:0.00} が高すぎて、攻撃絵が枠外に消えます。「攻撃の位置を初期化」を押してください。",
                    MessageType.Error);
                if (GUILayout.Button("今すぐ攻撃の位置を初期化", GUILayout.Height(26)))
                {
                    e.visual.feetPivotAttack = new Vector2(0.5f, 0f);
                    e.visual.poseOffsetAttack = Vector2.zero;
                    EditorUtility.SetDirty(e);
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("待機", GUILayout.Height(26)))
                _preview.PlayIdle();
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.35f);
            if (GUILayout.Button("▶ 攻撃モーション再生", GUILayout.Height(26)))
            {
                _editMode = PreviewEditMode.None;
                _alignGhost = false;
                if (e.visual.feetPivotAttack.y > 0.35f)
                {
                    e.visual.feetPivotAttack = new Vector2(0.5f, 0f);
                    e.visual.poseOffsetAttack = Vector2.zero;
                    EditorUtility.SetDirty(e);
                }
                _preview.PlayAttack();
            }
            GUI.backgroundColor = oldBg;
            if (GUILayout.Button("被弾", GUILayout.Height(26)))
                _preview.PlayHit();
            if (GUILayout.Button("撃破", GUILayout.Height(26)))
                _preview.PlayDefeat();
            if (GUILayout.Button("位置合わせ", GUILayout.Height(26)))
                _preview.PlayAlign();
            EditorGUILayout.EndHorizontal();

            _editMode = (PreviewEditMode)EditorGUILayout.Popup(
                "プレビュー編集",
                (int)_editMode,
                new[]
                {
                    "なし（再生用）",
                    "足元クリック：立ち",
                    "足元クリック：攻撃",
                    "位置ドラッグ：立ち",
                    "位置ドラッグ：攻撃",
                    "斬撃ドラッグ（位置）"
                });
            _alignGhost = GUILayout.Toggle(_alignGhost, "待機中に立ち＋攻撃を重ねる（攻撃再生中は無効）");
            if (_editMode == PreviewEditMode.OffsetStrike)
            {
                EditorGUILayout.HelpBox(
                    "斬撃ドラッグ中は着弾コマを常時表示します。ドラッグで位置を動かし、攻撃再生でも確認できます。",
                    MessageType.Info);
            }
        }

        /// <summary>指定矩形に足元線＋スプライト＋ドラッグ編集を描画（別窓／下部共通）。</summary>
        public void DrawPreviewCanvas(EnemyDefinition e, Rect rect)
        {
            if (e == null || rect.width < 8f || rect.height < 8f)
                return;
            EnsurePreview();
            EnsureVisual(e);
            DrawFeetLine(rect);

            if (e.visual.ResolveIdle(e) != null)
            {
                var showStrikePose = _editMode == PreviewEditMode.OffsetStrike;
                _preview.DrawPreview(e, rect, e.visual.displayScale, _alignGhost, showStrikePose);
                var allowDragDuringAttack = _editMode == PreviewEditMode.OffsetStrike;
                if (_preview.Phase != EnemyVisualEditorPreviewDriver.PreviewPhase.Attack || allowDragDuringAttack)
                    HandlePreviewDrag(e, rect, Event.current);
            }
            else
                GUI.Label(rect, "立ち絵を登録するとプレビュー表示", EditorStyles.centeredGreyMiniLabel);
        }

        void HandlePreviewDrag(EnemyDefinition e, Rect rect, Event ev)
        {
            if (_editMode == PreviewEditMode.None || !rect.Contains(ev.mousePosition))
                return;

            var eh = Mathf.Max(8f, (rect.height - 24f) * e.visual.displayScale);

            if (_editMode is PreviewEditMode.PivotIdle or PreviewEditMode.PivotAttack)
            {
                if (ev.type != EventType.MouseDown || ev.button != 0)
                    return;
                var local = ev.mousePosition - rect.position;
                var nx = Mathf.Clamp01(local.x / rect.width);
                var ny = Mathf.Clamp01(1f - local.y / rect.height);
                if (_editMode == PreviewEditMode.PivotAttack)
                    e.visual.feetPivotAttack = new Vector2(nx, ny);
                else
                    e.visual.feetPivotIdle = new Vector2(nx, ny);
                EditorUtility.SetDirty(e);
                ev.Use();
                return;
            }

            if (_editMode is not (PreviewEditMode.OffsetIdle or PreviewEditMode.OffsetAttack or PreviewEditMode.OffsetStrike))
                return;
            if (ev.type != EventType.MouseDrag || ev.button != 0)
                return;

            // 画面上へドラッグ = ＋Y（Eh）
            var delta = new Vector2(ev.delta.x / eh, -ev.delta.y / eh);
            if (_editMode == PreviewEditMode.OffsetAttack)
                e.visual.poseOffsetAttack += delta;
            else if (_editMode == PreviewEditMode.OffsetStrike)
                e.visual.strikeOffsetEh += delta;
            else
                e.visual.poseOffsetIdle += delta;
            EditorUtility.SetDirty(e);
            ev.Use();
            RepaintEnemyWindows();
        }

        static void RepaintEnemyWindows()
        {
            EnemyDefinitionEditorWindow.RepaintAllEnemyWindows();
            EnemyVisualPreviewWindow.RepaintAll();
        }

        void EnsureVisual(EnemyDefinition e)
        {
            if (e.visual.displayScale <= 0f)
                e.visual = EnemyVisualRegistration.Default;
            if (e.visual.idleIllustration == null && e.battleIllustration != null)
                e.visual.idleIllustration = e.battleIllustration;
        }

        static void DrawAssetWarnings(EnemyDefinition e)
        {
            if (e.visual.graphicMode != EnemyGraphicMode.SpritePair)
                return;
            var idle = e.visual.ResolveIdle(e);
            var attack = e.visual.attackIllustration;
            if (attack == null)
                EditorGUILayout.HelpBox("攻撃絵が未設定です（戦闘では立ち絵で代替）。", MessageType.Warning);
            if (idle != null && attack != null && idle.texture != null && attack.texture != null)
            {
                var i = idle.texture;
                var a = attack.texture;
                if (i.width != a.width || i.height != a.height)
                {
                    EditorGUILayout.HelpBox(
                        $"立ち絵と攻撃絵の解像度が異なります（{i.width}×{i.height} vs {a.width}×{a.height}）。",
                        MessageType.Warning);
                }
            }
        }

        void DrawPresetFields(EnemyDefinition e)
        {
            EditorGUI.indentLevel++;
            e.visual.motionPreset = (EnemyMotionPreset)EditorGUILayout.Popup(
                "攻撃モーション",
                (int)e.visual.motionPreset,
                MotionLabels);
            if (e.visual.motionPreset == EnemyMotionPreset.Idle || e.visual.motionPreset == EnemyMotionPreset.None)
            {
                EditorGUILayout.HelpBox(
                    "「なし／待機」だと攻撃はほぼ動きません。飛びかかり・突進などを選んでください。",
                    MessageType.Warning);
            }
            e.visual.strikeEffect = (EnemyStrikeEffectKind)EditorGUILayout.Popup(
                "斬撃エフェクト",
                (int)e.visual.strikeEffect,
                StrikeLabels);
            e.visual.effectPalette = (EnemyEffectPalette)EditorGUILayout.Popup(
                "エフェクト色",
                (int)e.visual.effectPalette,
                PaletteLabels);
            e.visual.effectIntensity = (EnemyEffectIntensity)EditorGUILayout.Popup(
                "強さ",
                (int)e.visual.effectIntensity,
                IntensityLabels);
            e.visual.defeatPreset = (EnemyDefeatPreset)EditorGUILayout.Popup(
                "撃破演出",
                (int)e.visual.defeatPreset,
                DefeatLabels);
            e.visual.hitFlashColor = EditorGUILayout.ColorField("被弾フラッシュ色", e.visual.hitFlashColor);

            _advancedFoldout = EditorGUILayout.Foldout(_advancedFoldout, "詳細上書き（0=ライブラリ既定）", true);
            if (_advancedFoldout)
            {
                e.visual.windupSecondsOverride = EditorGUILayout.FloatField("ため（秒）上書き", e.visual.windupSecondsOverride);
                e.visual.activeSecondsOverride = EditorGUILayout.FloatField("発動（秒）上書き", e.visual.activeSecondsOverride);
                e.visual.recoverySecondsOverride = EditorGUILayout.FloatField("戻り（秒）上書き", e.visual.recoverySecondsOverride);
                e.visual.strikeScaleOverride = EditorGUILayout.FloatField("エフェクト倍率上書き", e.visual.strikeScaleOverride);
            }

            var lib = EnemyVisualPresetLibrary.Instance;
            var probe = new EnemyAttackPattern { damage = 1f, intervalSeconds = 2f };
            var timing = lib.ResolveAttackTiming(e, probe);
            EditorGUILayout.LabelField(
                $"着弾まで参考: {timing.TimeToImpact:0.##}s",
                EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        public void OnDisable() => _preview?.OnDisable();

        static void DrawFeetLine(Rect rect)
        {
            var y = rect.yMax - 18f;
            EditorGUI.DrawRect(new Rect(rect.xMin + 8f, y, rect.width - 16f, 1f), new Color(0.55f, 0.4f, 0.35f, 0.5f));
        }

        public static void DrawListThumbnail(EnemyDefinition e, float size)
        {
            var sprite = e != null ? e.visual.ResolveIdle(e) : null;
            var r = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(r, new Color(0.18f, 0.17f, 0.2f));
            if (sprite == null || sprite.texture == null)
                return;
            var tr = sprite.textureRect;
            var tex = sprite.texture;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect(
                tr.x / tex.width,
                tr.y / tex.height,
                tr.width / tex.width,
                tr.height / tex.height));
        }
    }
}
#endif