# overview

KoromoEventScriptでは、言語仕様とコンパイラだけではなく、
補助ツール、IDE連携、ゲームエンジンへの組み込みなどの補助ツールを通じて、
シナリオライティングからスクリプティングまでのワークフローを効率化します。
以下にその全体像を示します。

## システムの全体像

KoromoEventScriptは、中核をなすKoromoEventScript言語と、
それを解釈し実行する各プラットフォーム向けの実装からなります。

### 関連リソース

- イベントスクリプトファイル : KoromoEventScript(.kc)
  - KoromoEventScript言語で記述され、具体的なシナリオと演出が含まれます。
- イベントマスタファイル : KoromoEventList(.kel)
  - イベント同士の関連を定義するファイルです。イベントの流れを設定し、ノベルゲームのストーリーを構成します。
- VM実行用中間表現 : KoromoEventScript Intermediate Representation(.klib)
  - CLIが`.kc`をビルドして生成するVM向け成果物です。
- ローカライズ辞書テンプレート : Localization Template(.csv)
  - `kes loc` が `.kc` から抽出したタグと原文を出力する翻訳用テンプレートです。
- ローカライズ済み中間表現 : Localized KoromoEventScript Intermediate Representation(.klib)
  - `kes build --loc <language-tag>` がローカライズ辞書 `.csv` を取り込んで生成する、言語別の VM 向け成果物です。

現行仕様では、イベントスクリプトファイルは `.kc`、VM実行用中間表現は `.klib` を正とします。
`.kc` と `.klib` は旧称または移行前表記として扱います。

### 関連ソフトウェア

- CLIツール : KoromoEventScript CLI Tool (kes.exe)
  - コンパイル、実行、デバッグなどの開発時のコマンドライン操作の起点です
- VSCode言語サポート : KoromoEventScript VsCode Extension
  - VSCode上でKoromoEventScript言語をサポートするための拡張機能です
- 単体実行基盤(Windowsのみ) : KoromoEventScript Runtime
  - コマンドラインから単体でKoromoEventScriptを実行するためのツールです。Windowsのみをサポートします。
- UnrealEngine組み込み拡張 : KoromoEventScript UE
  - UnrealEngine上でKoromoEventScriptを実行するための拡張機能です
- Unity組み込み拡張 : KoromoEventScript Unity
  - Unity上でKoromoEventScriptを実行するための拡張機能です

### ワークフロー例

1. シナリオの作成 : `.kc` ファイルを作成し、日本語など任意の基準言語でシナリオ本文を記述する
2. 演出の作成 : `.kc` ファイルのシナリオに、演出命令を追記する
3. イベントマスタファイルの作成 : .kelファイルを作成し、イベント同士をつなぎ合わせる
4. 書き戻し整形 : `kes correct` で `.kel` から参照される `.kc` を解析し、各セリフに対応するローカライズタグを補完して `.kc` に書き戻す。差分確認だけをしたい場合は `--check-only` を使う
5. 辞書テンプレート生成 : `kes loc` で `kes correct` 相当の処理を行った後、タグと原文だけを含むローカライズ辞書テンプレート `.csv` を出力する
6. 翻訳作業 : 書き出された `.csv` を各言語向けに編集し、タグに対応する翻訳文を追記する
7. ビルド : `kes build` で `kes correct` 相当の処理を行ってから `.kc` を `.klib` にコンパイルする。`--loc <language-tag>` が指定された場合はプロジェクトルートのローカライズ辞書 `.csv` からその言語列を解決し、`build/<target>/events/loc/<language-tag>/` 配下へローカライズ済み `.klib` を生成する。指定がない場合は基準言語の `.klib` を従来どおり `build/<target>/events/` 配下へ生成する
8. 実行
    - 単体実行 : kes run main.kel でイベントマスタファイルを起点にゲームを起動する
    - Unity組み込み :
      - `kes publish --target unity` で生成した `.klib` / `.kel` フォルダを Assets 以下にインポート
      - シーンに KoromoEventScriptManager を配置して `.kel` ファイルを指定して実行する
    - UnrealEngine組み込み :
      - `kes publish --target unreal` で生成した `.klib` / `.kel` フォルダを Content 以下にインポート
      - シーンに KoromoEventScriptManager を配置して `.kel` ファイルを指定して実行する

`.klib` のファイル形式、instruction schema、VM実行契約、manifest参照契約は、[`.klib` 中間表現仕様](k-intermediate-representation-spec.md)で定義します。

## 詳細仕様書

イベントスクリプトファイルの仕様書 : [[kes-language-spec]]
イベントマスタファイルの仕様書 : [[kel-file-spec]]
CLIツールの仕様書 : [[cli-tool-spec]]
`.klib` 中間表現仕様書 : [[k-intermediate-representation-spec]]
ローカライズ辞書仕様書 : [[localization-dictionary-spec]]
VSCode言語サポート拡張の仕様書 : [[vscode-ext-spec]]
単体実行基盤の仕様書 : [[windows-runtime-spec]]
UnrealEngine組み込み拡張の仕様書 : [[unreal-runtime-spec]]
Unity組み込み拡張の仕様書 : [[unity-runtime-spec]]
