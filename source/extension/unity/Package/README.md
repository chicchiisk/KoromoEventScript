# KoromoEventScript for Unity

KoromoEventScript の Unity 組み込み拡張パッケージである。

## 対象環境

- Unity 6000.5.3f1 以降
- Universal Render Pipeline 17.6.0 以降
- Addressables 2.7.6 以降
- Input System 1.19.0 以降
- uGUI 2.5.0 以降

実装契約は[Unity組み込み拡張仕様書](https://github.com/chicchiisk/KoromoEventScript/blob/main/docs/spec/unity-runtime-spec.md)を参照する。

## インストール

Unity Package Managerの **Install package from Git URL** へ次のURLを入力する。

```txt
https://github.com/chicchiisk/KoromoEventScript.git?path=/source/extension/unity/Package
```

リリース利用では、更新による予期しない変更を避けるためタグへ固定する。

```txt
https://github.com/chicchiisk/KoromoEventScript.git?path=/source/extension/unity/Package#unity-v0.1.0
```

利用環境にはGitクライアントが必要である。パッケージ導入後、Package ManagerのSamplesタブから`Basic Setup`をインポートできる。

リポジトリをcloneして開発する場合は、`../SampleProject/Packages/manifest.json`が`file:../../Package`でこのpackageを直接参照する。
