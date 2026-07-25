# Basic Setup

Unity 6000.5.3f1、URP 17.5.0以降でKESを起動するための最小構成です。

サンプルには次が含まれます。

- `KesSystem.prefab`: VM、表示、音声、入力、セーブhostをまとめた標準プレハブ
- `Input/InputSystem_Actions.inputactions`: Input System設定例
- `AddressableAssetsData/`: 空のAddressables設定例
- `Runtime/KesSampleSaveHost.cs`: ファイルI/Oをホスト側へ分離する実装例

## 導入

1. Package ManagerのSamplesから`Basic Setup`をインポートする。
2. Addressables Groupsで、インポートされた`AddressableAssetSettings`を使用する。
3. `kes build --target unity --out-dir <UnityProject>/Assets/Scenario`を実行する。
4. 生成された`manifest.kson`から作られるKES Build Assetを`KesSystem`の`KesManager`へ割り当てる。
5. `KesSystem.prefab`をURPシーンへ配置し、AddressablesをbuildしてPlay Modeを開始する。

Addressablesのaddressには、KES manifestに記録された論理asset IDと同じ文字列を指定します。
