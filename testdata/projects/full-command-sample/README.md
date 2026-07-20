# 全STLサンプル

このプロジェクトは、KES言語STL仕様の公開命令とflow構文を、Unity上で確認できるようにまとめたサンプルである。
実行位置を保存済みsnapshotへ移す`load`だけは自動周回から除外している。

- `core`: `print`, `array_len`, `str_len`, `range`, `number_to_string`, `bool_to_string`, `assert`
- `scene`: `rt_back`, `rt_front`, `bg`, `trans`, `camera_autofocus`
- `actor`: `standby` (`cast`), `show`, `hide`, `face`, `move`, `action_jump`
- `text`: `vo`（自動・明示）, `vf`, `p`, `r`, `l`, `cm`, `wait_click`
- `audio`: `bgm`, `bgm_stop`, `se`, `se_stop`, `se_stop_all`, `voice_stop`
- `flow`: `label`, `jump`, `select`, `case`
- `state`: `save`, `autosave`, `mark_read`, `is_read`
- `system`: `wait`, `set_auto`, `set_skip`, 全設定setter、`get_config`, 全ゲーム変数setter、`get_param`
- シナリオ構文: `say`, `nar`

Unity向け成果物の生成例:

```powershell
dotnet run --project source/cli/KoromoEventScript.Cli -- build testdata/projects/full-command-sample --target unity
Copy-Item testdata/projects/full-command-sample/build/unity/* source/extension/unity/SampleProject/Assets/Scenario -Recurse -Force
```

## 立ち絵・アニメーションテスト

`events/actor_animation_test.kc` は立ち絵の表示とアニメーションに特化した目視確認用シナリオである。
通常の entry event から再生し、最初の選択肢で「立ち絵・アニメーションテスト」を選ぶと開始できる。
KesManager の Start Event Override に `actor_animation_test` または `events/actor_animation_test` を指定して直接開始してもよい。

次の項目を順番に自動実行する。

- Riku の通常表示と全表情差分
- 左・中央・右への時間付き移動と即時移動
- `action_jump`
- `hide` 後の再表示とバストアップ指定
- Riku、Amane、Noa の3人同時表示
- 中央で重ねた場合の `layer` による描画順
- 3人それぞれのジャンプ
- 一括表示と一括非表示

各確認区間は Unity Console に `actor-animation-test:` で始まるログを出力し、目視できるよう短い待機時間を挟む。

`load` は復元先へ実行位置を移す命令であり、現在の自動周回サンプルには含めていない。
