# TypingRoguelike 敵モンスター編集ツール（共有用説明書）

別の AI・実装者がこのプロジェクトで敵データを編集・拡張するためのリファレンスです。  
**対象バージョン**: `EnemyDefinition` 3 枠ローテ + `EnemyVisualRegistration` + **SpritePair プレゼンター（戦闘接続済み）**。数値正本は `Assets/Monster/enemy_visual_preset_spec.md`。

> **別 AI への詳細ハンドオフ（推奨）**  
> プリセットの差し替え手順・レイヤ・検証リスト込み:  
> **`Assets/_Project/Docs/EnemySystem_AI_Handoff.md`**

---

## 1. 目的とスコープ

| 領域 | ツールでやること | ランタイム |
|------|------------------|------------|
| 出現条件・HP・種類 | 編集・保存 | `EnemyEncounterResolver` / カタログ |
| 攻撃 3 枠ローテ | 編集・保存 | `BattleController.ConfigureEnemySwings` |
| ブレイク条件 | 編集・保存 | 戦闘側（既存） |
| 姿・2 枚絵・演出 enum | 編集・プレビュー（ライブラリ再生） | `EnemySpritePairPresenter`（`SpritePair` のみ） |
| パーツリグ敵 | 従来フィールド | 既存 `battleVisual` |

**開き方（Unity Editor）**: メニュー `Tools → TypingRoguelike → 敵モンスター編集`  
**戦闘風プレビュー**: `Tools → TypingRoguelike → 敵プレビュー（戦闘風）`（編集画面の「別窓プレビュー」からも可）

### プレビューの使い分け

| 入口 | 内容 |
|------|------|
| **別窓プレビュー（推奨）** | 攻撃リング・単語カード位置を模した戦闘風レイアウト。再生／位置編集は従来どおり |
| **下部簡易プレビュー** | 編集ウィンドウ最下部の従来プレビュー（機能は同じ） |

---

## 2. ファイル一覧（実装の入口）

| 役割 | パス |
|------|------|
| データ本体 | `Assets/_Project/Scripts/Data/EnemyDefinition.cs` |
| 攻撃構造体・enum | `Assets/_Project/Scripts/Data/EnemyAttackData.cs` |
| 攻撃マイグレーション・ローテ補助 | `Assets/_Project/Scripts/Data/EnemyDefinitionAttackUtil.cs` |
| ビジュアル enum | `Assets/_Project/Scripts/Data/EnemyVisualPresets.cs` |
| ビジュアル登録 struct | `Assets/_Project/Scripts/Data/EnemyVisualRegistration.cs` |
| メイン UI | `Assets/_Project/Editor/EnemyDefinitionEditorWindow.cs` |
| ビジュアル UI・プレビュー | `Assets/_Project/Editor/EnemyDefinitionEditorVisual.cs` |
| 戦闘風プレビュー窓 | `Assets/_Project/Editor/EnemyVisualPreviewWindow.cs` |
| プレビュー再生ドライバ | `Assets/_Project/Editor/EnemyVisualEditorPreviewDriver.cs` |
| カタログ・遭遇プール同期 | `Assets/_Project/Editor/EnemyCatalogSync.cs` |
| 一括レガシー移行 | `Assets/_Project/Editor/EnemyDefinitionBatchMigrate.cs`（別メニュー） |

**敵アセットの置き場（新規作成時）**: `Assets/_Project/Data/Enemies/`（`enemyId` は自動で `enemy_yyyyMMdd_HHmmss` など）

**ランタイム参照**:

- カタログ: `Assets/_Project/Resources/Battle/EnemyCatalog.asset`
- 遭遇プール:  
  - 通常 `PrototypeCombatEncounter.asset`  
  - エリート `PrototypeEliteEncounter.asset`  
  - ボス `PrototypeBossEncounter.asset`

---

## 3. データモデル `EnemyDefinition`

ScriptableObject。`CreateAssetMenu`: `TypingRoguelike/Enemy Definition`。

### 3.1 基本・出現

| フィールド | 意味 |
|------------|------|
| `enemyId` | 内部 ID（カタログ・ファイル参照） |
| `displayName` | 一覧・UI 表示名 |
| `spawnCategory` | `Normal` / `Elite` / `Boss` — **戦闘の tier と一致する敵だけ**が候補 |
| `floorMin`, `floorMax` | 出現するダンジョン階層（両端含む） |
| `pickWeight` | 同階層・同種類の候補内での**抽選重み**（大きいほど出やすい）。**攻撃間隔ではない** |
| `maxHp` | 最大 HP（ラン倍率はランタイム側） |
| `designerNotes` | 自由メモ |

**エリート戦の出方（設計意図）**: マス種別やイベントが Elite tier の戦闘 → `spawnCategory == Elite` かつ `floorMin ≤ 現在階層 ≤ floorMax` の敵がプールから選ばれる。

### 3.2 攻撃（3 枠・ローテ）— ランタイム接続済み

`attacks` は長さ **3** 固定（`EnemyDefinitionAttackUtil.AttackSlotCount`）。  
`EnemyAttackPattern`:

| フィールド | 意味 |
|------------|------|
| `damage` | 与ダメージ |
| `intervalSeconds` | 枠が**再度選ばれたあと**の基本間隔 |
| `effect` | `None` / `Telegraph`（予告秒＝`effectValue`）/ `PlayerAttackSlow` |
| `effectValue` | 効果パラメータ |
| `firstDelaySeconds` | **この枠が選ばれた直後**の初回待ち。`0` なら `intervalSeconds` を使う |

**有効枠**: `damage > 0` **かつ** `intervalSeconds > 0`（`IsActive`）。

**ランタイム挙動**（`BattleController`）:

1. 戦闘開始時、有効枠の**先頭**からローテ開始（`FirstRotateSlot`）。
2. 選ばれた枠だけタイマーでカウント（旧「小攻撃・強攻撃の並行タイマー」は廃止）。
3. 発動後、次の有効枠へ（`AdvanceRotateSlot`、順序は配列インデックス順でスキップなし巡回）。
4. **開幕専用タイマーは無し**。初撃は枠 1 の `firstDelaySeconds` または `intervalSeconds`。

`attackPickMode`: 現状エディタは `Rotate` 固定。`Weighted` はデータ上のみ。

### 3.3 レガシー攻撃フィールド

まだ 3 枠が空で、旧フィールドだけある敵:

- `lightAttackInterval` / `lightAttackDamage`
- `heavyAttackInterval` / `heavyAttackDamage` / `heavyAttackTelegraphSeconds` / `heavyAttackFirstSeconds`
- `openingAttackDelay`（HideInInspector、枠 1 初回待ちに移行）

**自動移行**: 一覧更新・詳細表示時に `TryMigrateLegacyIfNeeded`。手動は「旧攻撃→3 枠に取り込み」またはメニュー `Migrate All Enemy Attack Slots`。

移行マップ（概要）:

- 枠 0 ← 小攻撃 + `openingAttackDelay`
- 枠 1 ← 強攻撃 + 予告・初回秒
- 枠 2 ← 空

枠未設定でもランタイムは `GetResolvedSlot` がレガシーを一時的に読む（保存推奨は 3 枠明示）。

### 3.4 狂暴・その他戦闘パラメータ

| フィールド | 意味 |
|------------|------|
| `enrageHpFraction` | HP 割合で狂暴 |
| `enrageIntervalMultiplier` | 狂暴時、攻撃間隔に乗算（初回待ちの扱いは戦闘コード参照） |
| `attackDamage` 等 | レガシー／他システム用が残存。新規は `attacks` を優先 |

### 3.5 ブレイク

| フィールド | 意味 |
|------------|------|
| `breakEnabled` | 有効フラグ |
| `breakRequirements` | `ElementColor[]`（赤・青・黄、順不同） |
| `breakDuration` | ブレイク継続秒 |
| `breakDamageMultiplier` | 与ダメ倍率 |

エディタ: プリセット「3赤3青3黄」、個別チップクリックで色循環。

### 3.6 ビジュアル `visual`（`EnemyVisualRegistration`）— **戦闘接続済み**

`EnemyDefinition.battleIllustration` は**立ち絵の後方互換**。`立ち絵2枚` モードでは `visual.idleIllustration` と同期される。

| フィールド | 意味 |
|------------|------|
| `graphicMode` | `PartRig` / `SpritePair` / `EmojiOnly` |
| `idleIllustration`, `attackIllustration` | 待機・攻撃スプライト |
| `displayScale` | 0.35〜2.5 |
| `feetPivotIdle`, `feetPivotAttack` | 正規化座標 (0〜1)。**足元**基準。デフォルト `(0.5, 0)` |
| `poseOffsetIdle`, `poseOffsetAttack` | 表示位置補正（Eh 比） |
| `motionPreset`, `strikeEffect`, `effectPalette`, `effectIntensity` | 演出プリセット ID（enum）→ `EnemyVisualPresetLibrary` が解釈 |
| `defeatPreset` | 撃破演出 ID |
| `hitFlashColor` | 被弾フラッシュ色 |
| `windupSecondsOverride` 等 | `0` = ライブラリ既定。`>0` で敵ごと上書き |

**表示モードと `battleVisual`:**

- `PartRig`: 従来 `EnemyBattleVisualKind`（紙の天使・大口等）。
- `SpritePair`: `battleVisual = StaticIllustration` + **`EnemySpritePairPresenter`**（戦闘・プレビュー）。
- `EmojiOnly`: `emojiNormal` / `emojiEnraged` / `emojiDefeated` のみ編集。

**プリセット差し替えの詳細**は `EnemySystem_AI_Handoff.md` セクション 4。

---

## 4. エディタ UI 操作

### 4.1 ツールバー

| ボタン | 動作 |
|--------|------|
| 更新 | 全 `EnemyDefinition` を再スキャン。レガシー自動マイグレーションあり |
| 新規 | `Data/Enemies` にアセット作成 + カタログ同期 |
| 保存 | 全敵を Dirty + `SaveAssets` + **カタログ同期** |
| カタログ同期 | `EnemyCatalog` 再構築 + 3 種遭遇の `enemyPool` 更新 |

### 4.2 一覧

- サムネ（`visual.ResolveIdle`）
- 名前・種類・層・HP・ブレイク色チップ
- 検索: `displayName` / `enemyId` / アセット名

### 4.3 詳細ペイン（上から）

1. **基本** — ID、種類、階層、重み、HP  
2. **姿・演出** — `EnemyDefinitionEditorVisual`（モード分岐・プリセット・プレビュー）  
3. **攻撃（3 枠・ローテ）** — 表形式 5 列  
4. **ブレイク**  
5. **その他** — 狂暴、メモ  
6. **旧攻撃→3 枠** / **削除**

### 4.4 ビジュアルプレビュー（エディタのみ）

- **別窓（推奨）**: 戦闘風レイアウト（リング・単語カード位置）。ツールバー「別窓プレビュー」
- **下部簡易**: 編集ウィンドウ最下部（従来）
- ボタン: 待機 / 攻撃モーション再生 / 被弾 / 撃破 / 位置合わせ
- 編集: 足元クリック・位置ドラッグ（立ち／攻撃）。ゴースト重ねあり
- プレビューは `EnemyVisualPresetLibrary.Sample*` を使用（モーションは誇張 2.75 倍）

---

## 5. カタログ同期のルール `EnemyCatalogSync`

1. プロジェクト内全 `EnemyDefinition` を列挙  
2. `ApplyDefaultSpawnCategories`  
   - アセット名に `Boss` → Boss  
   - `Elite` → Elite  
   - `displayName == "強敵"` かつ Normal → Elite  
3. `EnemyCatalog.RebuildFromProjectEnemies`  
4. 種類別に遭遇アセットの `enemyPool` を上書き（空プールはスキップ）

**運用**: 敵を編集したら **保存** または **カタログ同期** しないと遭遇に載らない。

---

## 6. 遭遇抽選（ランタイム・編集時の前提）

`EnemyEncounterResolver.PickEnemy`:

1. `EnemyCatalog.PickForFloor(floor, category, rng)` を試す  
2. なければ `BattleEncounterDefinition.enemyPool` から  
   - `spawnCategory` 一致  
   - `floorMin ≤ floor ≤ floorMax`  
   - `pickWeight` で加重ランダム  

戦闘開始時の `category` は遭遇の `ExEnemyTier` から決まる（`BattleController.ResolveBattleEnemy`）。

---

## 7. 演出プリセット enum 早見表

`EnemyVisualPresets.cs` に定義。数値は安定させ、将来ライブラリが解釈する。

**EnemyMotionPreset**: None, Idle, Lunge, RushForward, SwingInPlace, CastMagic  

**EnemyStrikeEffectKind**: None, Slash, Thrust, Bite, Claw, Magic  

**EnemyEffectPalette**: Neutral, BloodRed, PoisonGreen, CursePurple, IceBlue, HolyGold  

**EnemyEffectIntensity**: Small, Medium, Large  

**EnemyDefeatPreset**: FadeOut, TurnToAsh, Crumble, MistDissolve  

エディタ表示ラベルは `EnemyDefinitionEditorVisual` 内の日本語配列（enum 順と一致必須）。

---

## 8. 別 AI への実装タスク

**詳細・優先度・やってはいけないこと**は `EnemySystem_AI_Handoff.md` セクション 9。

要約:

1. Rush / Swing / Magic を Lunge プロキシから独立  
2. Weighted 攻撃選択のランタイム  
3. 撃破 4 種の見た目差  
4. StrikeFX: シェーダー不具合時は色付きフォールバック確認（`Build Strike FX Catalog (v1)`）

---

## 9. よくある混乱の整理

| 誤解 | 正しい理解 |
|------|------------|
| `pickWeight` が攻撃の速さ | 出現抽選の重みのみ |
| 開幕〜初撃（秒）という専用項目 | 廃止。枠の `firstDelaySeconds` / `intervalSeconds` |
| 3 枠が空＝攻撃なし | レガシーがあれば実行時フォールバック。一覧はマイグレーション推奨 |
| プリセットを選んでも戦闘で動かない | **SpritePair + Lunge 等は戦闘で動く** |
| Idle/なしでも飛びかかる | ほぼ動かない。Lunge 等を選ぶ |
| `カタログ同期` は任意 | プール反映には必要 |

---

## 10. 最小ワークフロー（デザイナー）

1. `敵モンスター編集` を開く  
2. 新規 or 選択  
3. 種類・階層・重み・HP を設定  
4. 攻撃 3 枠（少なくとも 1 枠を Active）  
5. 姿: `立ち絵2枚` + 待機/攻撃スプライト・倍率・足元・**Lunge 等**・斬撃  
6. **別窓プレビュー**で攻撃確認  
7. **保存**（＋必要ならカタログ同期）  
8. プレイで階層・tier が合うか確認  

---

## 11. 関連クラス（戦闘側クイックリンク）

- 攻撃ループ: `BattleController.cs` — `ConfigureEnemySwings`, `BeginAttackOnSlot`, `AdvanceToNextRotatedAttack`
- 見た目: `EnemySpritePairPresenter.cs` / `EnemyVisualPresetLibrary.cs`
- 階層渡し: `RunFlowController` → `BattleController.Initialize` の `dungeonFloor`

---

*詳細ハンドオフ: `EnemySystem_AI_Handoff.md`。数値正本: `Assets/Monster/enemy_visual_preset_spec.md`。*