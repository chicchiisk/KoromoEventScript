# Technology Stack

## Architecture

現在の実装は C# / .NET を用いた単一 CLI アプリケーションを中心に進んでいる。構成はレイヤ分離寄りで、`Build`、`Parsing`、`Semantics`、`Compilation`、`Execution`、`Commands` が責務ごとに分かれ、`KlibDocument` と headless VM が処理系の中核境界を担う。

将来の Windows ランタイムや各種拡張は `source/runtime/` と `source/extension/` に分離して置く前提だが、steering 上の技術判断はまず CLI と言語処理系の安定実装を優先する。

## Core Technologies

- **Language**: C#
- **Framework**: .NET SDK style projects
- **Runtime**: .NET 10 (`net10.0`)

## Key Libraries

- **NUnit**: テストフレームワークの標準。新規 C# テストも原則 NUnit を使う
- **Microsoft.NET.Test.Sdk**: `dotnet test` 実行基盤
- **coverlet.collector**: カバレッジ収集

## Development Standards

### Type Safety

- `Nullable` を有効化し、null 安全性を標準とする
- `ImplicitUsings` を有効化しつつ、境界型やドメイン型は明示的な名前で扱う
- 値表現や VM 状態は `object` のまま拡散させず、専用 record / model に寄せる

### Code Quality

- 公開仕様と矛盾する実装を避け、実装前後で `docs/spec/` と関連 Issue を確認する
- 変更は Issue 単位で閉じ、無関係なリファクタリングを混ぜない
- 長期的な設計判断は必要に応じて `docs/adr/` に昇格する

### Testing

- 実装 PR には原則テストを追加する
- `dotnet test` で再現できる自動テストを優先する
- parser / semantic / CLI / golden / VM / save-state を別観点で固定し、仕様回帰を防ぐ

## Development Environment

### Required Tools

- .NET SDK 10 系
- PowerShell 環境での `dotnet` と `git`

### Common Commands

```bash
# Build: dotnet build KoromoEventScript.slnx
# Test: dotnet test tests/KoromoEventScript.Cli.Tests/KoromoEventScript.Cli.Tests.csproj
# Focused VM test: dotnet test tests/KoromoEventScript.Cli.Tests/KoromoEventScript.Cli.Tests.csproj --filter "FullyQualifiedName~HeadlessVm"
```

## Key Technical Decisions

- `.klib` を共通の中間表現として扱い、parser / semantic / compiler / runtime の接点を明確に保つ
- UI に依存する前に headless VM と save/load を整え、実行意味を自動テストで固定する
- 仕様先行の開発を採り、実装の正しさは docs・snapshot・診断テスト・VM テストの組み合わせで担保する
