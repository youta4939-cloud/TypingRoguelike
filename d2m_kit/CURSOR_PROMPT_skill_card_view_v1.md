# Unity：スキルカードの見た目を実装する（D2-M カード）

スキルカードの**見た目だけ**を実装してください。カードの効果やゲームのロジックは触りません。
アセットは `d2m_kit_1x.zip` を展開したものです（750×1050、1x）。仕様の正本はキット内の `README.md`、`typography_spec.md`、`1x/layout.json` です。数値が食い違ったら `layout.json` を優先してください。

## 1. アセットの配置

- `Assets/Card/D2M/` に 1x フォルダの中身を入れてください（`card_mask.png`、`shade_top.png`、`shade_bottom.png`、`frame_{silver,gold,rainbow}.png`、`pieces/`、`gems/`、`preview/`、`layout.json`）。
- インポート設定：Texture Type = Sprite (2D and UI)、Alpha Is Transparency ON、Mip Maps OFF、Compression は High Quality（または None）、sRGB ON。
- フォントは Google Fonts から入手し、TMP Font Asset を作ります（Sampling Point Size 90、Padding 9）。
  - カード名：Shippori Mincho ExtraBold
  - 効果テキスト：Noto Sans JP Bold
  - レベル：Cinzel Bold
  - いずれも OFL 1.1。ライセンス全文はキットの `licenses/` にあります。

## 2. プレハブ `SkillCardView`（uGUI、RectTransform 750×1050、ピボット左上で座標を合わせやすく）

下から上へ、すべて (0,0) に 750×1050 で重ねます。

1. `ArtMask`（Mask または SpriteMask。`card_mask.png` で切り抜き。白＝表示）
   - 子に `Art`（Image、750×1050、カードイラスト）
2. `ShadeBottom`（Image：`shade_bottom.png`）
3. `ShadeTop`（Image：`shade_top.png`）
4. `Frame`（Image：`frame_{rarity}.png`）
5. `LevelText`（TMP）：プレート中心 (375, 51) に左右中央揃え
6. `NameText`（TMP）：x=50、ベースライン y=860、左揃え
7. `EffectText`（TMP）：枠 (50,902)〜(700,1006)、最大2行
8. `GemCapsule`（Image：`pieces/capsule_{rarity}_n{N}.png`）
9. `Gems`（Image × 最大4：`gems/gem_{R,Y,G,B,P}_on.png`）

※座標は左上原点・下向きが +y の px です（画像の座標）。uGUI では y を反転して配置してください。

## 3. データ

`SkillCardData`（ScriptableObject か既存のスキルデータ）から以下を受け取り、`SkillCardView.Apply(data)` で見た目を更新します。

- `Sprite art`
- `Rarity rarity`（Silver / Gold / Rainbow）
- `string skillName`
- `string effectText`
- `int level`、`int maxLevel`（level == maxLevel で MAX 表記）
- `List<GemColor> gems`（R/Y/G/B/P、0〜4個）

## 4. 文字の仕様

共通（TMP マテリアル）：

- Outline：色 #100C16、2px。Outline Thickness と同じ量だけ Face Dilate をプラスにして太さを保つ。
- Underlay：黒、Alpha 0.78、Offset Y −2px、Dilate はアウトライン＋3px。Softness は名前 5px、効果 3px、レベル 3px。
- 目安：1px ≒ 84px の文字で Thickness 0.12、40px で 0.25。最終的に `1x/sample_*.png` と `preview/*.png` を重ねて目で合わせる。
- 名前・効果・レベルで設定が違うので、マテリアルプリセットを3つ作ってください。

### カード名

- Shippori Mincho ExtraBold、Auto Size（Min 52 / Max 84）。
- 最大幅はジェムの数で変える：0〜1個 558、2個 490、3個 422、4個 354。
- 色：アイボリー #FCF6EE（デフォルト）。オプションでレア度色（silver #ECF2FC、gold #FFE9B0、rainbow は1文字ずつ #FFD6E8 → #FFF0C4 → #D6FFDE → #D0E8FF → #EAD8FF を `<color>` で循環）。インスペクタで切り替えられるように。

### 効果テキスト

- Noto Sans JP Bold 40px、行の高さ 52px、幅 650px、最大2行。
- 本文 #F0ECF6。数値は正規表現 `[+＋\-−]?[0-9０-９]+(?:[.．][0-9０-９]+)?[%％]?` に一致した部分を `<color=#F2D58A>…</color>` で囲む（単位は本文色のまま）。
- 禁則：「、。）」』！？ー%」を行頭に置かない（TMP の Line Breaking Rules に追加）。

### レベル

- Cinzel Bold、色 #ECE8F4、プレート中心 (375, 51) に全体を中央揃え。
- 通常：`<size=24>Lv</size><space=3><size=38>2</size>`
- MAX：`<size=24>Lv</size><space=3><size=31>MAX</size>`。Color Gradient（縦）上 #FFF6C8 → 下 #E8A434、Glow 色 #FFC44A・Alpha 0.47・Outer 0.35・Power 0.75。MAX 用マテリアルを別に用意。画像 `pieces/level_max_text.png`（左上 (309,25)）で代用しても可。

## 5. ジェムとカプセル

- ジェム：60px、中心 y=830、右揃え。中心 x は個数ごとに
  - 1個：662
  - 2個：594, 662
  - 3個：526, 594, 662
  - 4個：458, 526, 594, 662
- カプセル：`pieces/capsule_{rarity}_n{N}.png` を使用。置く位置は `layout.json` の `gems.capsule.box_for_n[N]` の左上から 8px 引いた位置（影の余白分）。
- ジェム0個のときはカプセルも非表示。

## 6. 確認用

- テスト用シーン `CardViewTest` を作り、silver / gold / rainbow × ジェム1〜4個 × Lv1・Lv MAX を並べてください。
- `1x/sample_silver.png` などと同じ内容を表示し、半透明で重ねてズレを確認できるオーバーレイ（トグル）を付けてください。ズレは ±2px 以内が目標。
- 表示倍率を変えても崩れないこと（カードの縮小表示 50% でも文字が読めること）。

## やらないこと

- カードの効果処理、ドロー、デッキなどのロジック。
- アセット画像の編集や作り直し（見た目がおかしい場合は報告だけしてください）。
