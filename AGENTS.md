# Agentic SDLC と仕様駆動開発

このリポジトリでは、エージェント主導の SDLC 上で Kiro スタイルの仕様駆動開発を行う。

## ドキュメント言語ポリシー

原則として、`docs/` 配下のドキュメントは日本語で記述する。

対象:

- 仕様書
- 設計書
- DSL 仕様
- アーキテクチャ資料
- メモ
- ガイド

例外:

- 外部 OSS 向けの公開英語 README
- 海外利用者向け資料
- API 仕様など、英語の方が適切なもの

コード中の識別子、DSL キーワード、文法定義、BNF、EBNF などは英語のままでよい。

また、`.kiro/specs/**/design.md`、`research.md`、`task.md`、`tasks.md` はすべて日本語で作成する。

## プロジェクトメモリ

プロジェクトメモリは、ステアリング、仕様メモ、コンポーネント文書のような継続的な指針を保持し、各実行でエージェントが一貫した判断を行うための長期的な情報源である。

- プロジェクト全体の方針は `.kiro/steering/` に置く。アーキテクチャ原則、命名規則、セキュリティ制約、技術選定、API 標準などをここで管理する。
- 機能やライブラリ単位の文脈はローカルの `AGENTS.md` に置く。例として `src/lib/payments/AGENTS.md` には、そのフォルダー固有の前提、API 契約、テスト規約を書く。
- 仕様単位のメモは `.kiro/specs/` 配下に残し、仕様ごとの開発フローを支える。

## プロジェクト構成

### 主要パス

- Steering: `.kiro/steering/`
- Specs: `.kiro/specs/`

### Steering と Specification の役割

**Steering** (`.kiro/steering/`) は、プロジェクト全体に適用される方針や前提を AI に伝えるための領域である。

**Specs** (`.kiro/specs/`) は、個別機能の要求、設計、タスク、検証を形式化するための領域である。

### 有効な仕様の確認

- 進行中の仕様は `.kiro/specs/` を確認する。
- 進捗確認には `$kiro-spec-status {feature}` を使う。

## 開発ガイドライン

- プロジェクトファイルへ書き込む Markdown は、対象仕様で定義された言語に従う。特に `design.md`、`research.md`、`tasks.md`、`requirements.md`、検証レポートは `spec.json.language` に合わせ、このリポジトリでは原則として日本語で記述する。
- このリポジトリでは、GitHub Issue、ブランチ、Pull Request、人間レビュー、CI を前提に開発を進める。
- 1 つの Issue に対して 1 つのブランチと 1 つの Pull Request を作成する。
- Issue に書かれた実装範囲を超える変更はしない。
- 仕様変更が必要な場合は、実装前に Issue または Pull Request で提案する。
- 作業完了のたびに ADR に記載すべき設計判断がないか棚卸しし、必要に応じて `docs/adr/` に ADR 文書を残す。
- すべての実装 Pull Request には、原則として対応するテストを追加する。
- テスト追加が不要な場合は、Pull Request 本文に理由を書く。
- 既存の公開仕様と矛盾する実装をしてはならない。
- Pull Request 本文には、参照した仕様書、満たした受け入れ条件、実行したテストを書く。
- レビュー指摘への対応では、指摘範囲を優先し、無関係なリファクタリングを混ぜない。

詳細は `docs/development-workflow.md`、`docs/testing-strategy.md`、`docs/adr/README.md` を参照する。

## ADR 運用ルール

- 実装、仕様、検証、レビュー対応などの作業完了時には、今回の判断が ADR として残すべき内容かを必ず確認する。
- ADR の対象は、将来の実装やレビューで参照される可能性があるアーキテクチャ判断、技術選定、互換性方針、エラー設計、永続化形式、公開仕様への影響を持つ判断とする。
- ADR が必要な場合は、`docs/adr/` に新しい ADR 文書を作成し、背景、決定内容、代替案、影響、関連 Issue または仕様を記録する。
- ADR が不要な場合でも、完了報告では「ADR 棚卸し済み。新規 ADR 不要」のように判断結果を明示する。
- ADR の詳細な書式や採番は `docs/adr/README.md` と `docs/adr/0000-template.md` に従う。

## 最小ワークフロー

- Phase 0（任意）: `$kiro-steering`、`$kiro-steering-custom`
- Discovery: `$kiro-discovery "idea"`
  複数仕様にまたがる場合は、実行方針を決めて `brief.md` と `roadmap.md` を生成する。
- Phase 1（Specification）:
  - 単一仕様では `$kiro-spec-quick {feature} [--auto]` を使うか、以下を順に実行する。
    - `$kiro-spec-init "description"`
    - `$kiro-spec-requirements {feature}`
    - `$kiro-validate-gap {feature}`（既存コードベース分析が必要な場合のみ）
    - `$kiro-spec-design {feature} [-y]`
    - `$kiro-validate-design {feature}`（設計レビューが必要な場合のみ）
    - `$kiro-spec-tasks {feature} [-y]`
  - 複数仕様では `$kiro-spec-batch` を使い、`roadmap.md` を基準に依存波ごとに並列生成する。
- Phase 2（Implementation）: `$kiro-impl {feature} [tasks]`
  - タスク番号なしでは自律モードとなり、各タスクをサブエージェントで処理し、独立レビューと最終検証まで行う。
  - タスク番号ありでは手動モードとなり、対象タスクを主コンテキストで進めつつ、完了前にレビューを通す。
  - 再検証だけを行う場合は `$kiro-validate-impl {feature}` を使う。
- 進捗確認: `$kiro-spec-status {feature}`

## スキル構成

スキルは `.agents/skills/kiro-*/SKILL.md` に配置する。

- 各スキルは `SKILL.md` を持つディレクトリとして構成する。
- 利用可能なスキル一覧の確認には `/skills` を使う。
- スキルは `$kiro-<skill-name>` で直接呼び出せる。
- `kiro-review` は、レビュー用サブエージェントが使うタスク局所レビュー手順である。
- `kiro-debug` は、デバッグ用サブエージェントが使う root-cause-first の調査手順である。
- `kiro-verify-completion` は、完了主張の前に新しい証拠で確認するための最終ゲートである。
- 対象タスクにスキルが関係する可能性が少しでもあるなら、そのスキルを使う。

## コラボレーションモード（任意）

長めのタスクで実行モードを切り替えたい場合は、`~/.codex/config.toml` で collaboration modes を有効化する。

```toml
[features]
collaboration_modes = true
```

## マルチエージェント（実験的）

利用可能なら、スキル内の独立した調査や検証を並列化するために multi-agent を有効化してよい。

```toml
[features]
multi_agent = true
```

Parallel Research セクションを持つスキルでは、この機能により独立作業の並列実行が可能になる。

## 開発ルール

- 承認フローは Requirements → Design → Tasks → Implementation の 3 段階を基本とする。
- 各段階では人間レビューを前提とし、`-y` は意図的な fast-track の場合にのみ使う。
- Steering は常に最新に保ち、`$kiro-spec-status` と整合していることを確認する。
- ユーザーの指示を優先しつつ、その範囲内では自律的に必要な情報収集、実装、検証まで完結させる。
- 質問は、本当に必要な情報が欠けている場合、または指示の曖昧さが致命的な場合に限る。

## Unity CLI 運用ルール

- `source/extension/unity/` の開発で Unity Editor の状態確認、シーン・GameObject・Prefab・Project Settings・Addressables・メニュー操作、Console 確認、Edit Mode / Play Mode テストを行う場合は Unity CLI を使用する。
- Unity Editor が生成・管理する `.unity`、`.prefab`、`.asset`、`.meta`、`Packages/manifest.json` などを変更する必要がある場合は、原則として Unity CLI 経由の Editor API またはメニュー操作で変更する。テキストを直接編集するのは、Unity CLI では実現できないことを確認した場合に限る。
- Unity CLI を使用する前に接続中の Unity instance と Editor state を確認し、複数 instance がある場合は対象を明示的に選択する。
- Unity のスクリプトまたは package を変更した後は、Unity CLI でコンパイル完了を待ち、Console の error と warning を確認してからテストを実行する。
- Unity 拡張の検証結果には、使用した Unity version、対象 instance、実行した Unity CLI コマンド、Edit Mode / Play Mode テストと結果を含める。

## Steering の読み込み設定

- `.kiro/steering/` 全体をプロジェクトメモリとして扱う。
- 既定ファイルは `product.md`、`tech.md`、`structure.md` である。
- カスタムファイルも利用でき、管理には `$kiro-steering-custom` を使う。
