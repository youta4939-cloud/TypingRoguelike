# TypingRoguelike — 小塔探索プロトタイプ

現行の仕様は [`Docs/ゲーム仕様_現状と今後.md`](../../Docs/ゲーム仕様_現状と今後.md)。この README の階層数・バックパック・イベントの記述は古い。

## プレイ（SampleScene → ▶ Play）

- **a-z** タイピング / **Space** ポーズ / **Enter** リロール
- タイピング SE: `Resources/Battle/Audio/DefaultTypingAudio.asset`（未設定時は仮ビープ）
- 左パネル: **★ 固有**（金系） / **◆ カード**（青系）
- 単語完了ごとに入力文字数と低確率のフリーズ抽選を蓄積。当選すると通常単語ダメージの代わりに
  **白い閃光 → ブラックアウト → 3人カットイン → JACKPOT連撃**が発生する

## 探索ループ

1. 1階層は **3列×6段程度の分岐マップ＋階層ボス**。プロトタイプでは全5階層（本番想定99階層）
2. 塔の構造はセーブごとに固定。未踏マスは **?**、一度踏んだマスは次回以降も内容が表示される
3. 各階層の最奥ボスを倒すと、次の階層の新しいマップへ進む
4. 5階層のいずれかにある **エレベーター**で帰還すると所持カードを図鑑へ確保できる
5. 戦闘・宝箱で **塔片**（ラン内通貨）を獲得。**行商人**で塔片を使い回復や刻印購入
6. **刻印の残響**イベントで、カードを1枚選んで獲得（戦闘直後の報酬は塔片のみ）
7. 先へ進んで敗北、または通常放棄すると、そのランの未登録カードは失われる
8. 最終階層ボス撃破時は自動的に帰還成功となる

## 所持上限

- 3キャラそれぞれ **初期スキル枠1＋カード2枠** = 装備9枚
- バックパックは6枚、合計最大15枚
- バックパック満杯で新カードを得た場合、既存カードとの入れ替えか取得放棄を選ぶ
- マップ左の「装備・バックパック」から戦闘外で装備を整理できる（初期スキル枠も入れ替え可）
- 拠点からのカード持ち込みはなし。出発時は各キャラの初期スキルのみ

## データ更新

**Tools → TypingRoguelike → Generate Prototype Data**  
（`rewardCardPool`・**戦闘立ち絵プレースホルダー** まで一括生成。既存プロジェクトは1回実行）

**Tools → TypingRoguelike → Generate Battle Art (Portraits)** — 立ち絵 PNG の再生成のみ

- 立ち絵: `Resources/Battle/Art/Portraits/`（Alice / Ellen / Luna）
- ボス: `Resources/Battle/Art/Boss_Dragon.png`
- 本番用イラストに差し替える場合: 同パスに PNG を上書き（**ファイル名は Alice / Ellen / Luna / Boss_Dragon のまま**）
- 差し替え後に **Tools → TypingRoguelike → Fix Portrait Import & Reconnect** を実行 → Play を**一度止めて**再 Play
- Import: **Texture Type = Sprite (2D and UI)**、**Sprite Mode = Single**、透明背景推奨（白背景の PNG も表示はされます）

- 報酬プール: `Data/Prototype/SkillCardPool_Prototype.asset`
- エンカウント: `Resources/Battle/PrototypeBossEncounter.asset` の **Reward Card Pool**

## 戦闘 UI テンプレート（9-slice）

**Tools → TypingRoguelike → Generate Battle UI Template**

- 生成先: `Resources/Battle/UI/`（`window_panel.png` など）
- 設定: `Resources/Battle/UI/DefaultBattleUITemplate.asset`
- **Asset Store / Kenney の UI パック**を使う場合: 同フォルダの PNG を差し替え → テンプレートメニューを再実行（または SO の Sprite を直接指定）
- Import: **Sprite Mode = Single**、**Border** を 9-slice 用に設定（生成ツールは 24px で設定）

## フォルダ

- `Scripts/Run/` — 固定塔、永続マップ開示、撤退・持ち帰り、装備・バックパック
- `Scripts/Data/` — ScriptableObject 定義
