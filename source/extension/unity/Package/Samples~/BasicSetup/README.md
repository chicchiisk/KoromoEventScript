# Basic Setup

1. URP を有効にした Unity 6000.5.3f1 以降のプロジェクトへサンプルをインポートする。
2. シーン内の GameObject に `KesManager` を追加する。
3. `kes build --target unity`が生成した`manifest.kson`と`.klib`を`Assets/`配下へ配置する。
4. `manifest.kson`からimportされたKES Build Assetを`KesManager`へ割り当てる。
