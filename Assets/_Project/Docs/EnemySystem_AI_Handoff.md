# TypingRoguelike 敵システム — 別 AI 向け詳細ハンドオフ

**目的**: 別の AI / 実装者が、敵データ編集ツール・演出プリセット・戦闘接続を壊さずに拡張・差し替えできるようにする。  
**読者**: コード未読でも方針が分かる／実装時はパスを辿れる粒度。  
**数値・演出の正本**: `Assets/Monster/enemy_visual_preset_spec.md`（Lunge は確定、他モーションは仮）。  
**短い操作説明**: `Assets/_Project/Docs/EnemyDefinitionEditorTool.md`（本ドキュメントを詳細版とする）。

---

## 0. 現状サマリ（2026-11 時点）

| 領域 | 状態 |
|------|------|
| 敵編集 EditorWindow | **稼働**（一覧・3枠攻撃・ブレイク・SpritePair 登録） |
| カタログ / 遭遇プール同期 | **稼働** |
| SpritePair 戦闘表示 | **稼働**（`EnemySpritePairPresenter`） |
| 攻撃モーション（Lunge） | **稼働**（エディタプレビュー＋戦闘） |
| 斬撃エフェクト | **稼働（簡易）** — enum で種別切替、見た目はプレースホルダ Image |
| プリセットライブラリ SO | **稼働**（Resources + コードフォールバック） |
| 別窓・戦闘風プレビュー | **稼働** |
| Rush / Swing / Magic 固有モーション | **稼働**（`EnemyRushMotion` / `EnemySwingMotion` / `EnemyCastMotion`、仕様 v2） |
| Weighted 攻撃選択 | データのみ・ランタイム未 |
| 本格 VFX プレハブ差し替え | **未**（後述の差し替え口はある） |

**重要設計**: 敵アセットは「どのプリセット ID を使うか」だけ持ち、**秒数・曲線・色の本体はライブラリ側**。後から見た目を差し替えても、敵データの enum は極力いじらない。

---

## 1. レイヤ構成

```
[デザイナー入力]
  EnemyDefinition アセット（visual に enum + スプライト + ピボット）
        │
        ▼
[解釈]
  EnemyVisualPresetLibrary  … タイミング・強度・Sample*（ポーズ）
  EnemyLungeMotion          … Lunge の曲線（確定値）
  EnemyVisualPaletteColors  … パレット → Color
        │
        ▼
[表示]
  戦闘: EnemySpritePairPresenter + EnemySpritePairStrikeFx
  編集: EnemyVisualEditorPreviewDriver（IMGUI）+ EnemyVisualPreviewWindow
```

**ルール**: 敵ごと上書きは `EnemyVisualRegistration` の `*Override`（`0` = ライブラリ既定を使う）。

---

## 2. ファイルマップ

### 2.1 データ

| パス | 役割 |
|------|------|
| `Scripts/Data/EnemyDefinition.cs` | 敵 SO 本体 |
| `Scripts/Data/EnemyAttackData.cs` | 攻撃 3 枠・`visualIntensity` |
| `Scripts/Data/EnemyDefinitionAttackUtil.cs` | 枠長保証・レガシー移行 |
| `Scripts/Data/EnemyVisualPresets.cs` | **プリセット enum（数値固定）** |
| `Scripts/Data/EnemyVisualRegistration.cs` | 敵ごとの visual 構造体 |

### 2.2 ランタイム演出

| パス | 役割 |
|------|------|
| `Scripts/Battle/Visual/EnemyVisualPresetLibrary.cs` | ライブラリ SO・解釈入口 |
| `Scripts/Battle/Visual/EnemyLungeMotion.cs` | Lunge サンプラ |
| `Scripts/Battle/Visual/EnemySpritePairPose.cs` | ポーズ構造体 |
| `Scripts/Battle/Visual/EnemySpritePairPresenter.cs` | 戦闘中の 2 枚絵駆動 |
| `Scripts/Battle/Visual/EnemySpritePairStrikeFx.cs` | 着弾時フラッシュ／斬撃帯 |
| `Scripts/Battle/Visual/EnemySpritePairShake.cs` | 揺れ |
| `Scripts/Battle/Visual/EnemyVisualPaletteColors.cs` | パレット色 |
| `Scripts/Battle/Visual/EnemyVisualEasing.cs` | イージング |
| `Scripts/Battle/BattleController.cs` | スイング／着弾同期・`SetPaused` |
| `Scripts/Battle/BattleUIBuilder.cs` | `EnemyHitRoot` 生成・Presenter Create |

### 2.3 エディタ

| パス | 役割 |
|------|------|
| `Editor/EnemyDefinitionEditorWindow.cs` | メイン「敵モンスター編集」 |
| `Editor/EnemyDefinitionEditorVisual.cs` | ④画像 UI・プレビュー操作共通 |
| `Editor/EnemyVisualEditorPreviewDriver.cs` | プレビュー再生 Tick |
| `Editor/EnemyVisualPreviewWindow.cs` | 別窓・戦闘風レイアウト |
| `Editor/EnemyCatalogSync.cs` | カタログ＋遭遇プール |
| `Editor/EnemyVisualPresetLibraryBuilder.cs` | ライブラリアセット作成メニュー |

### 2.4 アセット配置

| パス | 役割 |
|------|------|
| `Data/Enemies/*.asset` | 個別敵 |
| `Resources/Battle/EnemyCatalog.asset` | カタログ |
| `Resources/Battle/EnemyVisualPresetLibrary.asset` | **プリセット数値の Inspector 編集口** |
| `Resources/Battle/` 配下の遭遇アセット | 通常／エリート／ボスの `enemyPool` |
| `Monster/enemy_visual_preset_spec.md` | 演出仕様の正本 |

**ライブラリ読込パス**: `Resources.Load("Battle/EnemyVisualPresetLibrary")`  
無い場合は `CreateInstance` のメモリフォールバック（既定フィールド値）。

---

## 3. 敵データの持ち方

### 3.1 `EnemyVisualRegistration`（敵アセットに直保存）

| フィールド | 差し替え時の扱い |
|------------|------------------|
| `graphicMode` | `SpritePair` のときだけ Presenter / プレビュー対象 |
| `idleIllustration` / `attackIllustration` | **絵の差し替えはここ**（敵ごと） |
| `displayScale` | 敵ごとの大きさ |
| `feetPivotIdle` / `feetPivotAttack` | 正規化 0〜1。足元。攻撃 Y はだいたい 0〜0.1 |
| `poseOffsetIdle` / `poseOffsetAttack` | Eh 比オフセット（右＋X、上＋Y） |
| `motionPreset` | モーション ID（後述） |
| `strikeEffect` | 斬撃種別 ID |
| `effectPalette` | 色パレット ID |
| `effectIntensity` | Small/Medium/Large |
| `defeatPreset` | 撃破 ID |
| `hitFlashColor` | 被弾色 |
| `windupSecondsOverride` 等 | **`> 0` のときだけ**ライブラリ既定を上書き |
| `strikeScaleOverride` | 同上（エフェクト／最大スケール系） |

`Default` のモーションは **`Lunge`**（Idle/None だとほぼ動かない）。

### 3.2 攻撃枠との関係

- 枠の `visualIntensity`: `Inherit` なら敵の `effectIntensity`、それ以外は枠優先（`ResolveIntensity`）。
- 枠の `effect == Telegraph` かつ `effectValue > 0` なら、ため秒はその値（`ResolveWindupSeconds`）。

### 3.3 互換フィールド

- `EnemyDefinition.battleIllustration` ← SpritePair 時は `idleIllustration` と同期。
- `battleVisual = StaticIllustration` をエディタがセット（PartRig 以外の絵経路）。

---

## 4. プリセットの「あとから差し替え」完全ガイド

ここが本ドキュメントの核心。**やりたい差し替えの種類ごとに、触る場所が違う。**

### 4.1 方針（壊さないための原則）

1. **enum の数値を変えたり並び替えない**（既存 `.asset` が壊れる）。追加は末尾のみ。
2. 敵アセットは ID 参照のまま。**見た目の本体はライブラリ／サンプラ／FX クラス側を更新**。
3. 仕様数字は `enemy_visual_preset_spec.md` を先に更新し、コードを合わせる。
4. エディタの日本語ラベル配列（`EnemyDefinitionEditorVisual`）は **enum 順と必ず一致**。

### 4.2 差し替え早見表

| やりたいこと | 触る場所 | 敵アセットは？ |
|--------------|----------|----------------|
| 呼吸の周期・振幅を全体変更 | `EnemyVisualPresetLibrary` の Inspector（またはコード既定） | 不要 |
| Lunge の軌道・タイミング変更 | `EnemyLungeMotion.cs`（＋仕様書） | 不要（Override 無しなら） |
| 特定敵だけためを長く | その敵の `windupSecondsOverride` | **その敵だけ** |
| 攻撃モーション種別を敵で切替 | 敵の `motionPreset` | その敵 |
| 新モーション種別を追加 | enum 末尾追加 → `SampleAttackPose` に分岐 → サンプラ新規 | 使う敵だけ設定 |
| 斬撃の見た目を本格 VFX に | `EnemySpritePairStrikeFx.PlayStrike`（プレハブ化推奨） | `strikeEffect` はそのまま可 |
| 斬撃色だけ変える | `EnemyVisualPaletteColors` または敵の `effectPalette` | パレット変更なら敵側 |
| 強度倍率（小中大） | ライブラリの `intensitySmall/Large` 等 | 不要 |
| 撃破の消え方 | `SampleDefeat` | `defeatPreset` |
| 立ち絵／攻撃絵の差し替え | 敵の Sprite フィールド | **その敵** |
| ライブラリ SO を作り直す | メニュー `Create Enemy Visual Preset Library` | 不要 |

### 4.3 レイヤ別の詳細

#### A. ライブラリ数値（全体のつまみ）

アセット: `Assets/_Project/Resources/Battle/EnemyVisualPresetLibrary.asset`  
作成: `Tools → TypingRoguelike → Create Enemy Visual Preset Library`

Inspector で変えられる例:

- `breathPeriod` / `breathScaleY` / `breathScaleX`
- `enrage*` / `break*`（仮）
- `intensitySmall` / `intensityLarge`
- `shakeAmp*Px` / `localFlashPeakMedium`

**コード側** `EnemyVisualPresetLibrary.cs` のメソッドが「解釈ロジック」:

- `ResolveAttackTiming` / `ResolveWindupSeconds` / `DefaultWindup`
- `SampleAttackPose` / `SampleBreath` / `SamplePlayerHit` / `SampleDefeat`

SO にフィールドが無いもの（例: Rush の仮 windup `0.40f`）は **コード内 switch**。あとから SO フィールド化してよい。

#### B. モーションプリセット（`EnemyMotionPreset`）

| 値 | 実装状況 |
|----|----------|
| `None` | ほぼ静止ポーズ |
| `Idle` | ため後に攻撃絵切替程度 |
| `Lunge` | **本実装** `EnemyLungeMotion` |
| `RushForward` / `SwingInPlace` / `CastMagic` | タイミング表はあるがポーズは **Lunge プロキシ** |

**既存モーションの中身を差し替え**:

1. `enemy_visual_preset_spec.md` の該当節を更新  
2. `EnemyLungeMotion` または専用サンプラを実装  
3. `SampleAttackPose` の `switch` を本物に接続  
4. エディタプレビューは同じ `SampleAttackPose` を呼ぶので **自動で追従**（プレビューは `PreviewMotionBoost = 2.75` で誇張あり。戦闘は等倍）

**新モーション追加手順**:

1. `EnemyMotionPreset` に **末尾で**追加（例 `Spin = 6`）  
2. `EnemyDefinitionEditorVisual` の `MotionLabels` に同じ順で日本語追加  
3. `DefaultWindup` / `ResolveActiveSeconds` / `ResolveMaxScale` / `SampleAttackPose` を実装  
4. 仕様書に節を追加  
5. 使う敵の `motionPreset` を新値に（ツールのポップアップ）

#### C. 斬撃エフェクト（`EnemyStrikeEffectKind`）— StrikeFX_v1 接続済み

アセット: `Assets/Effect/StrikeFX_v1/`（README_ja.md 参照）  
カタログ生成: `Tools → TypingRoguelike → Build Strike FX Catalog (v1)`  
→ `Resources/Battle/EnemyStrikeFxCatalog.asset` + ramp マテリアル

| 経路 | 内容 |
|------|------|
| 推奨 | `frames_mask` + `Game/UI/StrikeFxRamp` + `ramps/StrikeRamp_<Palette>` |
| フォールバック | 色付きフレーム／シート（シェーダー不可時） |
| None | 局所フラッシュのみ |
| Magic | 端帯なし・揺れ 60% |

着弾トリガ: Presenter が `impact - impactFrame/fps` で `PlayStrike` を早め開始（着弾コマ一致）。  
UI 35%: `StrikeFxA`（不透明）+ `StrikeFxB`（α0.35）。

**敵ごとだけ変えたい**: ツール「斬撃エフェクト」→ `visual.strikeEffect`。

#### D. 色パレット（`EnemyEffectPalette`）

- 定義: `EnemyVisualPaletteColors.Resolve`  
- 消費: 主に `StrikeFx`（Body / EdgeGlow 等）  
- **全体の色味変更** → `EnemyVisualPaletteColors.cs` の Hex  
- **敵ごと** → `effectPalette`  
- 新色追加 → enum 末尾 + `Resolve` + エディタ `PaletteLabels`

#### E. 強度（`EnemyEffectIntensity` / 枠 `visualIntensity`）

- ライブラリ倍率: Small `0.67` / Medium `1` / Large `1.44`  
- 被弾揺れ振幅などに乗算  
- 敵デフォルトと攻撃枠 Override の優先は `ResolveIntensity`

#### F. 撃破（`EnemyDefeatPreset`）

- 実装: `SampleDefeat`（Ash は短い赤み、他はフェード寄り）  
- 本格化は同メソッド内分岐を厚くする（パーティクル等は Presenter 側で追加可）

#### G. 上書きフィールド（敵単位の例外）

| フィールド | 効く条件 |
|------------|----------|
| `windupSecondsOverride` | `> 0` |
| `activeSecondsOverride` | `> 0` |
| `recoverySecondsOverride` | 現状サンプラ未使用の可能性あり（拡張用） |
| `strikeScaleOverride` | `> 0` で MaxScale 系に使用 |

**0 のまま**が「ライブラリ／仕様の正」を使う意味。全体調整はライブラリ、例外だけ Override。

---

## 5. 戦闘接続（壊しやすいポイント）

### 5.1 Presenter 生成

- `BattleUIBuilder.BuildEnemy` / `RespawnEnemy` で `visuals.UseSpritePair` なら `EnemySpritePairPresenter.Create(EnemyHitRoot, visuals)`  
- `EnemyHitRoot` は約 **420×420**、Arena / AttackRing 系は `BattleUIBuilder` 定数（Arena 600、Ring 560）

### 5.2 攻撃同期

- `BattleController` がスイング開始で Presenter に Attack 開始  
- 着弾はライブラリの `TimeToImpact`（windup+active）と揃える想定  
- ポーズ中は Presenter を `SetPaused` で止める（`timeScale` 非依存）

### 5.3 NRE 対策の経緯（再発防止）

- `TickAttack` で `_shake` / `_strikeFx` null → Idle へ復帰するガードあり  
- `StrikeFx.Update` で Library / Image null ガードあり  
- **新しい必須参照を Update で触るときは必ず null 安全に**

---

## 6. エディタツール操作

### 6.1 メニュー

- `Tools → TypingRoguelike → 敵モンスター編集`
- `Tools → TypingRoguelike → 敵プレビュー（戦闘風）`
- `Tools → TypingRoguelike → Create Enemy Visual Preset Library`

### 6.2 プレビュー

| 入口 | 内容 |
|------|------|
| **別窓（推奨）** | 攻撃リング・単語カード位置を模した枠。再生・足元／オフセット編集は共通 |
| **下部簡易** | 編集ウィンドウ最下部。機能は同じ |

共通操作（`DrawPreviewControls`）:

- 待機 / **攻撃モーション再生** / 被弾 / 撃破 / 位置合わせ  
- 編集: 足元クリック（立ち／攻撃）、位置ドラッグ（立ち／攻撃）  
- ゴースト重ね（攻撃再生中は無効）

実装共有:

- 操作・キャンバス: `EnemyDefinitionEditorVisual`
- 再生: `EnemyVisualEditorPreviewDriver` → `EnemyVisualPresetLibrary.Sample*`
- Repaint: 編集窓 + プレビュー窓の両方

### 6.3 保存と出現

敵を出したい階層・種類に出したあと **保存** または **カタログ同期**。しないと遭遇プールに載らない。

---

## 7. 攻撃 3 枠（要約）

- 有効: `damage > 0` かつ `intervalSeconds > 0`
- ローテ: 有効枠を配列順に巡回。開幕専用タイマー無し（枠の `firstDelaySeconds`）
- レガシー小／強は `TryMigrateLegacyIfNeeded` で 3 枠へ

詳細は旧説明書セクション 3.2〜3.3 と同趣旨。

---

## 8. よくある混乱

| 誤解 | 正しい理解 |
|------|------------|
| プリセットは保存されるだけで動かない | **SpritePair は戦闘・プレビューで動く**（旧ドキュメントの記述は outdated） |
| Idle / なし でも飛びかかる | ほぼ動かない。**Lunge 等を選ぶ** |
| pickWeight が攻撃速さ | 出現抽選の重みのみ |
| 斬撃 enum を変えたら見た目が全部違う | 現状は色中心。形状差は StrikeFx 拡張待ち |
| ライブラリを消しても同じ | フォールバックはあるが、Inspector 調整は消える |
| プレビューの動き＝戦闘そのまま | プレビューは **2.75 倍誇張**。戦闘は等倍 |
| 攻撃ピボット Y を大きくしてよい | **0.35 超で絵が枠外**になりやすい。初期化ボタンあり |

---

## 9. 別 AI への推奨タスク（優先度つき）

### P0（安全・品質）

1. StrikeFx を enum ごとに見た目差を付ける／プレハブ差し込み口を明確化  
2. `recoverySecondsOverride` を本当にサンプラ／コントローラで使うか整理

### P1（演出充実）

3. Rush / Swing / CastMagic を Lunge プロキシから独立サンプラへ（仕様書の仮値を確定へ）  
4. 撃破プリセット 4 種の差を見た目で明確化  
5. 残像・土煙など仕様書の UI 後ろエフェクト

### P2（システム）

6. `attackPickMode == Weighted` のランタイム  
7. プレビューの MotionBoost を戦闘風別窓では 1.0 に近づけるオプション

### やってはいけないこと

- 既存 enum 値の番号変更・挿入（末尾追加以外）  
- `EnemyHitRoot` 無しで Presenter を想定した新規経路  
- Update 毎フレームで未初期化参照を触る  
- 仕様書を無視した Lunge 数字の独断変更（確定値）

---

## 10. 最小検証チェックリスト

敵データ変更後:

1. 編集ツールで SpritePair・Lunge・立ち／攻撃絵を確認  
2. 別窓プレビューで攻撃再生（コンソール NRE なし）  
3. 保存 → カタログ同期  
4. Play モードで実戦闘: 攻撃が 1 回ずつ終わり、ポーズ中に止まっている  
5. 斬撃を変えた敵で色／有無が変わること

プリセット差し替え後:

1. ライブラリ or サンプラ変更が **プレビューと戦闘の両方**に出ること  
2. Override=0 の敵が新既定に追従すること  
3. Override>0 の敵だけ例外が残ること

---

## 11. クイックリンク（実装ジャンプ）

```
データ enum …… EnemyVisualPresets.cs
敵の登録 ……… EnemyVisualRegistration.cs
解釈入口 ……… EnemyVisualPresetLibrary.cs
Lunge ……… EnemyLungeMotion.cs
戦闘表示 ……… EnemySpritePairPresenter.cs
斬撃 ……… EnemySpritePairStrikeFx.cs
色 ……… EnemyVisualPaletteColors.cs
戦闘同期 ……… BattleController.cs
UI 生成 ……… BattleUIBuilder.BuildEnemy / RespawnEnemy
編集 UI ……… EnemyDefinitionEditorWindow.cs
プレビュー …… EnemyVisualPreviewWindow.cs / EnemyVisualEditorPreviewDriver.cs
仕様正本 …… Monster/enemy_visual_preset_spec.md
```

---

*本ハンドオフは実装現状（SpritePair 接続済み・別窓プレビューあり）に合わせたものです。プリセット差し替え時はセクション 4 を先に読み、enum 互換を守ってください。*
