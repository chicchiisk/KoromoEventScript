# Unity manifestをkson assetとしてimportする

- ADR: 0003
- ステータス: 採用
- 日付: 2026-07-13
- 関連 Issue:
- 関連仕様: `docs/spec/runtime-manifest-spec.md`、`docs/spec/unity-runtime-spec.md`

## 背景

Unityでは`.json`が標準TextAsset importerに所有されるため、KoromoEventScript専用の`ScriptedImporter`を直接登録できない。Unity Playerは`AssetDatabase`を利用できないため、Editor import時にmanifestと`.klib`の参照をUnity assetへ変換しておく必要がある。

## 決定

`kes build --target unity`はruntime manifestを`manifest.kson`として出力する。`.kson`の内容はJSONであり、共通runtime manifest schemaに従う。Unity packageは`.kson`専用`ScriptedImporter`を登録し、検証済みmanifest modelとKES Klib Asset参照を保持するKES Build Assetをmain objectとして生成する。

## 検討した代替案

### JSON TextAssetとAssetPostprocessorを使う

標準`.json`を維持できるが、他のJSON assetとの識別、生成assetの寿命、再import依存関係が複雑になるため採用しない。

### 手動でScriptableObjectを作る

実装は単純だが、CLI build後の更新漏れと参照不整合を利用者が管理することになるため採用しない。

## 判断理由

- 拡張子でKES manifestを一意に識別できる。
- Unity Asset Pipelineの依存関係とGUID管理へ統合できる。
- PlayerへEditor専用path解決処理を持ち込まずに済む。
- JSONの共通schemaをWindows、Unity、Unrealで共有できる。

## 影響

- Unity targetだけmanifestファイル名が`manifest.kson`になる。
- CLI、testdata、importer、ドキュメントを同時に更新する必要がある。
- `.kson`を独自JSON方言として拡張してはならない。
- KES Build AssetのGUIDは元`.kson`のGUIDに従う。

## フォローアップ

- CLIのUnity output plannerとgolden snapshotを更新する。
- `.kson` importerとdependency tracking testを追加する。
- runtime manifest JSON Schema validationをCLIとUnity importerへ追加する。
