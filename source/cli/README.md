# KES CLI

`kes`はKoromoEventScriptプロジェクトの作成、検証、ビルド、実行、配布物生成を行うCLIです。

## 対象環境

- Windows 10またはWindows 11
- x64

配布zipは.NETランタイムを内包するため、.NET SDKの別途インストールは不要です。
リリース成果物はGitHub Releaseへ`v<version>`タグの作成時に自動登録されます。

## インストール

1. `kes-<version>-win-x64.zip`を展開する。
2. 展開先を`PATH`へ追加するか、`kes.exe`を直接実行する。
3. `kes --version`で導入を確認する。

## 基本操作

```text
kes init MyGame
kes build MyGame --target unity
kes publish MyGame --target windows
```

コマンドの全オプションは`kes --help`または[CLI仕様書](https://github.com/chicchiisk/KoromoEventScript/blob/main/docs/spec/cli-tool-spec.md)を参照してください。

`kes run`と`kes publish --target windows`はKoromoEventScript Windows Runtimeを必要とします。
