# 敵の着弾エフェクト（Strike FX v1）組み込みメモ — Cursor向け

対象: `EnemyStrikeEffectKind`（Slash / Thrust / Bite / Claw / Magic）。`None` はエフェクトなし（局所フラッシュと揺れだけ）。
今の `EnemySpritePairStrikeFx.PlayStrike` が出している仮の帯の Image を、この連番に差し替える。

## 1. 中身

```
unity/
  slash/  thrust/  bite/  claw/  magic/
    frames_mask/       strike_<kind>_mask_00.png …      ← 着色用（グレー）。1枚ずつのスプライト
    frames_BloodRed/   strike_<kind>_BloodRed_00.png …  ← 色付き済み（確認用・そのまま使ってもよい）
    sheets/            strike_<kind>_mask_sheet.png
                       strike_<kind>_<Palette>_sheet.png（6パレット全部、色付き済み）
    strike_<kind>.json ← コマ数・fps・着弾コマ・推奨サイズなど
  ramps/   StrikeRamp_<Palette>.png（256×4、グラデーションマップ）
  shader/  StrikeFxRamp.shader（UI用。mask＋ramp で着色）
  strike_fx_index.json（5種の json をまとめたもの）
```

| 種類 | コマ数 | 長さ | 1コマ | 着弾コマ | 推奨表示サイズ（敵の高さ Eh 比） |
|---|---|---|---|---|---|
| slash | 13 | 0.43s | 512 | 1 | 1.45 Eh |
| thrust | 12 | 0.40s | 512 | 1 | 1.53 Eh |
| bite | 14 | 0.47s | 512 | 3（顎が閉じる瞬間） | 1.21 Eh |
| claw | 13 | 0.43s | 512 | 1 | 1.33 Eh |
| magic | 17 | 0.57s | 768 | 7（紋が潰れて弾ける瞬間） | 1.65 Eh |

すべて 30fps、ループなし、ピボットはフレーム中心（= 着弾点に置く）。数値の正は各 json。

## 2. インポート設定

- どちらか片方でよい:
  - **個別PNG（おすすめ・簡単）**: `frames_*` を Texture Type = Sprite (2D and UI)、Sprite Mode = Single。
  - **シート**: `sheets/*` を Sprite Mode = Multiple、Sprite Editor → Slice → Grid By Cell Size（512×512、magic は 768×768）。並びは左上から右へ、行は上から下。最後の行の空きセルは使わない。
- 共通: Mesh Type = Full Rect、Alpha Is Transparency = ON、Mipmap = OFF、Compression は High Quality（またはなし。帯がにじむのを避ける）、Max Size はシートの実寸以上（magic のシートは 3840×3072 なので 4096）。
- `frames_mask` / `*_mask_sheet` は **sRGB (Color Texture) = OFF**（R の値を熱としてそのまま読むため）。
- `ramps/*` は Wrap = Clamp、Filter = Bilinear、Mipmap = OFF、sRGB = ON。

## 3. ブレンドと着色

- ブレンドは**普通のアルファ（Blend SrcAlpha OneMinusSrcAlpha）**で作ってある。加算は不要（加算にすると墨の黒い部分が消えて、ぼやけた光だけになる）。
- 着色は2通り。どちらでもよい。
  1. **シェーダーで着色（推奨・軽い）**: `StrikeFxRamp.shader` のマテリアルを Image に付け、`_RampTex` にパレットの ramp を入れる。スプライトは `frames_mask`。パレットごとにマテリアルを6個作っておくか、`material.SetTexture("_RampTex", …)` で切り替える（UI の共有マテリアルを書き換えないよう、パレットごとに別マテリアル推奨）。
  2. **色付き済みを使う（シェーダー不要）**: `sheets/strike_<kind>_<Palette>_sheet.png` をパレットごとに読み込んで、普通の UI マテリアルで出す。テクスチャは6倍になる。
- ※ `Image.color` に色を掛けるだけの単純な着色は非推奨（白い芯が消えて平たくなる）。

パレット（spec §9）と ramp の対応は名前どおり: Neutral / BloodRed / PoisonGreen / CursePurple / IceBlue / HolyGold。

## 4. PlayStrike の流れ

```csharp
// 例: EnemySpritePairStrikeFx.PlayStrike(kind, palette, intensity, impactPoint, enemyHeightEh)
if (kind == EnemyStrikeEffectKind.None) { /* 局所フラッシュ(70%)と揺れだけ */ return; }
var clip = strikeClips[kind];            // json から: frames[], fps=30, impactFrame, displaySizeEh
float scale = intensity switch { Small => 0.67f, Large => 1.44f, _ => 1.0f };
float size  = clip.displaySizeEh * enemyHeightEh * scale;   // 正方形の辺の長さ（UI単位）
// 開始時刻 = 着弾時刻 - impactFrame / fps   （impactFrame のコマが着弾にぴったり重なる）
// → bite は 0.10s、magic は 0.23s 早く出し始める。slash/thrust/claw は 0.033s。
// 1/fps ごとに sprite を差し替え、最後のコマのあと非表示にする（ループしない）。
// 時間は戦闘のタイムスケールに従う（ヒットストップ中は止める）。
```

- 置く位置: 着弾点（spec の局所フラッシュ中心 = HOME から左 0.02 Eh・上 0.44 Eh 付近）にフレーム中心を合わせる。参考の 1664×929 画面での中心は json の `reference_center_px_on_1664x929`。
- 向き: 敵が右側・プレイヤー（左）を攻撃する前提で描いてある。左右反転が必要な配置なら `localScale.x = -1` でよい（magic と bite は左右対称に近い）。
- 局所フラッシュ・画面の端の光・揺れは今まで通りコード側（spec §8.3, §8.4）。magic は端の光なし・揺れ 60%（spec のとおり）。

## 5. UI に重なるところは 35%（spec の約束）

一番簡単なのは**同じアニメを2枚同時に再生**するやり方:

1. **A: 敵レイヤー（UIの後ろ）** に不透明度 100% で1枚。
2. **B: UIより前** に不透明度 **35%**（`Image.color.a = 0.35`）で同じコマをもう1枚。

背景の上では A が見え、UIパネルの上では B の 35% だけが見えるので、マスクを作らずに spec と同じ見た目になる。背景の上では A と B が重なって少し濃くなるが、問題ない程度。

## 6. メモ

- 1コマ目から最後のコマまでで形が自然に崩れて消えるように作ってあるので、フェードアウトを足さなくてよい。
- サイズを大きくするとき（Large ×1.44）は、ぼやけが気になる場合のみ 1.5 倍解像度の版を作るので言ってください（いまは 512 / 768）。
- 元の生成スクリプト: `/workspace/art/vfx/strike_v1/src/`（fx_<kind>.py）。数値を変えて作り直せる。
