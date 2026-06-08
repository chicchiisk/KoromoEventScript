# Implementation Plan

- [x] 1. テスト基盤の golden 比較契約を明確化する
- [x] 1.1 `KlibCompilerTests` の snapshot 解決と改行正規化を局所責務として整理する
  - IR snapshot の参照先が常に `testdata/snapshots/ir/` に閉じるよう、helper の役割をテスト内で明確にする。
  - 実結果と期待値の双方で同じ改行正規化手順を通す比較前処理を 1 箇所へ揃える。
  - 完了時には、IR golden test の比較準備が 1 つの比較経路に集約され、環境依存の改行差だけでは失敗しない状態になる。
  - *Requirements: 1.3, 2.4, 3.3, 3.4*

- [x] 1.2 `KlibCompilerTests` の成功条件と失敗条件を全文比較ベースへ固定する
  - `.klibtxt` の全文一致を golden test の唯一の合否判定として扱い、部分一致や暗黙の確認に依存しない形へ整理する。
  - テスト名と assertion の意図から、IR 変更時に全文差分をレビューするテストであることが読み取れるようにする。
  - 完了時には、snapshot と実結果が一致したときだけテストが成功し、不一致時は可読なテキスト差分で失敗する。
  - *Requirements: 1.4, 2.1, 2.2, 2.3*

- [x] 2. 代表入力 fixture を broad surface の canonical ケースとして整える
- [x] 2.1 broad surface 入力が主要な言語表面を 1 ケースで覆うように整理する
  - 分岐、反復、ラベル遷移、台詞、地の文、選択肢が代表入力に含まれることを確認し、必要なら fixture を調整する。
  - build 実行が既存の `.klibtxt` 出力経路を通るよう、`EmitTextIr` を使う self-contained な流れを維持する。
  - 完了時には、1 つの代表入力を build するだけで主要な言語表面に対する IR 生成回帰を検知できる。
  - *Requirements: 1.2, 1.3*

- [x] 3. IR snapshot 資産を canonical 期待値として同期する
- [x] 3.1 代表入力に対応する `broad-surface.klibtxt` を canonical snapshot として更新する
  - representative fixture から得られる `.klibtxt` を `testdata/snapshots/ir/` 配下の期待値へ同期する。
  - 期待値ファイルはレビュー対象のテキスト資産として読みやすさを保ち、diagnostics や manifest の snapshot と混在させない。
  - 完了時には、1 fixture と 1 snapshot の対応が明確で、新しい開発者が期待値ファイルを直接読んで IR 内容を確認できる。
  - *Requirements: 1.1, 2.2, 3.1, 3.2, 3.4*
  - *Depends: 2.1*

- [x] 4. 検証経路と回帰確認を仕上げる
- [x] 4.1 snapshot 更新対象と比較経路が一意に追える状態を確認する
  - test 名、helper、snapshot ファイル名の対応から、正当な IR 変更時にどの期待値を更新すべきか迷わない形へ整える。
  - broad surface の IR golden test が既存の build 出力契約と矛盾しないことを確認する。
  - 完了時には、fixture 変更から snapshot 更新対象までの追跡経路が 1 つに定まり、更新理由を PR で説明しやすい状態になる。
  - *Requirements: 3.3*

- [x] 4.2 対象テストを実行して IR golden test の回帰検知を確認する
  - `KlibCompilerTests` を中心に対象テストを実行し、代表入力の build・snapshot 比較・改行正規化が一体で通ることを確認する。
  - 必要に応じて既存の関連 build テストと合わせて、`.klibtxt` 出力経路が壊れていないことを確認する。
  - 完了時には、IR golden test が CI で再実行可能な回帰検知として成立し、対象テストの成功結果を実装報告へ載せられる。
  - *Requirements: 1.1, 1.4, 2.1, 2.4*
  - *Depends: 1.1, 1.2, 3.1*
