# Unity Presentation とアセット解決の分離

- ADR: 0004
- ステータス: 採用
- 日付: 2026-07-15
- 関連 Issue: なし
- 関連仕様: `docs/spec/unity-runtime-spec.md`

## 背景

Unity拡張はVMが発行する演出effectをSpriteRenderer、uGUI、AudioSourceへ反映しつつ、素材をAddressablesから非同期に解決する必要がある。描画状態の更新とAddressables handleの管理を同一クラスへ集約すると、表示テストがAddressables設定へ依存し、将来の独自asset providerへの差し替えも難しくなる。

## 決定

VM effectをUnityオブジェクトへ反映する`KesPresentation`と、論理asset IDからUnity assetを解決する`IKesAssetResolver`を分離する。

- `KesPresentation`は1920x1080座標変換、背景・actor・UIの表示状態を担当する。
- `IKesAssetResolver`はSprite・AudioClipの非同期取得と参照解放を担当する。
- 標準実装は`KesAddressablesAssetResolver`とし、asset IDをAddressables keyとしてそのまま渡す。
- テストでは同期的なfake resolverへ差し替え、描画状態をAddressables設定から独立して検証する。

## 検討した代替案

### KesManagerへAddressables処理を直接実装する

起動、VM進行、描画、asset lifetimeが一つのMonoBehaviourへ集中するため採用しなかった。

### PresentationがResources.Loadを使用する

Git URL配布とAddressablesを前提とする公開仕様に反し、非同期ロードとhandle解放の契約も満たせないため採用しなかった。

## 判断理由

- VMとUnity表示の境界を`RuntimeEffectBatch`として維持できる。
- Presentationをfake resolverで決定的にテストできる。
- ホストプロジェクトが独自resolverを提供できる。
- Addressables handleの所有と解放箇所が明確になる。

## 影響

- 標準プレハブは`KesManager`、`KesPresentation`、`KesAddressablesAssetResolver`を組み合わせる。
- 演出完了待ちとasset load失敗時のVM停止は、Presentationの非同期完了をKesManagerへ返す後続実装が必要になる。
- 音声Presenterも同じ`IKesAssetResolver`を利用する。

## フォローアップ

- AddressablesのEditor検証を追加する。
- 演出完了待ちとキャンセルをKesManagerへ統合する。
- セーブ状態復元時に同じresolverからassetを再解決する。
