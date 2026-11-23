# タイポグラフィ仕様（D2-M カード / Unity TextMeshPro で再現する前提）

すべての効果は TextMeshPro（TMP）の標準 SDF シェーダーで再現できるものに限定しています。

- 面の色：単色、またはリッチテキストの `<color>`、または文字ごとの縦2色グラデーション
- アウトライン：1本
- アンダーレイ（影）：1つ
- グロー：Lv MAX のみ

値は 1x（750×1050）の px です。2x では2倍にしてください。座標は `1x/layout.json` / `2x/layout.json` にあります。

## フォントとライセンス（すべて SIL Open Font License 1.1）

| 用途 | フォント | 入手先 | ライセンス | 予約フォント名(RFN) |
|---|---|---|---|---|
| カード名 | Shippori Mincho ExtraBold | Google Fonts / github.com/fontdasu/ShipporiMincho | OFL 1.1 | なし |
| 効果テキスト（推奨） | Noto Sans JP Bold（wght 700） | Google Fonts | OFL 1.1 | **"Source"**（※） |
| 効果テキスト（比較候補） | M PLUS 1p Bold | Google Fonts | OFL 1.1 | なし |
| 効果テキスト（比較候補） | BIZ UDPGothic Bold | Google Fonts | OFL 1.1 | なし |
| 効果テキスト（比較候補・旧） | Shippori Mincho Bold | Google Fonts | OFL 1.1 | なし |
| レベル表記 | Cinzel Bold（wght 700） | Google Fonts / github.com/NDISCOVER/Cinzel | OFL 1.1 | なし |

- ライセンス全文は `licenses/OFL_*.txt` にあります。
- OFL のフォントは、ゲームへの組み込み・TMP のフォントアセット化・商用利用が可能です。
- 配布するゲームのクレジットまたは同梱ドキュメントに、著作権表示と OFL 全文を入れてください。
- フォント単体での販売はできません。
- （※）サブセット化など改変したフォントファイルを配布する場合、予約フォント名（RFN）をフォント名に使えません。TMP のアトラス（テクスチャ）化だけなら通常は問題ありません。

## 共通の効果モデル（TMP マテリアルとの対応）

- **アウトライン**：文字の外側へ付ける縁取り。色 #100C16。
  - TMP の Outline は輪郭線をまたいで内側にも食い込みます。そのため **Outline Thickness と同じ量だけ Face Dilate をプラスにして**、太さを保ってください。
- **影（アンダーレイ）**：TMP の Underlay で再現します。
  - 黒 #000000、不透明度 200/255（Alpha 0.78）
  - オフセット：下に 2px（TMP の Offset Y はマイナス）
  - 太らせ量：アウトライン＋3px（Underlay Dilate）
  - ぼかし：名前 5px、効果 3px、レベル 3px（Underlay Softness）
- **TMP の値の目安**：
  - 前提：フォントアセットの Sampling Point Size 90、Padding 9（10%）。
  - 1px は 文字サイズ84px で Thickness 約 0.12、40px で約 0.25 です。
  - 最終的には `1x/sample_*.png` / `preview/*.png` を重ねて目で合わせてください。

## 1. カード名

- **フォント・サイズ**：Shippori Mincho ExtraBold、84px
  - 自動縮小：TMP の Auto Size で Min 52 / Max 84。
  - 最大幅は、ジェムの個数ごとの値（`layout.json` の `name.max_width_by_gem_count`）。
- **位置**：x=50、ベースライン y=860、左揃え
- **色**：
  - A（統一）：アイボリー **#FCF6EE**
  - B（レア度で色味）：
    - silver **#ECF2FC**
    - gold **#FFE9B0**
    - rainbow：1文字ごとに次の5色を順に `<color>` で付けます
      **#FFD6E8 → #FFF0C4 → #D6FFDE → #D0E8FF → #EAD8FF**
- **効果**：アウトライン 2px、影（上記。ぼかし 5px）
- **文字数の目安**（テスト結果）：
  - ジェム3個：5文字 83px、6文字 69px、7文字 59px、8文字 52px
  - ジェム4個：5文字 70px、6文字 58px。7文字以上は 52px でも入りきりません。
  - 推奨：**名前は6文字まで**。7文字以上の場合は、TMP の Character Spacing をマイナスにするか、ジェム4個のスキルでは避けてください。

## 2. 効果テキスト

- **フォント・サイズ**：Noto Sans JP Bold 40px（比較候補：M PLUS 1p Bold / BIZ UDPGothic Bold / Shippori Mincho Bold）
- **位置・行**：x=50、1行目の上端 y=902、行の高さ 52px、最大2行、幅 650px
- **色**：本文 **#F0ECF6**、数値 **#F2D58A**（淡い金）
- **数値の色ルール**：正規表現 `[+＋\-−]?[0-9０-９]+(?:[.．][0-9０-９]+)?[%％]?` に一致した部分を `<color=#F2D58A>…</color>` で囲みます。
  - 例：「ダメージ<color=#F2D58A>15%</color>UP（<color=#F2D58A>10</color>秒）」
  - 単位（秒・UP など）は本文の色のままです。
- **改行**：
  - 「。」「、」の後、または「（」「「」の前で改行します（行が35%以上埋まっている場合）。
  - 「、。）」』！？ー%」は行頭禁止です。TMP の禁則処理（Line Breaking Rules）と同じ考え方です。
- **効果**：アウトライン 2px、影（ぼかし 3px）

## 3. レベル表記（文字のみ。プレートは `shade_top` と `frame_*` に含まれます）

- **フォント**：Cinzel Bold。小文字は小型の大文字（スモールキャップ）として表示されます。
- **配置**：プレートの中心 (375, 51) に、文字列全体を左右中央揃えで置きます。
  - ベースライン ＝ 51 ＋ メインの文字の大文字の高さ ÷ 2
- **書式**（`<size>` の単位は px）：
  - 通常：`<size=24>Lv</size><space=3><size=38>2</size>`、色 **#ECE8F4**
  - 分数：`<size=24>Lv</size><space=3><size=38>2</size><size=24><color=#A8A0B8>/3</color></size>`
  - MAX：`<size=24>Lv</size><space=3><size=31>MAX</size>`
    - 色：TMP の Color Gradient（Vertical Gradient）で 上 **#FFF6C8** → 下 **#E8A434**。グラデーションは文字ごとにかかります。
    - グロー：TMP の Glow。色 **#FFC44A**、Alpha 0.47、Offset 0、Inner 0、Outer 約 0.35、Power 約 0.75。見た目は「ぼかし 6px・不透明度 120/255」に相当します。
- **効果**：アウトライン 2px、影（ぼかし 3px）
- Lv MAX は固定の文字列なので、画像版 `pieces/level_max_text.png` も用意しています（任意）。

## 4. ジェムとカプセル（参考）

- **ジェム**：60px（2x は 120px）、間隔 8px。中心 y=830。
- **カプセル**：
  - サイズ：高さ 76px、内側の余白 8px、右端 x=700。
  - 見た目：塗り #08060C（不透明度 218/255）、レア度色の線 1.6px、外側に 1px の黒線、柔らかい影。
  - 画像：`pieces/capsule_{rarity}_n{1-4}.png`。
- **名前とカプセルの間隔**：16px 以上。
