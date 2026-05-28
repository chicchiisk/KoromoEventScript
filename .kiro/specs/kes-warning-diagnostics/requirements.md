# Requirements Document

## Introduction

この仕様では、`kes build --check-only` が warning level の診断を既存の診断出力契約に従って扱い、`warnings-as-errors` が有効な場合に警告だけの検証結果を失敗として終了コードへ反映できる状態を目指す。CLI 利用者と CI は、警告を通常のエラーとは区別して確認しつつ、必要に応じて警告を品質ゲートの失敗条件として扱える必要がある。

既存の `docs/spec/cli-tool-spec.md` は `Build.WarningsAsErrors`、`--warnings-as-errors`、警告診断 `KES4xxx`、終了コード `9` を定義している。既存実装には `DiagnosticLevel.Warning` と formatter / sink レベルの警告出力があるが、build 検証フローにおける警告診断、warnings-as-errors、終了コード反映の要求をこの仕様で固定する。

## Boundary Context

- **In scope**: `kes build --check-only` における warning level 診断の text / JSON Lines 出力、`--warnings-as-errors` と `Build.WarningsAsErrors` による警告昇格、終了コード `0` / `9` / 既存エラー終了コードの選択。
- **Out of scope**: runtime 実行中の警告、publish / run / clean / init の警告ポリシー、素材 manifest の完全検証、VS Code 拡張の診断表示、警告コード体系全体の再設計、既存 compile error 診断の分類変更。
- **Adjacent expectations**: 既存の syntax / compile / file I/O 診断、text / JSON Lines 形式、終了コード分類と整合する。警告診断の個別生成ルールは、既存または将来の検証ルールが warning level として分類したものをこの仕様の入力として扱う。

## Requirements

### Requirement 1: 警告診断の出力

**Objective:** As a CLI 利用者, I want warning level 診断がエラー診断と同じ形式で出力される, so that ローカル検証と CI ログで警告を確認できる

#### Acceptance Criteria

1. When `kes build --check-only` の検証中に warning level 診断が生成される, the KES CLI shall その診断を既存の診断出力に含める。
2. When warning level 診断が text 形式で出力される, the KES CLI shall 診断レベルを `warning` として表示する。
3. When warning level 診断が JSON Lines 形式で出力される, the KES CLI shall 診断レベルを `warning` として出力する。
4. When warning level 診断が出力される, the KES CLI shall 診断の code、file、line、column、message を既存の診断項目として保持する。
5. The KES CLI shall warning level 診断を標準エラー出力へ出力する。

### Requirement 2: warnings-as-errors の有効化

**Objective:** As a CI 利用者, I want CLI オプションまたはプロジェクト設定で警告を失敗扱いにできる, so that 品質ゲートで警告を見逃さない

#### Acceptance Criteria

1. When `kes build --check-only` が `--warnings-as-errors` を受け取る, the KES CLI shall warning level 診断を失敗条件として扱う。
2. When `kes.xml` の `Build.WarningsAsErrors` が `true` である, the KES CLI shall warning level 診断を失敗条件として扱う。
3. When `--warnings-as-errors` が指定され、`kes.xml` の `Build.WarningsAsErrors` が `false` である, the KES CLI shall warning level 診断を失敗条件として扱う。
4. When `--warnings-as-errors` が指定されず、`kes.xml` の `Build.WarningsAsErrors` が `false` または未指定である, the KES CLI shall warning level 診断だけを失敗条件として扱わない。

### Requirement 3: 警告に基づく終了コード

**Objective:** As a CI 利用者, I want warning-only の検証結果が設定に応じた終了コードを返す, so that スクリプトから警告ポリシーを判定できる

#### Acceptance Criteria

1. When warning level 診断だけが生成され、warnings-as-errors が無効である, the KES CLI shall 成功終了コード `0` を返す。
2. When warning level 診断だけが生成され、warnings-as-errors が有効である, the KES CLI shall 警告をエラーとして扱った終了コード `9` を返す。
3. When warning level 診断が複数生成され、warnings-as-errors が有効である, the KES CLI shall 終了コード `9` を返す。
4. When warning level 診断が出力され、終了コード `0` または `9` が返る, the KES CLI shall 出力される診断レベルを `warning` のまま維持する。

### Requirement 4: 既存エラー分類との優先順位

**Objective:** As a CLI 利用者, I want 既存のエラー終了コードが警告昇格で上書きされない, so that エラー原因の分類が安定する

#### Acceptance Criteria

1. When syntax error 診断と warning level 診断が同じ検証で生成される, the KES CLI shall syntax error 終了コードを返す。
2. When compile error 診断と warning level 診断が同じ検証で生成される, the KES CLI shall compile error 終了コードを返す。
3. When file I/O error 診断と warning level 診断が同じ検証で生成される, the KES CLI shall file I/O error 終了コードを返す。
4. When warning level 診断と error level 診断が同じ検証で生成され、warnings-as-errors が有効である, the KES CLI shall error level 診断に対応する既存終了コードを返す。

### Requirement 5: スコープ境界と互換性

**Objective:** As a KES 開発者, I want 警告ポリシーが既存診断契約と互換である, so that 将来の警告ルールを同じ出力と終了コード規則に接続できる

#### Acceptance Criteria

1. The KES CLI shall `KES4xxx` の warning level 診断を警告診断として扱う。
2. The KES CLI shall warning level 診断を `error` 表記へ変換しない。
3. The KES CLI shall runtime 実行中の警告をこの仕様の `kes build --check-only` 終了コード規則の対象にしない。
4. The KES CLI shall publish、run、clean、init コマンドの警告ポリシーをこの仕様の対象にしない。
5. The KES CLI shall 既存の compile error 診断コードと終了コード分類を変更しない。
