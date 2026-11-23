# 採用した拠点UIのUnity実装

`RunOverlayUI.ShowHub` は `HubOfficeView` を構築する。旧 `Hub.prefab` のボタン一覧は使用しない。

## 実装した内容

- アニメ調の事務所背景と既存キャラを別の画像として表示。背景は画面を覆い、キャラは縦横比を維持。
- 左約半分のメニュー、明朝の出発見出し、角の欠けた準備タブ、薄い漢字、朱色の選択表示。
- 魔石・貯金・中断層・探索記録はセーブの値を使用。
- 装備・倉庫と商店・開発は既存画面へ接続。
- パーティからステータスと修復屋を選択。施設からの戻り表記を「拠点」に統一。
- 塔の入口で階段またはエレベーターを選択。既存の中断地点再開も入口に保持。
- エレベーターは18個ずつページ表示。解放層だけ数字を出して有効化し、未解放は空白の無効ボタン。
- 行き先選択と出発を分離。「閉」で扉の演出後に既存の出発コールバックを一度だけ呼ぶ。「開」や戻るで未確定の移動を中止。
- 「設定・セーブ」は実際の音量調整と既存セーブ保存。音量はセッション内のみ。
- ポップアップ中は拠点側の操作を無効化。Escで戻り、既存のInput System UIでキーボード選択可能。

## 素材

`Assets/_Project/Resources/UI/HubOffice/Background.png` は採用した `office-background-anime-v4.png`。
`Character.png` は既存 `Chara01/battle_portrait_chara01_1200x1600.png` のコピー。
キャラの描き直しは行っていない。キャラランダム表示・メニューごとのポーズ変更は、素材差分がまだないため実装していない。

## 確認

- `dotnet build TypingRoguelike/TypingRoguelike.csproj --no-restore` によるコンパイル。
- `dotnet run --project output/hub-check/HubCheck.csproj` による、実装と共通の選択モデルの9検証。
- Unity EditMode用 `HubOfficeTests.cs` に、未解放・出発一回制限・開による解除・レイアウト・修復屋への導線テストを追加。
- Unityを別途バッチ起動することは、既存エディターがプロジェクトを使用中のため不可。Play Modeでの画面確認は別途必要。

Unityでスクリプトと画像のインポートが終わったら、SampleSceneを再生して拠点を開く。
