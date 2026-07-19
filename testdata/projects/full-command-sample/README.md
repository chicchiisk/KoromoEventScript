# 全STLサンプル

このプロジェクトは、KES言語STL仕様の全公開命令とflow構文を、Unity上で確認できるようにまとめたサンプルである。

- `core`: `print`, `array_len`, `str_len`, `range`, `number_to_string`, `bool_to_string`, `assert`
- `scene`: `rt_back`, `rt_front`, `bg`, `trans`, `camera_autofocus`
- `actor`: `standby` (`cast`), `show`, `hide`, `face`, `move`, `action_jump`
- `text`: `vo`（自動・明示）, `vf`, `p`, `r`, `l`, `cm`, `wait_click`
- `audio`: `bgm`, `bgm_stop`, `se`, `se_stop`, `se_stop_all`, `voice_stop`
- `flow`: `label`, `jump`, `select`, `case`
- `state`: `save`, `load`, `autosave`, `mark_read`, `is_read`
- `system`: `wait`, `set_auto`, `set_skip`, 全設定setter、`get_config`, 全ゲーム変数setter、`get_param`
- シナリオ構文: `say`, `nar`

Unity向け成果物の生成例:

```powershell
dotnet run --project source/cli/KoromoEventScript.Cli -- build testdata/projects/full-command-sample --target unity
Copy-Item testdata/projects/full-command-sample/build/unity/* source/extension/unity/SampleProject/Assets/Scenario -Recurse -Force
```
