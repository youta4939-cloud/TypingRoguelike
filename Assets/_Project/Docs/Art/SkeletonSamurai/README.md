# 骸骨の侍 — 揺れ用パーツ

## Unity 配置

| 用途 | パス |
| --- | --- |
| 戦闘スプライト | `Assets/_Project/Resources/Battle/Art/Enemies/SkeletonSamurai/` |
| この README / pivots | `Assets/_Project/Docs/Art/SkeletonSamurai/` |
| 分割・テスト | `Tools/SkeletonSamuraiPipeline/` |

戦闘では `EnemyDefinition.battleVisual = SkeletonSamurai`。重撃型（`Enemy_PrototypeHeavy`）に設定済み。危険マスの抽選で出る。

重ね方は下の「Unity での読み込みルール」どおり。回転は空の親だけ。着物の裾は `KimonoSway` シェーダー（振幅 横 3.5px / 縦 0.8px、2秒周期）。

---

# skeleton_samurai_parts（骸骨の侍）— 揺れ用パーツ

- 元絵: トダさん提供 01M5H71RPN6R2RC6YN40DSFDJP.png（1024×1536）。十分な解像度なので拡大なし・**1024×1536 px** のまま
- 背景抜き: BiRefNet。白背景の色を差し引いて縁の白フチを除去（黒・暗い赤の背景で確認済み）
- 向き: 正面〜やや画面左向き → ゲームの「左下向き」ルールのままでOK。**反転セットは不要**
- 見た目は変えていません（切り抜き・分割・隠れる部分の描き足しのみ）

## Unity での読み込みルール（重要）
前回、胸の印が全身サイズに拡大されてしまった件の対策です。

1. **全パーツが同じキャンバスサイズ（1024×1536）の PNG**。余白も含めてそのまま使う（Sprite Editor でトリミングしない／Mesh Type は Full Rect）
2. **Pixels Per Unit は全パーツ同じ値**にする（例: 100）
3. 各パーツの GameObject は親の下で **Local Position (0,0,0)・Scale (1,1,1)** に置く。これだけで元絵どおりに重なります
4. parts_pivots.json の pivot は **回転の中心にだけ**使う。Sprite の Pivot を変える場合は、位置がずれないよう「同じ点を pivot にした空の親オブジェクトを作って回す」方式にしてください（パーツ自体を移動・拡大しない）
5. 座標は左上原点・下向き y（ピクセル）。Unity の pivot（左下原点・0〜1）に直すなら: px = x/1024、py = 1 − y/1536

## 描画順（奥 → 手前）
01_shadow → 02_legs → 03_kimono_lining → 04_kimono → 05_torso → 06_scabbards → 07_sash_strip_L → 08_sash_rope → 09_sash_strip_R → 10_arm_R → 11_arm_L_katana → 12_skull → 13_hat → 14〜18（笠の札・鎖）

L/R は画面上の左右。親子関係: 06〜12 は 05_torso の子、13_hat は 12_skull の子、14〜18 は 13_hat の子。

## 着物の揺れ（推奨: シェーダー）
**推奨は 04_kimono 1枚 + sway_mask_kimono.png のシェーダー揺れ**です（ボーン分割より継ぎ目が出ません）。
- マスク: 白 = 裾（一番揺れる）、黒 = 腰（固定、y=650）
- UV オフセット = mask × 振幅 × sin(時間 + y×0.010 − x×0.006)（左右で少しずつ位相がずれる）
- 推奨振幅: 横 3〜4px（裾で）、縦 0.8px 程度。速さは 2秒で1往復くらい
- 03_kimono_lining（固定の裏地）が後ろにあるので、揺れても穴は開きません（6px まで確認）

代替（ボーン方式）: alt_hem_sections/ の 04a/04b/04c（裾の左・中・右）を **04_kimono の代わりに**使う（両方は重ねない）。pivot は各パーツの腰、±2° 以内推奨（最大 ±3°）。境目がわずかに見える可能性あり。

## 推奨の動き（小さめ）
| パーツ | 動き |
|---|---|
| 05_torso（呼吸） | scaleY 1.000〜1.004（腰の pivot）、4秒周期 |
| 07〜09 帯の垂れ布・紐 | ±2〜2.5°、上端 pivot、位相を少しずつずらす |
| 14/17 笠の札 | ±6° |
| 15/16/18 笠の鎖 | ±4° |
| 12_skull（＋笠） | ±0.6° 程度のうなずき |
| 10_arm_R / 11_arm_L_katana | ±0.6° 以内（刀の先は大きく動くので控えめに） |

## テスト動画
skeleton_samurai_sway_test.mp4 / .gif … 4秒ループ。上記の値で揺らしたもの（着物はシェーダー相当の揺れ）。
debug_sway_extreme_x1.5.png … 1.5倍の振幅でも穴がないかの確認用（マゼンタ背景）。
