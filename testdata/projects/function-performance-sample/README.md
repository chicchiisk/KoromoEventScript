# 関数パフォーマンスサンプル

`104707` が素数かどうかを、KES のユーザー定義関数、動的長の `bool[]`、
エラトステネスのふるいで判定する実装です。
Unity サンプルの `Tools > KoromoEventScript > Run Prime Sieve Benchmark` は、同じ処理の
C# 実装と KES VM 実装をウォームアップ後に各 100 回実行し、合計・平均・中央値・最小・
最大・標準偏差と速度比を Console に出力します。

```powershell
dotnet run --project source/cli/KoromoEventScript.Cli -- build testdata/projects/function-performance-sample --target unity
```

生成された `build/unity/events/prime_sieve.klib` を Unity サンプルの
`Assets/Benchmarks/prime_sieve.klib` に配置して計測します。
