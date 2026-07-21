# ユーザー定義関数をFunction Tableとcall frameで実行する

- ADR: 0007
- ステータス: 採用
- 日付: 2026-07-20
- 関連 Issue: 性能改善計画 10
- 関連仕様: `docs/spec/kes-language-spec.md`、`docs/spec/k-intermediate-representation-spec.md`

## 背景

言語仕様には `fn` と `return` が定義されていたが、compilerは関数宣言を成果物から除外し、VMはユーザー定義関数を実行できなかった。標準callableと同じ文字列dispatchへ追加すると、関数entry、引数slot、再帰時のlocal退避をruntimeが復元できない。

## 決定

`.klib` に任意のFunction Table section（0x0008）を追加し、関数名、entry byte offset、引数slot、local slot、戻り値有無を保存する。compilerはユーザー定義関数呼び出しを専用の `CALL_FUNCTION*` と `RETURN_*` へloweringする。

runtimeは呼び出しごとにcall frameを作成し、return位置と対象local slotの以前の状態を退避する。return時にlocalを復元することで、同じ関数の再帰呼び出しを許可する。

## 検討した代替案

### 標準callableの文字列dispatchを再利用する

既存の `CALL` 実装は外部callableを即時実行する契約で、bytecode内のentryへ制御を移す情報を持たない。再帰と中断再開も扱えないため採用しない。

### 関数ごとに変数slotを複製する

再帰深度がcompile-timeに決まらず、相互再帰にも対応できないため採用しない。

## 判断理由

- 関数メタデータをloaderで検証できる。
- call frameにより再帰、ネスト呼び出し、中断再開へ同じモデルで対応できる。
- 外部callableとユーザー関数のopcodeを分離し、runtime境界を明確にできる。
- 引数とlocalのslotを明示するため、関数単位の最適化やインライン化に利用できる。

## 影響

- `.klib` version 1.1 runtimeはFunction Tableと4つの関数opcodeを解釈する。
- 値を返す関数は全経路でreturnする必要があり、void関数は値を返せない。
- 共有Runtime Coreとheadless VMの両方がcall frameを持つ。
- 実行途中のsave snapshotはcall frameを永続化する必要がある。

## フォローアップ

性能改善計画11でcall frameを含むsnapshot schemaを定義し、関数内で中断した状態の保存・復元を実装する。
