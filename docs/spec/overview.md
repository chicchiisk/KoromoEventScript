# overview

KoromoEventScriptでは、言語仕様とコンパイラだけではなく、
補助ツール、IDE連携、ゲームエンジンへの組み込みなどの補助ツールを通じて、
シナリオライティングからスクリプティングまでのワークフローを効率化します。
以下にその全体像を示します。

## システムの全体像

KoromoEventScriptは、中核をなすKoromoEventScript言語と、
それを解釈し実行する各プラットフォーム向けの実装からなります。

### 関連リソース

- イベントスクリプトファイル : KoromoEvent(.ke)
  - KoromoEventScript言語で記述され、具体的なシナリオと演出が含まれます。
- イベントマスタファイル : KoromoEventList(.kel)
  - イベント同士の関連を定義するファイルです。イベントの流れを設定し、ノベルゲームのストーリーを構成します。

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
    - 単体実行 : kes run XXX.kel でイベントマスタファイルを起点にゲームを起動する
    - Unity組み込み :
      - `kes publish --target unity` で生成した `.k` / `.kel` フォルダを Assets 以下にインポート
      - シーンに KoromoEventScriptManager を配置して `.kel` ファイルを指定して実行する
    - UnrealEngine組み込み :
      - `kes publish --target unreal` で生成した `.k` / `.kel` フォルダを Content 以下にインポート
      - シーンに KoromoEventScriptManager を配置して `.kel` ファイルを指定して実行する

## 詳細仕様書

イベントスクリプトとイベントマスタファイルの仕様書 : [[kes-language-spec]]
CLIツールの仕様書 : [[cli-tool-spec]]
VSCode言語サポート拡張の仕様書 : [[vscode-ext-spec]]
単体実行基盤の仕様書 : [[windows-runtime-spec]]
UnrealEngine組み込み拡張の仕様書 : [[unreal-runtime-spec]]
Unity組み込み拡張の仕様書 : [[unity-runtime-spec]]
