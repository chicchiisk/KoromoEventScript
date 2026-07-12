# UnityとWindowsでランタイムソースを共有する

- ADR: 0002
- ステータス: 採用
- 日付: 2026-07-13
- 関連 Issue:
- 関連仕様: `docs/spec/unity-runtime-spec.md`、`docs/spec/k-intermediate-representation-spec.md`

## 背景

WindowsランタイムとUnity拡張は同じ`.klib`を同じ意味論で実行する必要がある。一方、既存Runtime Coreは.NET 10 projectとして実装され、Unity 6000.5.3f1はより低いC#言語・API profileでソースをコンパイルする。別VM実装を持つとopcode、manifest、イベント評価、状態復元の差異が生じやすい。

## 決定

`.klib` loader、VM、manifest model、イベント評価、状態modelのC#ソースをWindowsとUnityで共有する。共通ソースはUnity 6000.5.3f1がコンパイルできるC# 9.0と.NET Standard 2.1の範囲へ制限する。platform固有処理はinterfaceの背後へ分離し、同じソースファイルを.NET projectとUnity asmdefの双方からコンパイルする。

## 検討した代替案

### Unity専用VMを実装する

Unity向けに最適化しやすいが、同じ`.klib`に対する意味論と不具合修正を二重管理するため採用しない。

### .NET 10 assemblyをUnityから直接参照する

UnityのAPI compatibilityとC# runtimeの制約に適合せず、対応環境を保証できないため採用しない。

## 判断理由

- opcodeとsave/restoreの意味論を単一実装で固定できる。
- 共通テストケースをWindowsとUnityの双方へ適用できる。
- platform固有UIやファイルI/OをCoreから分離する設計を促進する。

## 影響

- 共通ソースではC# 10以降の構文とUnity非対応BCL APIを使用できない。
- 既存の新しい構文をC# 9互換へ変更する作業が必要になる。
- record利用時はUnity側へ`IsExternalInit`互換shimが必要になる。
- .NET側とUnity側の両方で同じCore testを実行する必要がある。

## フォローアップ

- 共有対象ファイルとplatform interfaceを確定する。
- Runtime CoreのC# 9互換性検査をCIへ追加する。
- Unity Edit Modeで`.klib` loaderとVMの共通golden testを実行する。
