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

1. シナリオの作成 : .keファイルを作成し、シナリオ本文を記述する
2. 演出の作成 : .keファイルのシナリオに、演出命令を追記する
3. イベントマスタファイルの作成 : .kelファイルを作成し、イベント同士をつなぎ合わせる
4. ビルド＆自動処理 : .keファイルを kes build コマンドで中間表現である .kファイルにビルドする。一部IDなどの自動採番を行って.keファイルに書き戻す。またローカライズ辞書(.csv)を書き出す
5. 実行
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
VSCode言語サポート拡張の仕様書 : [[vscode-ext-spec]]
単体実行基盤の仕様書 : [[windows-runtime-spec]]
UnrealEngine組み込み拡張の仕様書 : [[unreal-runtime-spec]]
Unity組み込み拡張の仕様書 : [[unity-runtime-spec]]
