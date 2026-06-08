# Requirements Document

## Introduction

この仕様では、KoromoEventScript の CLI テストスイートが代表的な `.ke` 入力から得られる IR 生成結果を golden test として固定し、コンパイラ変更時に期待差分を人間がレビューしやすい形で検知できる状態を定義する。コンパイラ保守者と CI 利用者は、IR 生成の意図しない変化をバイナリ比較ではなく可読なテキスト差分として確認できる必要がある。

## Boundary Context

- **In scope**: 代表的な `.ke` 入力に対する IR 生成結果の固定、CLI テストスイートでの snapshot 比較、失敗時に可読なテキスト差分として確認できること、期待値ファイルの継続管理。
- **Out of scope**: `.klib` バイナリ形式の互換性検証、runtime 実行結果の検証、diagnostics や manifest の golden test 拡張、IR 命令体系そのものの仕様変更。
- **Adjacent expectations**: IR の論理内容と `.klibtxt` 表現の定義は既存の IR 仕様が所有する。この仕様は、その既存契約に従って CLI テストスイートが回帰検知できることを要求する。

## Requirements

### Requirement 1: 代表入力の IR 固定

**Objective:** As a コンパイラ保守者, I want 代表的な `.ke` 入力に対する IR 生成結果を固定したい, so that 言語表面の広い範囲に対する回帰を継続的に検知できる

#### Acceptance Criteria

1. The KES CLI test suite shall 少なくとも 1 つの代表的な `.ke` 入力に対する IR 生成結果を golden snapshot として保持する。
2. When 代表入力が分岐、反復、ラベル遷移、台詞、地の文、選択肢のような主要な言語表面を含む, the KES CLI test suite shall その入力から生成された IR 全体を 1 つの期待値として検証する。
3. When コンパイラが代表入力をビルドする, the KES CLI test suite shall 実際に生成された IR テキストと保持済み snapshot を比較する。
4. If 代表入力の IR 生成結果が期待値と一致する, then the KES CLI test suite shall その golden test を成功として扱う。

### Requirement 2: 可読な差分による失敗検知

**Objective:** As a CI 利用者, I want IR golden test が可読な差分で失敗してほしい, so that 変更の妥当性をレビューで素早く判断できる

#### Acceptance Criteria

1. When IR golden test の実結果と期待値が一致しない, the KES CLI test suite shall テキスト差分として失敗を報告する。
2. When 差分が報告される, the KES CLI test suite shall 生成物全体の差分を人間が読める順序で確認できるようにする。
3. The KES CLI test suite shall バイナリ比較だけでは完結しない失敗表示を採用する。
4. If 改行コードの違いだけが存在する, then the KES CLI test suite shall それだけで IR golden test を失敗させない。

### Requirement 3: 期待値資産の継続管理

**Objective:** As a テスト整備担当, I want IR golden test の期待値をリポジトリ内で継続管理したい, so that 将来の変更でも更新対象とレビュー対象を明確に保てる

#### Acceptance Criteria

1. The KES CLI test suite shall IR golden test の期待値をリポジトリ管理下のテキストファイルとして保持する。
2. When 新しい開発者が IR golden test を確認する, the KES CLI test suite shall 期待値ファイルから比較対象の IR 内容を直接読めるようにする。
3. When 代表入力に対する正当な IR 変更が発生する, the KES CLI test suite shall 対応する期待値ファイルだけを更新対象として特定できるようにする。
4. Where IR golden test が存在する, the KES CLI test suite shall 期待値ファイルを diagnostics snapshot や manifest snapshot と区別して扱えるようにする。
