# Windows runtime 開発前提チェック

Windows runtime の実装や手元確認を始める前に、次のチェックを実行する。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows-runtime/Check-WinUiPrerequisites.ps1
```

このチェックは、以下の状態を一度に表示する。

- .NET SDK 8.0 以上
- WinApp CLI 0.3 以上
- WinUI 3 template
- Developer Mode

Developer Mode が無効な場合、このチェックは失敗する。Windows runtime の実装、ビルド、起動確認へ進まず、WinUI setup 手順で Developer Mode を有効化してから再実行する。

Windows runtime のビルドと起動確認は、`winapp run` または `BuildAndRun.ps1` 相当の手順で行う。packaged exe を直接起動して確認してはならない。直接起動では WinUI の診断が失われたり、アプリが無言で終了したりするため、開発時の確認手順として扱わない。
