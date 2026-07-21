# Snapshot schema 2でcall frameを永続化する

- ADR: 0008
- ステータス: 採用
- 日付: 2026-07-20
- 関連 Issue: 性能改善計画 11
- 関連仕様: `docs/spec/unity-runtime-spec.md`、`docs/spec/windows-runtime-spec.md`

## 背景

ユーザー定義関数の実行中は、現在位置と変数だけでなく、return位置と再帰呼び出し前のlocal状態がcall frameに保持される。従来のRuntime Core snapshot schema 1はcall frameを保存しないため、関数内で`state.save`やhost待機が発生した状態を正しく復元できない。

## 決定

Runtime Core snapshot schemaを2へ更新し、外側から内側の順にcall frameを保存する。各frameは関数index、return命令index、戻り値要否、呼び出し前のlocal slot初期化状態と値を持つ。

schema 1はcall frameが空の旧形式として読み込み可能にする。schema 2の復元時は、関数indexとreturn位置が現在の`.klib`に存在することを検証する。

headless runtimeも同じ情報を既存のcall frame snapshotへ追加し、途中状態のserialize/restoreで失わない。

## 検討した代替案

### 関数実行中の保存を禁止する

host処理やシナリオ命令を関数から呼べる言語仕様と矛盾し、保存可能地点が利用者に分かりにくくなるため採用しない。

### call stackを単一の不透明byte列で保存する

runtime version間の検証とmigrationが困難になり、壊れたframeの診断もできないため採用しない。

## 判断理由

- 関数内と再帰中の実行を決定論的に再開できる。
- schema 1との後方互換を明示できる。
- frameの各参照を復元前に検証できる。
- Unity、Windows、headlessで同じ概念を共有できる。

## 影響

- 新規captureはschema 2を出力する。
- schema 1のloadは継続するが、call frameは空として扱う。
- 将来Function Tableを変更する場合、build互換policyとframe migrationを同時に検討する必要がある。
