# KoromoEventScript Unity Extension

このディレクトリは次の2領域に分ける。

- `Package/`: Unity Package Manager で導入するプラグイン本体
- `SampleProject/`: Unity 6000.5.3f1、URP で package を検証するサンプルプロジェクト

`SampleProject/Packages/manifest.json` はプロジェクトルート基準で `file:../Package` を参照するため、リポジトリ内では package の変更がサンプルプロジェクトへ直接反映される。

## GitHubからの導入

利用者は Unity の Package Manager で **Install package from Git URL** を選び、次のURLを指定する。

```txt
https://github.com/chicchiisk/KoromoEventScript.git?path=/source/extension/unity/Package
```

再現可能な導入では、リリースタグまたはコミットハッシュを末尾に付ける。

```txt
https://github.com/chicchiisk/KoromoEventScript.git?path=/source/extension/unity/Package#unity-v0.1.0
```

`Packages/manifest.json`へ直接記述する場合は次の形式とする。

```json
{
  "dependencies": {
    "com.koromosoft.koromo-event-script": "https://github.com/chicchiisk/KoromoEventScript.git?path=/source/extension/unity/Package#unity-v0.1.0"
  }
}
```

Git依存関係の取得には、Unityを実行する環境の`PATH`からGitクライアントを利用できる必要がある。既定ブランチを直接参照するURLは動作確認用途とし、ゲームプロジェクトではタグまたはコミットハッシュへ固定する。

## 開発とリリース

- リポジトリ内の`SampleProject`は未コミット変更を即時検証できるよう`file:../Package`を使用する。
- 公開リリースタグは`unity-v{package.jsonのversion}`形式とする。
- タグ作成前に`package.json`と`CHANGELOG.md`のバージョンを一致させる。
- 公開タグは作成後に付け替えない。修正時はpackage versionを上げ、新しいタグを作成する。
- GitHub URL経由では`?path=`で`Package/`だけをUPM packageとして登録し、`SampleProject/`は利用者のプロジェクトへ導入しない。
