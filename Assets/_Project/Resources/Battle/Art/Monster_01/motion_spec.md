# 大口の敵（左右反転版）モーション仕様 / Unity実装メモ

座標はすべて **345×520 キャンバスのピクセル（左上原点・y下向き）**。Unityでは `x/PPU`, `-(y)/PPU` に変換し、各スプライトのPivotをここで指定した点に設定する（Sprite Editor → Custom Pivot, Pixels）。
各PNGは同じキャンバスサイズで位置が揃っているので、全パーツをPivot設定後に同じ位置に置けば組み上がる。

## L/R 対応（反転後）
_L/_R は **反転後の画面上の左右**。
| 反転版 | 元ファイル |
|---|---|
| 05_arm_L（画面左の腕） | 06_arm_R を反転 |
| 06_arm_R（画面右の腕） | 05_arm_L を反転 |
| 07_earring_L | 08_earring_R を反転 |
| 08_earring_R | 07_earring_L を反転 |
| 他のパーツ | 同名を反転 |
Pivot は x' = W−1−x（W=345）で反転済み。

## 階層 / 描画順（奥→手前）
```
Root
├ 10_shadow      (order 0)
├ 09_legs        (order 1)
└ 01_body_skin   (order 5)   ← 呼吸スケール
   ├ 02_mouth_inner (2)
   ├ 04_teeth_lower (3)
   ├ 03_teeth_upper (4)
   ├ 05_arm_L (6)  06_arm_R (7)
   └ 07_earring_L (8)  08_earring_R (9)
```
SortingGroup を Root に付け、子の Order in Layer を上の番号に。

## Pivot
| パーツ | Pivot (px) |
|---|---|
| 01_body_skin（足元） | [179, 460] |
| 02_mouth_inner | [174, 224] |
| 03_teeth_upper | [174, 160] |
| 04_teeth_lower（あご軸） | [174, 232] |
| 05_arm_L 肩/ひじ/手 | [132, 226] / [78, 261] / [49, 230] |
| 06_arm_R 肩/ひじ/手 | [216, 226] / [267, 261] / [299, 230] |
| 07_earring_L 吊り点 | [71, 140] |
| 08_earring_R 吊り点 | [286, 137] |
| 09_legs | [179, 408] |
| 10_shadow | [179, 475] |

## 待機モーション（ループ 4.8s）
`s(t,T,φ)=sin(2π(t−φ)/T)`, `s01=0.5−0.5cos(2π(t−φ)/T)`（0→1→0）
| 対象 | 値 | 周期 | 位相/遅れ | イージング |
|---|---|---|---|---|
| 胴体 呼吸 | scaleY 1.00→1.03, scaleX 1.00→1.015（足元軸） | 2.4s | 0 | s01 |
| 上の歯 | y −1.5px（上へ） | 1.2s | 0 | s01 |
| 下の歯 | y +4px（下へ） | 1.2s | 0 | s01 |
| 口の中 | scaleY 1→1.03 | 1.2s | 0 | s01 |
| 左腕 | 肩回転 ±9° | 1.2s | 0 | sin |
| 右腕 | 肩回転 ∓9° | 1.2s | +0.3s | sin |
| ひじ（任意） | ±5°、肩より0.1s遅れ | 1.2s | +0.1s | sin |
| 耳飾り L/R | ±6° 振り子 | 1.2s | 0.15s遅れ | sin |
| 影 | scaleX 1→1.04, α 1→0.85（呼吸と同期） | 2.4s | 0 | s01 |
回転の符号: +＝画面上で反時計回り（Unityの z 回転と同じ）。

## 攻撃（かみつき）0.4s
| t | 上の歯 y | 下の歯 y | 口の中 scaleY | 胴体 scale / offset | 左腕 | 右腕 |
|---|---|---|---|---|---|---|
| 0.00 | 0 | 0 | 1.00 | 1.00 / (0,0) | 0° | 0° |
| 0.12 | −10px | +18px | 1.12 | 1.08 / (−6,+4)px | +25° | −15° |
| 0.28 | −10px | +4px（かみつき） | 1.03 | 1.04 / (−3,+2)px | +15° | −8° |
| 0.40 | 0 | 0 | 1.00 | 1.00 / (0,0) | 0° | 0° |
0→0.12 ease-out、0.12→0.28 速く閉じる、0.28→0.4 ease-in-out で待機へ。腕は左下（プレイヤー側）へ伸ばす。

## Unity 実装案
- 1つの `MonoBehaviour`（例 `BigMouthIdle`）に、パーツごとの Transform 参照と `motion_params.json` と同じ数値を持たせ、`Update()` で `Time.time` から上式で localScale / localPosition / localEulerAngles を設定（初期値に加算）。
- px→Unity単位は `/ PPU`、y は符号反転（画面下向き +y → Unity では −y）。
- 攻撃は `AnimationCurve` かコルーチンで 0.4s 補間、終了後に待機へ戻す。
- ひじを曲げたい場合のみ、05/06 の腕に **Unity 2D Animation の Sprite Skin**（肩→ひじ→手 の3ボーン、上記座標）を設定し、ひじボーンを 0.1s 遅れで ±5° 回す。未使用なら腕全体の回転だけで十分。

```csharp
using UnityEngine;
public class BigMouthIdle : MonoBehaviour {
  public Transform body, mouth, teethU, teethL, armL, armR, earL, earR, shadow;
  public float ppu = 100f;
  Vector3 tU0, tL0;
  void Start(){ tU0 = teethU.localPosition; tL0 = teethL.localPosition; }
  static float S(float t,float T,float ph=0)=>Mathf.Sin(2*Mathf.PI*(t-ph)/T);
  static float S01(float t,float T,float ph=0)=>0.5f-0.5f*Mathf.Cos(2*Mathf.PI*(t-ph)/T);
  void Update(){
    float t=Time.time, br=S01(t,2.4f), j=S01(t,1.2f);
    body.localScale=new Vector3(1+0.015f*br,1+0.03f*br,1);
    mouth.localScale=new Vector3(1,1+0.03f*j,1);
    teethU.localPosition=tU0+new Vector3(0, 1.5f*j/ppu,0);   // 上へ
    teethL.localPosition=tL0+new Vector3(0,-4f*j/ppu,0);     // 下へ
    armL.localRotation=Quaternion.Euler(0,0, 9*S(t,1.2f));
    armR.localRotation=Quaternion.Euler(0,0,-9*S(t,1.2f,0.3f));
    earL.localRotation=Quaternion.Euler(0,0,6*S(t,1.2f,0.15f));
    earR.localRotation=Quaternion.Euler(0,0,6*S(t,1.2f,0.15f));
    shadow.localScale=new Vector3(1+0.04f*br,1,1);
  }
}
```
（各 Transform の原点＝上表の Pivot になるよう、スプライトの Pivot を設定しておくこと）

## lean_variant（任意）
`lean_variant/` は上半身（胴体・口・歯・腕・耳飾り）を腰（脚の軸）中心に +6°（頭が画面左へ傾く）回転し、口と歯を (−3,+2)px ずらした版。キャンバスは左右40px拡張（425×520）。Pivotは `lean_variant/parts_pivots.json` と `lean_variant/motion_params.json` を使用。
