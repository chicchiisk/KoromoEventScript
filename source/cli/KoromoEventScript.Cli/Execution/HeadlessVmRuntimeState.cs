namespace KoromoEventScript.Cli.Execution;

public enum HeadlessVmRuntimeValueKind
{
    Null = 0,
    String = 1,
    Number = 2,
    Bool = 3,
    Reference = 4,
}

public sealed record HeadlessVmRuntimeValue(
    HeadlessVmRuntimeValueKind Kind,
    string? StringValue = null,
    double? NumberValue = null,
    bool? BoolValue = null,
    string? ReferenceId = null)
{
    public static HeadlessVmRuntimeValue Null() => new(HeadlessVmRuntimeValueKind.Null);

    public static HeadlessVmRuntimeValue FromObject(object? value)
    {
        return value switch
        {
            null => Null(),
            string text => new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.String, StringValue: text),
            double number => new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Number, NumberValue: number),
            int number => new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Number, NumberValue: number),
            bool boolean => new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Bool, BoolValue: boolean),
            _ => new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Reference, ReferenceId: value.ToString()),
        };
    }

    public object? ToObject()
    {
        return Kind switch
        {
            HeadlessVmRuntimeValueKind.Null => null,
            HeadlessVmRuntimeValueKind.String => StringValue,
            HeadlessVmRuntimeValueKind.Number => NumberValue,
            HeadlessVmRuntimeValueKind.Bool => BoolValue,
            HeadlessVmRuntimeValueKind.Reference => ReferenceId,
            _ => null,
        };
    }
}

public sealed class HeadlessVmRuntimeState
{
    private readonly Stack<HeadlessVmRuntimeValue> operandStack = [];
    private readonly Stack<HeadlessVmFunctionFrame> functionFrames = [];

    public Dictionary<int, HeadlessVmRuntimeValue> VariableValues { get; } = [];

    public List<HeadlessVmCallFrameSnapshot> CallFrames { get; } = [];

    public HeadlessVmObjectStore ObjectStore { get; } = new();

    public int OperandCount => operandStack.Count;

    internal void PushFunctionFrame(HeadlessVmFunctionFrame frame)
    {
        functionFrames.Push(frame);
    }

    internal bool TryPopFunctionFrame(out HeadlessVmFunctionFrame frame)
    {
        if (functionFrames.Count == 0)
        {
            frame = null!;
            return false;
        }

        frame = functionFrames.Pop();
        return true;
    }

    public void PushOperand(HeadlessVmRuntimeValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        operandStack.Push(value);
    }

    public bool TryPopOperand(out HeadlessVmRuntimeValue? value)
    {
        if (operandStack.Count == 0)
        {
            value = null;
            return false;
        }

        value = operandStack.Pop();
        return true;
    }

    public bool TryPeekOperand(out HeadlessVmRuntimeValue? value)
    {
        if (operandStack.Count == 0)
        {
            value = null;
            return false;
        }

        value = operandStack.Peek();
        return true;
    }

    public IReadOnlyList<HeadlessVmRuntimeValue> ExportOperands()
    {
        return operandStack.Reverse().ToArray();
    }

    public void RestoreOperands(IEnumerable<HeadlessVmRuntimeValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        operandStack.Clear();
        foreach (var value in values)
        {
            PushOperand(value);
        }
    }
}

internal sealed record HeadlessVmSavedVariable(
    int Slot,
    bool WasInitialized,
    HeadlessVmRuntimeValue? Value);

internal sealed record HeadlessVmFunctionFrame(
    int FunctionIndex,
    int ReturnOffset,
    bool ExpectsReturnValue,
    IReadOnlyList<HeadlessVmSavedVariable> SavedVariables);
