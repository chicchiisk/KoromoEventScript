#nullable enable

using System;
using System.Collections.Generic;

namespace KoromoEventScript.Runtime.Core.Execution
{

public enum RuntimeArrayStorageKind
{
    Generic = 0,
    Number = 1,
    Bool = 2,
    String = 3,
}

public sealed class RuntimeObjectStore
{
    private readonly List<IRuntimeArray?> arrays = new List<IRuntimeArray?>();
    private readonly List<Dictionary<string, RuntimeValue>?> instances = new List<Dictionary<string, RuntimeValue>?>();
    private readonly Dictionary<string, Dictionary<string, RuntimeValue>> externalInstances = new Dictionary<string, Dictionary<string, RuntimeValue>>(StringComparer.Ordinal);

    public RuntimeValue CreateArray(IEnumerable<RuntimeValue> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var source = values as IReadOnlyList<RuntimeValue> ?? new List<RuntimeValue>(values);
        return RegisterArray(CreateBestArrayStorage(source));
    }

    public RuntimeValue CreateNumberArray(double[] values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        return RegisterArray(new NumberRuntimeArray(values));
    }

    public RuntimeValue CreateFilledArray(int count, RuntimeValue value)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Array length must be non-negative.");
        }

        switch (value.Kind)
        {
            case RuntimeValueKind.Number when value.NumberValue is double number:
                var numbers = new double[count];
                for (var index = 0; index < count; index++)
                {
                    numbers[index] = number;
                }

                return RegisterArray(new NumberRuntimeArray(numbers));

            case RuntimeValueKind.Bool when value.BoolValue is bool boolean:
                var booleans = new bool[count];
                if (boolean)
                {
                    for (var index = 0; index < count; index++)
                    {
                        booleans[index] = true;
                    }
                }

                return RegisterArray(new BoolRuntimeArray(booleans));

            case RuntimeValueKind.String:
                var strings = new string?[count];
                for (var index = 0; index < count; index++)
                {
                    strings[index] = value.StringValue;
                }

                return RegisterArray(new StringRuntimeArray(strings));

            default:
                var values = new RuntimeValue[count];
                for (var index = 0; index < count; index++)
                {
                    values[index] = value;
                }

                return RegisterArray(new GenericRuntimeArray(values));
        }
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

        value = array.Get(index);
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

        if (!array.TrySet(index, value))
        {
            array = new GenericRuntimeArray(array.ToRuntimeValues());
            arrays[reference.ObjectHandle] = array;
            array.TrySet(index, value);
        }

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

    public bool TryGetArrayStorageKind(RuntimeValue reference, out RuntimeArrayStorageKind storageKind)
    {
        if (TryGetArray(reference, out var array))
        {
            storageKind = array.StorageKind;
            return true;
        }

        storageKind = RuntimeArrayStorageKind.Generic;
        return false;
    }

    public bool TryGetNumberArrayValue(RuntimeValue reference, int index, out double value, out string? error)
    {
        value = 0;
        error = null;
        if (!TryGetArray(reference, out var array) || array is not NumberRuntimeArray numbers)
        {
            error = $"Array handle '{reference.ObjectHandle}' is not a number array.";
            return false;
        }

        if (index < 0 || index >= numbers.Count)
        {
            error = $"Array index '{index}' is out of range.";
            return false;
        }

        value = numbers.GetNumber(index);
        return true;
    }

    public bool TrySetNumberArrayValue(RuntimeValue reference, int index, double value, out string? error)
    {
        error = null;
        if (!TryGetArray(reference, out var array) || array is not NumberRuntimeArray numbers)
        {
            error = $"Array handle '{reference.ObjectHandle}' is not a number array.";
            return false;
        }

        if (index < 0 || index >= numbers.Count)
        {
            error = $"Array index '{index}' is out of range.";
            return false;
        }

        numbers.SetNumber(index, value);
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

    private RuntimeValue RegisterArray(IRuntimeArray array)
    {
        var handle = arrays.Count;
        arrays.Add(array);
        return RuntimeValue.ObjectReference(RuntimeReferenceKind.Array, handle);
    }

    private static IRuntimeArray CreateBestArrayStorage(IReadOnlyList<RuntimeValue> values)
    {
        if (values.Count == 0)
        {
            return new GenericRuntimeArray(Array.Empty<RuntimeValue>());
        }

        var kind = values[0].Kind;
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index].Kind != kind)
            {
                return new GenericRuntimeArray(CopyValues(values));
            }
        }

        switch (kind)
        {
            case RuntimeValueKind.Number:
                var numbers = new double[values.Count];
                for (var index = 0; index < values.Count; index++)
                {
                    if (values[index].NumberValue is not double number)
                    {
                        return new GenericRuntimeArray(CopyValues(values));
                    }

                    numbers[index] = number;
                }

                return new NumberRuntimeArray(numbers);

            case RuntimeValueKind.Bool:
                var booleans = new bool[values.Count];
                for (var index = 0; index < values.Count; index++)
                {
                    if (values[index].BoolValue is not bool boolean)
                    {
                        return new GenericRuntimeArray(CopyValues(values));
                    }

                    booleans[index] = boolean;
                }

                return new BoolRuntimeArray(booleans);

            case RuntimeValueKind.String:
                var strings = new string?[values.Count];
                for (var index = 0; index < values.Count; index++)
                {
                    strings[index] = values[index].StringValue;
                }

                return new StringRuntimeArray(strings);

            default:
                return new GenericRuntimeArray(CopyValues(values));
        }
    }

    private static RuntimeValue[] CopyValues(IReadOnlyList<RuntimeValue> values)
    {
        var copied = new RuntimeValue[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            copied[index] = values[index];
        }

        return copied;
    }

    private bool TryGetArray(RuntimeValue reference, out IRuntimeArray array)
    {
        if (reference.Kind == RuntimeValueKind.Reference &&
            reference.ReferenceKind == RuntimeReferenceKind.Array &&
            reference.ObjectHandle >= 0 &&
            reference.ObjectHandle < arrays.Count &&
            arrays[reference.ObjectHandle] is IRuntimeArray stored)
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

    private interface IRuntimeArray
    {
        int Count { get; }

        RuntimeArrayStorageKind StorageKind { get; }

        RuntimeValue Get(int index);

        bool TrySet(int index, RuntimeValue value);

        RuntimeValue[] ToRuntimeValues();
    }

    private sealed class GenericRuntimeArray : IRuntimeArray
    {
        private readonly RuntimeValue[] values;

        public GenericRuntimeArray(RuntimeValue[] values)
        {
            this.values = values;
        }

        public int Count => values.Length;

        public RuntimeArrayStorageKind StorageKind => RuntimeArrayStorageKind.Generic;

        public RuntimeValue Get(int index) => values[index];

        public bool TrySet(int index, RuntimeValue value)
        {
            values[index] = value;
            return true;
        }

        public RuntimeValue[] ToRuntimeValues() => (RuntimeValue[])values.Clone();
    }

    private sealed class NumberRuntimeArray : IRuntimeArray
    {
        private readonly double[] values;

        public NumberRuntimeArray(double[] values)
        {
            this.values = values;
        }

        public int Count => values.Length;

        public RuntimeArrayStorageKind StorageKind => RuntimeArrayStorageKind.Number;

        public RuntimeValue Get(int index) => RuntimeValue.Number(values[index]);

        public double GetNumber(int index) => values[index];

        public void SetNumber(int index, double value) => values[index] = value;

        public bool TrySet(int index, RuntimeValue value)
        {
            if (value.Kind != RuntimeValueKind.Number || value.NumberValue is not double number)
            {
                return false;
            }

            values[index] = number;
            return true;
        }

        public RuntimeValue[] ToRuntimeValues()
        {
            var result = new RuntimeValue[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                result[index] = RuntimeValue.Number(values[index]);
            }

            return result;
        }
    }

    private sealed class BoolRuntimeArray : IRuntimeArray
    {
        private readonly bool[] values;

        public BoolRuntimeArray(bool[] values)
        {
            this.values = values;
        }

        public int Count => values.Length;

        public RuntimeArrayStorageKind StorageKind => RuntimeArrayStorageKind.Bool;

        public RuntimeValue Get(int index) => RuntimeValue.Bool(values[index]);

        public bool TrySet(int index, RuntimeValue value)
        {
            if (value.Kind != RuntimeValueKind.Bool || value.BoolValue is not bool boolean)
            {
                return false;
            }

            values[index] = boolean;
            return true;
        }

        public RuntimeValue[] ToRuntimeValues()
        {
            var result = new RuntimeValue[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                result[index] = RuntimeValue.Bool(values[index]);
            }

            return result;
        }
    }

    private sealed class StringRuntimeArray : IRuntimeArray
    {
        private readonly string?[] values;

        public StringRuntimeArray(string?[] values)
        {
            this.values = values;
        }

        public int Count => values.Length;

        public RuntimeArrayStorageKind StorageKind => RuntimeArrayStorageKind.String;

        public RuntimeValue Get(int index) => RuntimeValue.String(values[index] ?? string.Empty);

        public bool TrySet(int index, RuntimeValue value)
        {
            if (value.Kind != RuntimeValueKind.String)
            {
                return false;
            }

            values[index] = value.StringValue;
            return true;
        }

        public RuntimeValue[] ToRuntimeValues()
        {
            var result = new RuntimeValue[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                result[index] = RuntimeValue.String(values[index] ?? string.Empty);
            }

            return result;
        }
    }
}
}
