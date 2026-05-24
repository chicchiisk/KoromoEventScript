# Requirements Document

## Introduction

GitHub Issue #16 は、`kes build --check-only` の最小骨組みをCLIに追加し、成果物生成やruntime起動を行わずにKoromoEventScriptプロジェクトの設定と入力スクリプトを検査できるようにする。CLI利用者および開発者は、プロジェクトルート解決、`kes.xml` 読み込み、`.kel` と `.ke` の解析、診断出力、終了コードを一連のCLI動作として確認できる必要がある。

## Boundary Context

- **In scope**: `kes build --check-only` のコマンド受付、プロジェクトルート解決、`kes.xml` 読み込み、エントリ `.kel` と参照される `.ke` の解析、診断出力、終了コード。
- **Out of scope**: `.k` 生成、manifest 生成、runtime 起動、publish/clean/run コマンドの挙動変更、Phase 2以降の意味解析。
- **Adjacent expectations**: 既存の `.ke` / `.kel` 構文、CLI診断形式、終了コード定義、標準プロジェクト構成に従う。

## Requirements

### Requirement 1: `kes build --check-only` コマンド受付

**Objective:** As a CLI利用者, I want `kes build --check-only` を実行できる, so that 生成なしでプロジェクト検査を開始できる

#### Acceptance Criteria

1. When `kes build --check-only` is executed, the CLI shall start check-only validation for the target KoromoEventScript project.
2. When `kes build [PROJECT_DIR] --check-only` is executed, the CLI shall use `PROJECT_DIR` as the validation target.
3. When `kes build --check-only` is executed with no `PROJECT_DIR`, the CLI shall resolve the target project from the current directory or its parent directories.
4. If the command line arguments are invalid for `kes build --check-only`, the CLI shall report a command-line diagnostic and return exit code `2`.

### Requirement 2: プロジェクト設定の読み込み

**Objective:** As a CLI利用者, I want `kes.xml` が検査時に読み込まれる, so that CLIがプロジェクト設定に基づいて入力ファイルを解決できる

#### Acceptance Criteria

1. When a project root is resolved, the CLI shall read the `kes.xml` file in that project root.
2. If a required `kes.xml` file cannot be found, the CLI shall report a CLI error diagnostic and return exit code `6`.
3. If `kes.xml` cannot be read or is not a valid project configuration, the CLI shall report a diagnostic that identifies the configuration problem and return a non-zero exit code.
4. When no explicit entry is provided, the CLI shall use the project entry declared by `kes.xml` as the `.kel` entry point.

### Requirement 3: `.kel` と `.ke` の解析

**Objective:** As a CLI利用者, I want entry `.kel` and referenced `.ke` files to be parsed, so that syntax problems are found before generation or runtime execution

#### Acceptance Criteria

1. When the entry `.kel` file is resolved, the CLI shall parse the `.kel` file using the documented `.kel` syntax rules.
2. When `.ke` files are referenced by the entry `.kel`, the CLI shall parse each referenced `.ke` file using the documented `.ke` syntax rules.
3. If an input `.kel` or `.ke` file cannot be found or read, the CLI shall report a file diagnostic and return exit code `6`.
4. If an input `.kel` or `.ke` file contains a syntax error, the CLI shall report a `KES1xxx` diagnostic for the file and return exit code `3`.
5. While `--check-only` validation is running, the CLI shall not require `.k`, manifest, or runtime artifacts to already exist.

### Requirement 4: 診断出力

**Objective:** As a CLI利用者, I want diagnostics to be printed in the standard CLI format, so that validation results are readable by humans and tooling

#### Acceptance Criteria

1. When validation produces diagnostics, the CLI shall output each diagnostic with level, code, file, line, column, and message.
2. When default text output is used, the CLI shall format diagnostics according to the documented text diagnostic layout.
3. When JSON log output is requested, the CLI shall output diagnostics as JSON Lines.
4. When multiple diagnostics are produced, the CLI shall preserve their validation order in the output.
5. If validation completes without diagnostics, the CLI shall return success without emitting error diagnostics.

### Requirement 5: 終了コード

**Objective:** As a CLI利用者, I want `kes build --check-only` to return documented exit codes, so that scripts and CI can detect validation outcomes

#### Acceptance Criteria

1. When check-only validation completes without errors, the CLI shall return exit code `0`.
2. If command-line parsing fails, the CLI shall return exit code `2`.
3. If syntax validation fails, the CLI shall return exit code `3`.
4. If file or directory input/output fails, the CLI shall return exit code `6`.
5. If multiple error categories occur, the CLI shall return the exit code for the earliest processing stage among the failures.

### Requirement 6: Check-only scope boundaries

**Objective:** As a CLI利用者, I want `--check-only` to avoid generation and runtime side effects, so that validation can run safely in development and CI

#### Acceptance Criteria

1. While `--check-only` is active, the CLI shall not generate `.k` files.
2. While `--check-only` is active, the CLI shall not generate manifest files.
3. While `--check-only` is active, the CLI shall not start any runtime.
4. When validation completes, the CLI shall leave existing build and distribution artifacts unchanged.
