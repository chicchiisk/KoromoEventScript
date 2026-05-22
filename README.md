# KoromoEventScript

KoromoEventScript (KES) は、RPG・ADV・ノベルゲーム向けのシナリオ DSL と、その開発・実行環境です。

シナリオライターが読み書きしやすい脚本寄りの記法を保ちながら、パーサ、LSP、ローカライズ、Git 管理、Unity / Unreal Engine 組み込みまでを見据えたワークフローを提供することを目指しています。

> 現在は初期実装準備段階です。仕様、開発ルール、テストデータを先に整備し、CLI と Windows ランタイムから MVP を構築していきます。

## 目指すもの

- `.kc` ファイルでイベント本文、演出命令、分岐、変数、マクロなどを記述する
- `.kel` ファイルでイベント一覧、遷移、エントリポイントを管理する
- `kes` CLI で検証、ビルド、実行、配布物生成を行う
- VS Code 拡張で編集支援、診断、補完、フォーマットを提供する
- Windows 単体ランタイム、Unity、Unreal Engine から KES を実行できるようにする

## 想定ワークフロー

1. `.kc` にシナリオ本文と演出命令を書く
2. `.kel` にイベントの流れとエントリポイントを定義する
3. `kes build` で構文・参照・設定を検証し、実行用の `.klib` を生成する
4. `kes run` で Windows 単体ランタイムから動作確認する
5. `kes publish --target unity` または `kes publish --target unreal` でゲームエンジン向け成果物を生成する

## リポジトリ構成

```txt
docs/
    spec/                  仕様書
    development-workflow.md
    testing-strategy.md
    task-breakdown.md
testdata/
    ke/                    .kc テスト入力
    kel/                   .kel テスト入力
    projects/              CLI 統合テスト用プロジェクト
    snapshots/             golden test / 診断期待値
```

実装コードは MVP ロードマップに従い、`source/cli/` と `source/runtime/` から配置していく予定です。

## 開発ドキュメント

- [全体仕様](docs/spec/overview.md)
- [言語仕様](docs/spec/kes-language-spec.md)
- [標準ライブラリ仕様](docs/spec/kes-language-stl-spec.md)
- [CLI 仕様](docs/spec/cli-tool-spec.md)
- [Windows ランタイム仕様](docs/spec/windows-runtime-spec.md)
- [VS Code 拡張仕様](docs/spec/vscode-ext-spec.md)
- [Unity 組み込み仕様](docs/spec/unity-runtime-spec.md)
- [Unreal Engine 組み込み仕様](docs/spec/unreal-runtime-spec.md)
- [開発ワークフロー](docs/development-workflow.md)
- [テスト戦略](docs/testing-strategy.md)
- [MVP 実装ロードマップ](docs/task-breakdown.md)
- [テストデータ](testdata/README.md)

## 開発ステータス

現在の優先範囲は、CLI、言語処理系、Windows 単体ランタイムの MVP です。
VS Code 拡張、Unity 組み込み、Unreal Engine 組み込みは仕様を先行して管理し、MVP 後に本格実装します。

## ライセンス

このリポジトリのライセンスは [LICENSE](LICENSE) を参照してください。
