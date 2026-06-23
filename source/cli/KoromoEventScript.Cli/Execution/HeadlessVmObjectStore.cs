namespace KoromoEventScript.Cli.Execution;

public sealed class HeadlessVmObjectStore
{
    private readonly Dictionary<string, List<HeadlessVmRuntimeValue>> arrays = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, HeadlessVmRuntimeValue>> instances = new(StringComparer.Ordinal);
    private int nextArrayId = 1;
    private int nextInstanceId = 1;

    public HeadlessVmRuntimeValue CreateArray(IReadOnlyList<HeadlessVmRuntimeValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var referenceId = $"array:{nextArrayId++}";
        arrays[referenceId] = values.ToList();
        return new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Reference, ReferenceId: referenceId);
    }

    public bool TryGetArrayValue(string referenceId, int index, out HeadlessVmRuntimeValue value, out string? error)
    {
        if (!arrays.TryGetValue(referenceId, out var array))
        {
            value = HeadlessVmRuntimeValue.Null();
            error = $"Array reference '{referenceId}' does not exist.";
            return false;
        }

        if (index < 0 || index >= array.Count)
        {
            value = HeadlessVmRuntimeValue.Null();
            error = $"Array index '{index}' is out of range.";
            return false;
        }

        value = array[index];
        error = null;
        return true;
    }

    public bool TrySetArrayValue(string referenceId, int index, HeadlessVmRuntimeValue value, out string? error)
    {
        ArgumentNullException.ThrowIfNull(value);
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
        error = null;
        return true;
    }

    public bool TryGetArrayLength(string referenceId, out int length, out string? error)
    {
        if (!arrays.TryGetValue(referenceId, out var array))
        {
            length = 0;
            error = $"Array reference '{referenceId}' does not exist.";
            return false;
        }

        length = array.Count;
        error = null;
        return true;
    }

    public HeadlessVmRuntimeValue CreateInstance(string classId)
    {
        ArgumentException.ThrowIfNullOrEmpty(classId);
        var referenceId = $"instance:{nextInstanceId++}";
        instances[referenceId] = CreateInstanceFields(classId);
        return new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Reference, ReferenceId: referenceId);
    }

    public void EnsureActorReference(string referenceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(referenceId);
        if (instances.ContainsKey(referenceId))
        {
            return;
        }

        instances[referenceId] = CreateInstanceFields("Actor");
    }

    public bool TryGetField(string referenceId, string fieldId, out HeadlessVmRuntimeValue value, out string? error)
    {
        if (!instances.TryGetValue(referenceId, out var fields))
        {
            value = HeadlessVmRuntimeValue.Null();
            error = $"Object reference '{referenceId}' does not exist.";
            return false;
        }

        value = fields.TryGetValue(fieldId, out var storedValue)
            ? storedValue
            : HeadlessVmRuntimeValue.Null();
        error = null;
        return true;
    }

    public bool TryGetActorField(string referenceId, string fieldId, out HeadlessVmRuntimeValue value, out bool exists, out string? error)
    {
        EnsureActorReference(referenceId);
        if (!instances.TryGetValue(referenceId, out var fields))
        {
            value = HeadlessVmRuntimeValue.Null();
            exists = false;
            error = $"Actor reference '{referenceId}' does not exist.";
            return false;
        }

        exists = fields.TryGetValue(fieldId, out var storedValue);
        value = exists ? storedValue! : HeadlessVmRuntimeValue.Null();
        error = null;
        return true;
    }

    public bool TrySetField(string referenceId, string fieldId, HeadlessVmRuntimeValue value, out string? error)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!instances.TryGetValue(referenceId, out var fields))
        {
            error = $"Object reference '{referenceId}' does not exist.";
            return false;
        }

        fields[fieldId] = value;
        error = null;
        return true;
    }

    public bool TryDispose(string referenceId, out string? error)
    {
        if (arrays.Remove(referenceId) || instances.Remove(referenceId))
        {
            error = null;
            return true;
        }

        error = $"Object reference '{referenceId}' does not exist.";
        return false;
    }

    public IReadOnlyList<HeadlessVmObjectSnapshot> ExportSnapshots()
    {
        var arraySnapshots = arrays
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new HeadlessVmObjectSnapshot(
                pair.Key,
                HeadlessVmObjectSnapshotKind.Array,
                ArrayItems: pair.Value.Select(HeadlessVmObjectSnapshot.ToSnapshotValue).ToArray()))
            .ToArray();
        var instanceSnapshots = instances
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new HeadlessVmObjectSnapshot(
                pair.Key,
                HeadlessVmObjectSnapshotKind.Instance,
                Fields: pair.Value
                    .OrderBy(static field => field.Key, StringComparer.Ordinal)
                    .Select(static field => new HeadlessVmObjectFieldSnapshot(
                        field.Key,
                        HeadlessVmObjectSnapshot.ToSnapshotValue(field.Value)))
                    .ToArray()))
            .ToArray();

        return arraySnapshots.Concat(instanceSnapshots).ToArray();
    }

    public void RestoreSnapshots(IEnumerable<HeadlessVmObjectSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        arrays.Clear();
        instances.Clear();
        nextArrayId = 1;
        nextInstanceId = 1;

        foreach (var snapshot in snapshots)
        {
            switch (snapshot.Kind)
            {
                case HeadlessVmObjectSnapshotKind.Array:
                    arrays[snapshot.ReferenceId] = (snapshot.ArrayItems ?? [])
                        .Select(HeadlessVmObjectSnapshot.ToRuntimeValue)
                        .ToList();
                    nextArrayId = Math.Max(nextArrayId, ParseNextId(snapshot.ReferenceId, "array:"));
                    break;

                case HeadlessVmObjectSnapshotKind.Instance:
                    instances[snapshot.ReferenceId] = (snapshot.Fields ?? [])
                        .ToDictionary(
                            static field => field.FieldId,
                            static field => HeadlessVmObjectSnapshot.ToRuntimeValue(field.Value),
                            StringComparer.Ordinal);
                    nextInstanceId = Math.Max(nextInstanceId, ParseNextId(snapshot.ReferenceId, "instance:"));
                    break;
            }
        }
    }

    private static int ParseNextId(string referenceId, string prefix)
    {
        if (!referenceId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 1;
        }

        return int.TryParse(referenceId[prefix.Length..], out var parsedId)
            ? parsedId + 1
            : 1;
    }

    private static Dictionary<string, HeadlessVmRuntimeValue> CreateInstanceFields(string classId)
    {
        return new Dictionary<string, HeadlessVmRuntimeValue>(StringComparer.Ordinal)
        {
            ["__class"] = new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.String, StringValue: classId),
            ["isVisible"] = new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Bool, BoolValue: false),
        };
    }
}
