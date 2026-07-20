#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace KoromoEventScript.Runtime.Core.Execution
{

public sealed class RuntimeObjectStore
{
    private readonly List<List<RuntimeValue>?> arrays = new List<List<RuntimeValue>?>();
    private readonly List<Dictionary<string, RuntimeValue>?> instances = new List<Dictionary<string, RuntimeValue>?>();
    private readonly Dictionary<string, Dictionary<string, RuntimeValue>> externalInstances = new Dictionary<string, Dictionary<string, RuntimeValue>>(StringComparer.Ordinal);

    public RuntimeValue CreateArray(IEnumerable<RuntimeValue> values)
    {
        var handle = arrays.Count;
        arrays.Add(values.ToList());
        return RuntimeValue.ObjectReference(RuntimeReferenceKind.Array, handle);
    }

    public RuntimeValue CreateInstance(string classId)
    {
        if (string.IsNullOrEmpty(classId))
        {
            throw new ArgumentException("Class id must not be null or empty.", nameof(classId));
        }

        var handle = instances.Count;
        instances.Add(CreateInstanceFields(classId));
        return RuntimeValue.ObjectReference(RuntimeReferenceKind.Instance, handle);
    }

    public void EnsureActorReference(string referenceId)
    {
        if (string.IsNullOrEmpty(referenceId))
        {
            throw new ArgumentException("Reference id must not be null or empty.", nameof(referenceId));
        }

        if (externalInstances.ContainsKey(referenceId))
        {
            return;
        }

        externalInstances[referenceId] = CreateInstanceFields("Actor");
    }

    public bool TryGetArrayValue(RuntimeValue reference, int index, out RuntimeValue value, out string? error)
    {
        value = RuntimeValue.Null;
        error = null;

        if (!TryGetArray(reference, out var array))
        {
            error = $"Array handle '{reference.ObjectHandle}' does not exist.";
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

    public bool TrySetArrayValue(RuntimeValue reference, int index, RuntimeValue value, out string? error)
    {
        error = null;

        if (!TryGetArray(reference, out var array))
        {
            error = $"Array handle '{reference.ObjectHandle}' does not exist.";
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

    public bool TryGetArrayLength(RuntimeValue reference, out int length, out string? error)
    {
        length = 0;
        error = null;

        if (!TryGetArray(reference, out var array))
        {
            error = $"Array handle '{reference.ObjectHandle}' does not exist.";
            return false;
        }

        length = array.Count;
        return true;
    }

    public bool TryGetField(RuntimeValue reference, string fieldId, out RuntimeValue value, out string? error)
    {
        value = RuntimeValue.Null;
        error = null;

        if (!TryGetInstance(reference, out var fields))
        {
            error = $"Instance handle '{reference.ObjectHandle}' does not exist.";
            return false;
        }

        value = fields.TryGetValue(fieldId, out var storedValue) ? storedValue : RuntimeValue.Null;
        return true;
    }

    public bool TrySetField(RuntimeValue reference, string fieldId, RuntimeValue value, out string? error)
    {
        error = null;

        if (!TryGetInstance(reference, out var fields))
        {
            error = $"Instance handle '{reference.ObjectHandle}' does not exist.";
            return false;
        }

        fields[fieldId] = value;
        return true;
    }

    public bool TryGetField(string referenceId, string fieldId, out RuntimeValue value, out string? error)
    {
        EnsureActorReference(referenceId);
        value = externalInstances[referenceId].TryGetValue(fieldId, out var storedValue)
            ? storedValue
            : RuntimeValue.Null;
        error = null;
        return true;
    }

    public bool TrySetField(string referenceId, string fieldId, RuntimeValue value, out string? error)
    {
        EnsureActorReference(referenceId);
        externalInstances[referenceId][fieldId] = value;
        error = null;
        return true;
    }

    public bool TryDispose(RuntimeValue reference, out string? error)
    {
        error = null;

        if (reference.ReferenceKind == RuntimeReferenceKind.Array &&
            reference.ObjectHandle >= 0 &&
            reference.ObjectHandle < arrays.Count &&
            arrays[reference.ObjectHandle] != null)
        {
            arrays[reference.ObjectHandle] = null;
            return true;
        }

        if (reference.ReferenceKind == RuntimeReferenceKind.Instance &&
            reference.ObjectHandle >= 0 &&
            reference.ObjectHandle < instances.Count &&
            instances[reference.ObjectHandle] != null)
        {
            instances[reference.ObjectHandle] = null;
            return true;
        }

        if (reference.ReferenceKind == RuntimeReferenceKind.External &&
            !string.IsNullOrEmpty(reference.ReferenceId) &&
            externalInstances.Remove(reference.ReferenceId))
        {
            return true;
        }

        error = $"Object handle '{reference.ObjectHandle}' does not exist.";
        return false;
    }

    private bool TryGetArray(RuntimeValue reference, out List<RuntimeValue> array)
    {
        if (reference.Kind == RuntimeValueKind.Reference &&
            reference.ReferenceKind == RuntimeReferenceKind.Array &&
            reference.ObjectHandle >= 0 &&
            reference.ObjectHandle < arrays.Count &&
            arrays[reference.ObjectHandle] is List<RuntimeValue> stored)
        {
            array = stored;
            return true;
        }

        array = null!;
        return false;
    }

    private bool TryGetInstance(RuntimeValue reference, out Dictionary<string, RuntimeValue> fields)
    {
        if (reference.Kind == RuntimeValueKind.Reference &&
            reference.ReferenceKind == RuntimeReferenceKind.Instance &&
            reference.ObjectHandle >= 0 &&
            reference.ObjectHandle < instances.Count &&
            instances[reference.ObjectHandle] is Dictionary<string, RuntimeValue> stored)
        {
            fields = stored;
            return true;
        }

        if (reference.Kind == RuntimeValueKind.Reference &&
            reference.ReferenceKind == RuntimeReferenceKind.External &&
            !string.IsNullOrEmpty(reference.ReferenceId))
        {
            EnsureActorReference(reference.ReferenceId);
            fields = externalInstances[reference.ReferenceId];
            return true;
        }

        fields = null!;
        return false;
    }

    private static Dictionary<string, RuntimeValue> CreateInstanceFields(string classId)
    {
        return new Dictionary<string, RuntimeValue>(StringComparer.Ordinal)
        {
            ["__class"] = RuntimeValue.String(classId),
            ["isVisible"] = RuntimeValue.Bool(false),
        };
    }
}
}
