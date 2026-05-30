# Requirements Document

## Introduction

KoromoEventScript のCLI利用者とコンパイラ開発者は、現在 `.kc` / `.kel` を入力にした意味解析で `import` が解決されないため、複数ファイルに分割した定義を後続の名前解決で利用できない。Issue #17 では、import されたファイルをプロジェクト基準で解決し、循環や未存在ファイルを診断し、import 済み定義を名前解決に渡せるようにする。

## Boundary Context

- **In scope**: プロジェクト基準の import 解決、import 依存関係の構築、未存在ファイルと循環 import の診断、import 済み定義を後続の名前解決で利用できる状態にすること。
- **Out of scope**: 新しい import 構文の追加、`.kel` の新しい構文拡張、型検査全体の完成、IR / `.klib` 生成、manifest 生成、runtime 起動。
- **Adjacent expectations**: 既存の `.kc` import 構文、`.kel` を起点にした入力解決、CLI診断形式、終了コード、既存の構文解析結果に従う。

## Requirements

### Requirement 1: Import 対象ファイルの解決

**Objective:** As a CLI利用者, I want import されたモジュールがプロジェクト基準で解決される, so that 複数ファイルに分割したスクリプトを検証できる

#### Acceptance Criteria

1. When 意味解析が import 文を含む入力ファイルを処理する, the KES compiler shall import モジュール名をプロジェクト内の対応する入力ファイルとして解決する。
2. When import モジュール名が拡張子なしのファイル名として指定される, the KES compiler shall documented import 規則に従って対応する `.kc` 入力を特定する。
3. When import 元ファイルと import 先ファイルが異なるディレクトリにある, the KES compiler shall プロジェクト基準の解決規則を優先して import 先を特定する。
4. If import モジュール名に対応する入力ファイルがプロジェクト内に存在しない, the KES compiler shall import 元のファイル位置を示す診断を報告する。
5. If import モジュール名が複数の入力ファイルに一致する, the KES compiler shall あいまいな import として診断を報告する。

### Requirement 2: Import 依存関係の構築

**Objective:** As a コンパイラ開発者, I want import 依存関係が一貫して構築される, so that 後続の意味解析が同じ入力集合を参照できる

#### Acceptance Criteria

1. When 解析対象ファイルが import を持つ, the KES compiler shall import 先ファイルを依存関係に含める。
2. When import 先ファイルがさらに import を持つ, the KES compiler shall transitive import 依存関係を検査対象に含める。
3. When 同じファイルが複数経路から import される, the KES compiler shall そのファイルを重複しない1つの依存関係として扱う。
4. When import 依存関係が構築される, the KES compiler shall import 元から到達可能なファイルの検査順序を安定して保持する。
5. While import 解決が実行されている, the KES compiler shall `.klib`、manifest、runtime成果物の存在を要求しない。

### Requirement 3: Import エラー診断

**Objective:** As a CLI利用者, I want import 解決の失敗が標準診断として報告される, so that どのファイル関係を直せばよいか分かる

#### Acceptance Criteria

1. If import 先ファイルが存在しない, the KES compiler shall ファイル、行、列、診断コード、メッセージを含む診断を出力する。
2. If import 先ファイルを読み取れない, the KES compiler shall ファイル入出力失敗として診断を出力する。
3. If import 依存関係に循環がある, the KES compiler shall 循環に含まれる import 経路を識別できる診断を出力する。
4. If import 先ファイルに構文エラーがある, the KES compiler shall import 元の意味解析を成功扱いせず、import 先ファイルの構文診断を保持する。
5. When 複数の import 診断が発生する, the KES compiler shall 検査順序に従って診断を出力する。

### Requirement 4: Import 済み定義の名前解決利用

**Objective:** As a CLI利用者, I want import 済み定義が後続の名前解決で利用される, so that 共通定義を別ファイルへ分割できる

#### Acceptance Criteria

1. When import 先ファイルがトップレベル定義を含む, the KES compiler shall import 元ファイルの後続名前解決でその定義を参照可能にする。
2. When import 先ファイルの定義が import 元ファイルから参照される, the KES compiler shall 未定義名として診断しない。
3. If import されていないファイルの定義が参照される, the KES compiler shall 未定義名として診断する。
4. If import 済み定義と import 元ファイル内の定義が同じ名前で衝突する, the KES compiler shall 名前衝突として診断する。
5. If 複数の import 先が同じ名前を公開する, the KES compiler shall あいまいな名前参照として診断する。

### Requirement 5: CLI 結果との統合

**Objective:** As a CLI利用者, I want import 解決結果が `kes build --check-only` の結果に反映される, so that CIやスクリプトで import 問題を検出できる

#### Acceptance Criteria

1. When `kes build --check-only` が import を含むプロジェクトを検証する, the KES CLI shall import 解決と import 済み定義の名前解決を検証に含める。
2. When import 解決と名前解決が成功する, the KES CLI shall 成功終了コードを返す。
3. If import 解決または名前解決でコンパイルエラーが発生する, the KES CLI shall コンパイルエラー終了コードを返す。
4. If import 先ファイルの入出力に失敗する, the KES CLI shall ファイルまたはディレクトリ入出力エラー終了コードを返す。
5. If import 関連エラーと他のエラー分類が同時に発生する, the KES CLI shall 最も早い処理段階のエラー分類に対応する終了コードを返す。
