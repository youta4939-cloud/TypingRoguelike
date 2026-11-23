# アセット／データの置き方（指針）

更新: 2026-11-23  
既存の `Assets/_Project` を壊さず、足りない箱だけ足す想定。

## 推奨（ゲーム本体）

```
Assets/_Project/
  Scripts/           … 既存のまま
  Resources/         … 実行時 Load するもの（既存）
  Data/              … ScriptableObject の正（敵・スキル等、既存）
  Art/               … まだ薄ければ以下を切る
    Characters/      … 立ち絵・顔・拠点スロット用（キャラID/スロット）
    Enemies/         … 敵スプライト（IDごと）
    Backgrounds/     … 拠点・イベント背景
    UI/              … 枠・アイコン（生成ラフは置かない）
  Audio/
    SE/              … ui_confirm, ui_cancel, ui_move, ui_deny …
    BGM/
    Typing/          … 打鍵・ミス（既存 TypingAudio と接続）
  Prefabs/ or Resources/UI/  … 既存UIプレハブ
```

## 推奨（ドキュメント・制作）

```
Docs/
  ProjectMgmt/       … 進捗（本フォルダ）
  Scenario/          … 物語正本（既存）
  （UI_* は案のアーカイブ。採用したら ProjectMgmt か仕様に一行リンク）

tools/ui-atlas/      … UI台帳（リポジトリ直下・既存）
```

## キャラ画像の命名（後でやるとき）

例: `Art/Characters/{charId}/hub_{slot}.png`  
`hub_chair.png` / `hub_window.png` / `hub_floor.png`  
戦闘用は `battle_portrait.png` など用途をファイル名に含める。

## 設定データ

| 内容 | 置き場の考え方 |
| --- | --- |
| 敵ステ・行動 | `Data/Enemies` + Catalog（既存方針） |
| 刻印 | Skill ScriptableObject／Resources |
| 出来事 | JSON or SO + 文言テキスト |
| バランス数値 | 正本に「課題」と書き、データ側を正にする |

Illustrious の生成ラフは `Docs` や制作用フォルダに置き、**採用した透過PNGだけ** `Art/` へ入れる。
