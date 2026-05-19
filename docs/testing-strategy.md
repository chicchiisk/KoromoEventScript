# テスト戦略

このドキュメントは、KoromoEventScript の実装品質を保つためのテスト方針を定義する。

本プロジェクトでは、AI が Issue 単位で実装し、人間が Pull Request をレビューする。
そのため、テストは AI の実装ミス、仕様とのズレ、既存機能の退行を検出するための主要な安全装置として扱う。

## 基本方針

- 実装 PR には、原則として対応するテストを追加する。
- 仕様に基づく受け入れ条件は、できる限り自動テストへ落とし込む。
- 診断コード、終了コード、生成物の形式は、スナップショットまたは golden test で固定する。
- UI や描画のテストは、VM や状態管理をヘッドレスに分離してから追加する。
- テスト追加が不要な場合は、PR 本文に理由を書く。

## C# テストフレームワーク

C# のテストフレームワークには NUnit を使用する。

新しい C# テストプロジェクトを追加する場合は、原則として次のパッケージを使用する。

- `NUnit`
- `NUnit3TestAdapter`
- `Microsoft.NET.Test.Sdk`

テストは `dotnet test` で実行できる構成にする。
xUnit や MSTest は、既存資産の移行など明確な理由がある場合を除き使用しない。

## テスト分類

| 分類 | 対象 | 目的 |
|---|---|---|
| Lexer test | `.ke` / `.kel` の字句 | トークン化の退行を防ぐ |
| Parser test | AST / 構文木 | 文法の解釈を固定する |
| Diagnostic test | エラー、警告 | 診断コード、位置、メッセージを検証する |
| Semantic test | 名前解決、型検査 | コンパイルエラーを仕様通り検出する |
| Golden test | `.ke` から `.k` への変換 | 中間表現の生成結果を固定する |
| VM test | `.k` の実行 | 分岐、変数、選択肢、テキスト進行を検証する |
| CLI integration test | `kes` コマンド | 終了コード、標準出力、成果物を検証する |
| Manifest test | `manifest.json` | ランタイム入力契約を固定する |
| LSP test | VS Code 診断、補完、定義ジャンプ | 編集支援の退行を防ぐ |
| Runtime state test | セーブ、ロード、入力、音声状態 | 描画に依存しないランタイム挙動を検証する |

## 優先順位

初期開発では、次の順でテスト基盤を整える。

1. Lexer / Parser test
2. Diagnostic test
3. CLI integration test
4. Golden test
5. VM test
6. Manifest test
7. LSP test
8. Runtime state test

Windows Runtime の描画テストは、言語処理系、VM、manifest 生成が安定してから追加する。

## Testdata 構成

テスト入力は `testdata/` 配下に配置する。
テストコードに直接長い KES ソース文字列を埋め込まず、可能な限りファイルとして管理する。

```txt
testdata/
    ke/
        valid/
        invalid/
    kel/
        valid/
        invalid/
    projects/
        minimal/
    snapshots/
        diagnostics/
        ir/
        manifest/
```

| パス | 内容 |
|---|---|
| `testdata/ke/valid/` | 正常な `.ke` 入力 |
| `testdata/ke/invalid/` | 構文エラーまたはコンパイルエラーを含む `.ke` 入力 |
| `testdata/kel/valid/` | 正常な `.kel` 入力 |
| `testdata/kel/invalid/` | 不正な `.kel` 入力 |
| `testdata/projects/minimal/` | CLI 統合テスト用の最小プロジェクト |
| `testdata/snapshots/diagnostics/` | 診断出力の期待値 |
| `testdata/snapshots/ir/` | `.k` または IR の期待値 |
| `testdata/snapshots/manifest/` | manifest の期待値 |

## 診断テスト

診断テストでは、最低限次の項目を検証する。

- 診断レベル
- 診断コード
- ファイルパス
- 行
- 列
- メッセージ

診断メッセージは人間が読むための出力であり、変更時の影響が大きい。
文言を変更する場合は、仕様書または PR 本文で意図を説明する。

## Golden Test

Compiler や manifest 生成の出力は golden test で検証する。
期待値ファイルはレビュー対象に含める。

Golden test を更新する場合は、PR 本文に次を記載する。

- なぜ期待値が変わるのか
- 仕様変更か、実装修正か
- 既存入力への互換性影響

## CLI 統合テスト

CLI 統合テストでは、実行コマンド、終了コード、標準出力、標準エラー出力、生成ファイルを検証する。

優先して検証するコマンドは次の通り。

```txt
kes --version
kes --help
kes init
kes build --check-only
kes build
kes clean --dry-run
kes publish --target windows
```

終了コードは `docs/spec/cli-tool-spec.md` の定義に合わせる。

## PR ごとの必須確認

AI は PR 作成前に、Issue の必須テストを実行する。
実装がまだ存在しない領域では、最低限 Markdown lint と関連ドキュメントの確認を行う。

PR 本文には、次を記載する。

- 実行したコマンド
- 成功または失敗の結果
- 失敗した場合の理由
- 未実行の場合の理由

## CI の役割

CI は、Pull Request ごとに自動実行する。
初期段階では、実装が存在しない場合にスキップできる構成とする。

実装が追加された後は、次のチェックを必須化していく。

- Markdown lint
- .NET build
- .NET test
- Node.js test
- format check
- snapshot / golden test

## 受け入れ条件との対応

Issue の受け入れ条件は、可能な限りテスト名と対応させる。

例:

| 受け入れ条件 | 対応テスト |
|---|---|
| `jump #missing` が `KES2xxx` を出す | `UndefinedJumpTargetReportsDiagnostic` |
| 正常なタグ参照ではエラーにならない | `DefinedJumpTargetDoesNotReportDiagnostic` |
| `kes build --check-only` が構文エラーで終了コード3を返す | `BuildCheckOnlyReturnsSyntaxErrorExitCode` |

この対応を PR 本文に書くことで、人間レビュー時に確認しやすくする。
