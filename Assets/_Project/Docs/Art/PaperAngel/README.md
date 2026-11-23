# 紙の天使 — 動かし用パーツ

## Unity 配置

| 用途 | パス |
| --- | --- |
| 戦闘スプライト（Resources） | `Assets/_Project/Resources/Battle/Art/Enemies/PaperAngel/` |
| この README / pivots | `Assets/_Project/Docs/Art/PaperAngel/` |
| 分割・テスト用スクリプト | `Tools/PaperAngelPipeline/` |

初回または画像差し替え後: **Tools → TypingRoguelike → Setup Paper Angel Enemy**

戦闘表示: `EnemyDefinition.battleVisual = PaperAngel`（通常敵は設定済み）。待機モーションは `PaperAngelIdle.cs`（4秒ループ、README 下記テスト動画と同系統）。

---

# paper_angel_parts（紙の天使）— 動かし用パーツ

- 元絵: トダさん提供 01M5GXVA0NS3EN52KSVKVN309H.png（369×477）
- 拡大: Real-ESRGAN x4（AI拡大）→ **1476×1908 px**。全パーツこのキャンバスサイズ・同じ座標（重ねるだけで元絵に戻る）
- 背景抜き: BiRefNet で切り抜き → 背景色を引いて縁の灰色を除去
- 向き: 正面・左右対称なので反転不要（ゲーム内の左下向きルールに対しても正面のままでOK）

## 描画順（奥 → 手前）
01_shadow → 02_wing_L_inner → 03_wing_L_outer → 04_wing_R_inner → 05_wing_R_outer → 06_feet → 07_face_inner → 08_body → 09_seal → 10_seal_glow_fx（加算）

L/R は画面上の左右。羽は頭巾の裏の肩の根元で回転（parts_pivots.json）。
羽の付け根は頭巾の裏まで描き足し済み（推奨 ±10°、±14° まで穴なし確認）。
内側の羽は外側の羽の下にもう1枚分描き足してあるので、外側を少し遅らせて動かしても隙間は出ません。
顔（07）は頭巾の奥にあり、胴体の穴越しに見える。上下 4px／2° までなら自然に動きます。
10_seal_glow_fx は脈動用の光（加算合成・任意）。元絵にはない演出用レイヤーです。

## テスト動画
paper_angel_parts_test.mp4 / .gif … 4秒ループ。羽ばたき2回、ふわっと浮く、印がかすかに脈打つ。
