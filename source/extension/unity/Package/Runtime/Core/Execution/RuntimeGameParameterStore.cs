#nullable enable

using System;
using System.Collections.Generic;

namespace KoromoEventScript.Runtime.Core.Execution
{

public interface IRuntimeGameParameterStore
{
    void Set(string key, RuntimeValue value);

    bool TryGet(string key, out RuntimeValue value);
}

public sealed class RuntimeGameParameterStore : IRuntimeGameParameterStore
{
    private readonly Dictionary<string, RuntimeValue> values = new(StringComparer.Ordinal);

    public void Set(string key, RuntimeValue value)
    {
        values[key] = value;
    }

    public bool TryGet(string key, out RuntimeValue value)
    {
        return values.TryGetValue(key, out value!);
    }
}
}
