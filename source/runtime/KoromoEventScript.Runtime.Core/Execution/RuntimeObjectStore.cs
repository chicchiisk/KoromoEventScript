namespace KoromoEventScript.Runtime.Core.Execution;

public sealed class RuntimeObjectStore
{
    private readonly Dictionary<string, List<RuntimeValue>> arrays = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, RuntimeValue>> instances = new(StringComparer.Ordinal);
    private int nextArrayId;
    private int nextInstanceId;

    public RuntimeValue CreateArray(IEnumerable<RuntimeValue> values)
    {
        var referenceId = $"array:{nextArrayId++}";
        arrays[referenceId] = values.ToList();
        return RuntimeValue.Reference(referenceId);
    }

    public RuntimeValue CreateInstance(string classId)
    {
        ArgumentException.ThrowIfNullOrEmpty(classId);

        var referenceId = $"instance:{nextInstanceId++}";
        instances[referenceId] = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
        {
            ["__class"] = RuntimeValue.String(classId),
        };
        return RuntimeValue.Reference(referenceId);
    }

    public bool TryGetArrayValue(string referenceId, int index, out RuntimeValue value, out string? error)
    {
        value = RuntimeValue.Null;
        error = null;

        if (!arrays.TryGetValue(referenceId, out var array))
        {
            error = $"Array reference '{referenceId}' does not exist.";
            return false;
        }

        if (index < 0 || index >= array.Count)
        {
            error = $"Array index '{index}' is out of range.";
            return false;
        }

        value = array[index];
        return true;
    }

    public bool TrySetArrayValue(string referenceId, int index, RuntimeValue value, out string? error)
    {
        error = null;

        if (!arrays.TryGetValue(referenceId, out var array))
        {
            error = $"Array reference '{referenceId}' does not exist.";
            return false;
        }

        if (index < 0 || index >= array.Count)
        {
            error = $"Array index '{index}' is out of range.";
            return false;
        }

        array[index] = value;
        return true;
    }

    public bool TryGetArrayLength(string referenceId, out int length, out string? error)
    {
        length = 0;
        error = null;

        if (!arrays.TryGetValue(referenceId, out var array))
        {
            error = $"Array reference '{referenceId}' does not exist.";
            return false;
        }

        length = array.Count;
        return true;
    }

    public bool TryGetField(string referenceId, string fieldId, out RuntimeValue value, out string? error)
    {
        value = RuntimeValue.Null;
        error = null;

        if (!instances.TryGetValue(referenceId, out var fields))
        {
            error = $"Instance reference '{referenceId}' does not exist.";
            return false;
        }

        value = fields.TryGetValue(fieldId, out var storedValue) ? storedValue : RuntimeValue.Null;
        return true;
    }

    public bool TrySetField(string referenceId, string fieldId, RuntimeValue value, out string? error)
    {
        error = null;

        if (!instances.TryGetValue(referenceId, out var fields))
        {
            error = $"Instance reference '{referenceId}' does not exist.";
            return false;
        }

        fields[fieldId] = value;
        return true;
    }

    public bool TryDispose(string referenceId, out string? error)
    {
        error = null;

        if (arrays.Remove(referenceId) || instances.Remove(referenceId))
        {
            return true;
        }

        error = $"Object reference '{referenceId}' does not exist.";
        return false;
    }
}
