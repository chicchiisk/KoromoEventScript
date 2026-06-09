# Project Structure

## Organization Philosophy

リポジトリは「仕様」「実装」「テストデータ」「自動テスト」を明確に分ける構成を取る。実装コードは責務ごとのレイヤで分離し、テストはそれと対応する名前空間・ディレクトリで並走させる。新しいコードは既存レイヤのどこに属するかを先に決め、その境界をまたぐ責務は増やしすぎない。

## Directory Patterns

### 仕様と開発ルール

**Location**: `docs/`, `docs/spec/`, `docs/adr/`  
**Purpose**: 言語、CLI、ランタイム、開発運用の source of truth を置く  
**Example**: `docs/spec/kes-language-spec.md`, `docs/testing-strategy.md`

### 実装本体

**Location**: `source/cli/KoromoEventScript.Cli/`  
**Purpose**: 現在の主実装である CLI と言語処理系を責務別ディレクトリで管理する  
**Example**: `Parsing/` は AST 構築、`Semantics/` は名前解決と型検査、`Execution/` は headless VM

### 将来拡張の受け皿

**Location**: `source/runtime/`, `source/extension/`  
**Purpose**: Windows ランタイムや各種拡張の実装先を先に分け、CLI 中心実装と混線させない  
**Example**: `source/extension/vscode/`, `source/extension/unity/`

### テスト

**Location**: `tests/KoromoEventScript.Cli.Tests/`  
**Purpose**: 実装レイヤに対応する NUnit テストをカテゴリ別に配置する  
**Example**: `Parsing/KeParserTests.cs`, `Execution/HeadlessVmExecutionTests.cs`

### テストデータとスナップショット

**Location**: `testdata/`  
**Purpose**: 入力ファイル、最小プロジェクト、診断・IR の期待値をコードから分離して管理する  
**Example**: `testdata/snapshots/ir/`, `testdata/projects/`

### 仕様単位の作業記録

**Location**: `.kiro/specs/`  
**Purpose**: feature ごとの requirements / design / tasks / 実装メモを残す  
**Example**: `.kiro/specs/kes-headless-vm-full-opcodes/`

## Naming Conventions

- **Files**: C# 実装とテストは PascalCase を基本にする
- **Types**: ドメインや機能が分かる名詞中心の PascalCase
- **Tests**: `{Subject}Tests.cs` とし、メソッド名は `Condition_ExpectedBehavior` 形式を優先する

## Import Organization

```csharp
using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Execution;
using KoromoEventScript.Cli.Parsing;
```

- 名前空間は `KoromoEventScript.Cli.*` を基準に機能単位で切る
- 同一レイヤ内の参照は明示的 namespace import を使い、曖昧な global alias は増やさない
- テストは対象実装 namespace を直接参照し、テスト専用 helper は `tests/.../Execution/HeadlessVmTestHelper.cs` のように近接配置する

## Code Organization Principles

- `Commands` は CLI 入力境界、`Build` はファイル収集と前処理、`Parsing` は構文、`Semantics` は意味、`Compilation` は IR、`Execution` は VM 実行を担当する
- 実装の責務が増えたときは既存ファイルを肥大化させるより、補助クラスや state model を導入して境界を保つ
- テストは実装と同じ粒度で置き、公開契約を compiler 駆動テストと synthetic テストの両方で支える
- `bin/` と `obj/` は生成物であり、構造パターンの基準には含めない
