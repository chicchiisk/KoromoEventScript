# testdata

このディレクトリには、Parser、Diagnostic、Compiler、CLI 統合テストで使う入力と期待値を置く。

テストコードに長い KES ソースを直接埋め込まず、レビュー可能なファイルとして管理する。

## 構成

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

## 追加ルール

- 正常系は `valid/` に置く。
- 異常系は `invalid/` に置き、期待する診断を `snapshots/diagnostics/` に置く。
- CLI 統合テストに使う複数ファイル構成は `projects/` に置く。
- golden test の期待値は `snapshots/` に置く。
