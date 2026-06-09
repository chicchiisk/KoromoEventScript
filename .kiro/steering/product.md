# Product Overview

KoromoEventScript は、RPG・ADV・ノベルゲーム向けのシナリオ DSL と、その開発・実行環境を段階的に整備するプロジェクトである。脚本寄りの記法で `.kc` と `.kel` を記述し、CLI による検証・ビルド・実行を中核に据えつつ、Windows ランタイム、VS Code 拡張、Unity、Unreal Engine 連携へ拡張できる共通基盤を育てる。

現在の実装重心は MVP の CLI と言語処理系にある。仕様書とテストを先に整え、実装は公開仕様と CI で裏付けられた小さな単位で前進させることを前提にしている。

## Core Capabilities

- `.kc` と `.kel` を対象にした字句解析、構文解析、意味解析、IR 生成を CLI で完結させる
- `.klib` を共通中間表現として扱い、headless VM や将来の各種ランタイムで再利用できる境界を維持する
- 仕様書、golden test、診断テスト、VM テストで言語仕様とツール挙動を固定する
- GitHub Issue、PR、CI、人間レビューを前提に、AI 実装でも追跡可能な開発フローを保つ

## Target Use Cases

- シナリオ DSL のコンパイラと実行系を段階的に実装し、仕様と実装の差分を減らしたい開発者
- CLI 上で `.kc` / `.kel` の検証、ビルド、診断確認を行いたい利用者
- 将来の Windows / Unity / Unreal ランタイムに共有できるスクリプト資産と中間表現を整備したいチーム

## Value Proposition

- 言語仕様、CLI 仕様、ランタイム仕様を `docs/spec/` で明示し、実装が仕様駆動で進む
- ヘッドレス実行や golden test を重視し、UI 実装より前に言語機能の回帰安全性を確保する
- CLI、言語処理系、ランタイム連携を別境界として育てられるため、MVP の集中開発と将来拡張を両立しやすい
