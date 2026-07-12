# KoromoEventScript ランタイムマニフェスト仕様書

本仕様書は、`kes build` が生成し、各ランタイムが読み込むランタイムマニフェストの共通契約を定義する。機械検証可能な正式 schema は [runtime-manifest.schema.json](runtime-manifest.schema.json) とする。

## ファイル名と形式

| target | ファイル名 | 内容 |
|---|---|---|
| `windows` | `manifest.json` | UTF-8 JSON |
| `unity` | `manifest.kson` | UTF-8 JSON。Unity の専用 `ScriptedImporter` を選択するため拡張子だけを変更する |
| `unreal` | `manifest.json` | UTF-8 JSON |

`.kson` は独自のデータ記法ではなく、JSON と同じ字句・構文・値表現を使用する。BOM は付けない。プロパティ名は大文字小文字を区別する。

## schema version

- ルートの `schemaVersion` は必須とし、`MAJOR.MINOR` 形式で記録する。
- runtime は同じ MAJOR の未知の MINOR を、未知フィールドを無視できる場合に限って読み込んでよい。
- MAJOR が異なる場合は実行開始前の互換性エラーとする。
- schema `1.0` では正式 schema にない未知フィールドを許可しない。

## パス規則

- `klibPath`、`klibTextPath`、`sourcePath`、`entryEventListPath`、`inputs[].path`、`assets[].path` は `/` 区切りの相対パスとする。
- `klibPath` はマニフェストを含む build output root を基準に解決する。
- `.klib`等のruntime artifact pathでは`..`、絶対パス、URI、ドライブ文字を禁止する。
- Windows/Unrealの`assets[].path`は既存build構成との互換性のため`../`を許容するが、解決後の絶対パスがruntime packageまたは明示されたasset root内にあることを検証する。
- `sourcePath` と `inputs[].path` は診断表示用であり、runtime がソースファイルを開くことを要求しない。
- Unity targetでは`assets[].path`を素材解決に使用しない。Unityは`assetId`をAddressables keyとして使用する。

## ルートプロパティ

| プロパティ | 必須 | 意味 |
|---|---:|---|
| `schemaVersion` | yes | manifest schema version |
| `gameId` | yes | ゲームを識別する安定ID |
| `title` | yes | 表示名 |
| `defaultLocale` | yes | 基準locale |
| `cliVersion` | yes | 生成したCLI version |
| `target` | yes | `windows`、`unity`、`unreal` |
| `entryEventListPath` | yes | build入力となったentry `.kel` |
| `inputs` | yes | build入力一覧 |
| `scripts` | yes | 基準localeの`.klib`一覧 |
| `events` | yes | `.kel`から生成したevent一覧 |
| `assets` | yes | runtime asset参照一覧 |
| `defaults` | yes | runtime初期表示設定 |
| `build` | yes | build識別情報 |
| `localizations` | yes | locale別`.klib`一覧 |

配列の順序は保持する。特に`events`の順序は、複数trigger成立時の優先順位として意味を持つ。

## scripts

`scripts`と`localizations[].scripts`は次の構造を持つ。

| プロパティ | 必須 | 意味 |
|---|---:|---|
| `scriptId` | yes | `.klib` Module Infoと一致する安定ID |
| `locale` | yes | このartifactのlocale |
| `isEntry` | yes | entry scriptか |
| `startLabel` | yes | 開始public label。存在しない場合は`null` |
| `sourcePath` | yes | 診断用source path |
| `klibPath` | yes | runtimeが読む`.klib`への相対パス |
| `klibTextPath` | yes | 補助`.klibtxt`への相対パス。未生成時は`null` |

同一locale内で`scriptId`は一意でなければならない。`isEntry=true`のscriptは1件だけとする。

## eventsとtrigger

- `eventId`はmanifest内で一意とする。
- `scriptId`は同じlocaleで解決可能なscriptを参照する。
- `trigger.conditions`内はAND、`trigger.or`内の各triggerはORとして評価する。
- `kind=from`では`from`だけを使用する。
- `kind=is`では`param`と`value`を使用する。
- `value.kind`は`string`、`number`、`bool`とし、`text`を対応型へ変換して比較する。
- `isEntry=true`のeventは1件だけとする。

## assets

| プロパティ | 必須 | 意味 |
|---|---:|---|
| `assetId` | yes | runtime共通の論理asset ID。UnityではAddressables key |
| `kind` | yes | `background`、`actor`、`bgm`、`se`、`voice`、`ui` |
| `path` | target依存 | Windows/Unreal素材パス。Unityでは省略または`null` |
| `locale` | yes | locale非依存なら`null`、依存する場合はlocale文字列 |

`assetId`と`locale`の組は一意でなければならない。Unity runtimeは要求localeと一致するentryを優先し、存在しなければ`locale=null`へフォールバックする。Addressablesに同じkeyが複数登録され、要求型を一意に解決できない場合は実行時エラーとする。

## build互換性

`build.buildId`は同一成果物集合を識別する非空文字列とする。セーブ状態の復元時は保存された`gameId`、manifest schema MAJOR、`buildId`を検証する。異なる`buildId`間の互換性をゲーム側が保証する場合は、ホストが明示的に復元を許可できる。

## 検証責務

CLIは出力前にJSON Schemaと上記の相互参照制約を検証する。runtimeまたはimporterも信頼境界として再検証する。JSON Schemaだけでは表現しない一意性、参照整合性、path traversal、entry件数はsemantic validationとして扱う。
