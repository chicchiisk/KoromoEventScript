using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Execution;

public sealed class KesVmExecutor
{
    private const int DefaultMaxInstructionCount = 10_000;

    public static IReadOnlySet<KlibOpCode> DispatchedOpCodes { get; } = new HashSet<KlibOpCode>
    {
        KlibOpCode.PushConst,
        KlibOpCode.PushTrue,
        KlibOpCode.PushFalse,
        KlibOpCode.PushNull,
        KlibOpCode.PushInt,
        KlibOpCode.Pop,
        KlibOpCode.Dup,
        KlibOpCode.LoadVar,
        KlibOpCode.StoreVar,
        KlibOpCode.DefVar,
        KlibOpCode.Add,
        KlibOpCode.Sub,
        KlibOpCode.Mul,
        KlibOpCode.Div,
        KlibOpCode.Neg,
        KlibOpCode.Eq,
        KlibOpCode.Neq,
        KlibOpCode.Lt,
        KlibOpCode.Le,
        KlibOpCode.Gt,
        KlibOpCode.Ge,
        KlibOpCode.And,
        KlibOpCode.Or,
        KlibOpCode.Not,
        KlibOpCode.Jump,
        KlibOpCode.JumpFalse,
        KlibOpCode.Label,
        KlibOpCode.Select,
        KlibOpCode.End,
        KlibOpCode.Call,
        KlibOpCode.CallVoid,
        KlibOpCode.SysCall,
        KlibOpCode.SysCallVoid,
        KlibOpCode.ArrayNew,
        KlibOpCode.ArrayGet,
        KlibOpCode.ArraySet,
        KlibOpCode.New,
        KlibOpCode.GetField,
        KlibOpCode.SetField,
        KlibOpCode.CallMethod,
        KlibOpCode.CallMethodVoid,
        KlibOpCode.Dispose,
    };

    public KesVmExecutionResult Run(KesVmSession session, int maxInstructionCount = DefaultMaxInstructionCount)
    {
        ArgumentNullException.ThrowIfNull(session);

        var executed = 0;
        while (session.Continuation.Kind == RuntimeContinuationKind.Running)
        {
            if (executed++ >= maxInstructionCount)
            {
                return Fault(session, "KESR3198", "VM execution exceeded the maximum instruction count.");
            }

            var instruction = session.CurrentInstruction();
            if (instruction is null)
            {
                return Fault(session, "KESR3100", $"Instruction index '{session.Position.InstructionIndex}' does not exist.");
            }

            var result = Execute(session, instruction);
            if (!result.Succeeded)
            {
                return result;
            }
        }

        return KesVmExecutionResult.Success();
    }

    public KesVmExecutionResult ChooseSelection(KesVmSession session, int choiceIndex)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Continuation.Kind != RuntimeContinuationKind.WaitingForSelection)
        {
            return Fault(session, "KESR3205", "Session is not waiting for a selection.");
        }

        if (choiceIndex < 0 || choiceIndex >= session.Continuation.PendingChoices.Count)
        {
            return Fault(session, "KESR3206", $"Selection choice index '{choiceIndex}' is out of range.");
        }

        var target = session.Continuation.PendingChoices[choiceIndex].TargetInstructionIndex;
        session.SetInstructionIndex(target);
        session.SetContinuation(RuntimeContinuation.Running);
        return KesVmExecutionResult.Success();
    }

    private static KesVmExecutionResult Execute(KesVmSession session, KlibInstruction instruction)
    {
        switch (instruction.OpCode)
        {
            case KlibOpCode.Label:
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.Jump:
                if (!TryReadOperand(instruction, 0, out var jumpOffset))
                {
                    return Fault(session, "KESR3201", "JUMP target operand is missing.");
                }

                return JumpToRelativeOffset(session, instruction, jumpOffset);

            case KlibOpCode.JumpFalse:
                if (!TryReadOperand(instruction, 0, out var jumpFalseOffset))
                {
                    return Fault(session, "KESR3201", "JUMP_FALSE target operand is missing.");
                }

                if (!session.TryPopOperand(out var condition) ||
                    condition.Kind != RuntimeValueKind.Bool ||
                    condition.BoolValue is null)
                {
                    return Fault(session, "KESR3202", "JUMP_FALSE requires a bool operand.");
                }

                return condition.BoolValue.Value
                    ? Advance(session, instruction)
                    : JumpToRelativeOffset(session, instruction, jumpFalseOffset);

            case KlibOpCode.Select:
                return WaitForSelection(session, instruction);

            case KlibOpCode.End:
                session.SetContinuation(RuntimeContinuation.Completed);
                return KesVmExecutionResult.Success();

            case KlibOpCode.PushNull:
                session.PushOperand(RuntimeValue.Null);
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.PushConst:
                if (!TryReadOperand(instruction, 0, out var constantIndex) ||
                    constantIndex < 0 ||
                    constantIndex >= session.Document.Constants.Count)
                {
                    return Fault(session, "KESR3103", $"Constant index is invalid for {instruction.OpCode}.");
                }

                session.PushOperand(ResolveConstant(session.Document.Constants[constantIndex]));
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.PushTrue:
                session.PushOperand(RuntimeValue.Bool(true));
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.PushFalse:
                session.PushOperand(RuntimeValue.Bool(false));
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.PushInt:
                if (!TryReadOperand(instruction, 0, out var intValue))
                {
                    return Fault(session, "KESR3103", $"Integer operand is missing for {instruction.OpCode}.");
                }

                session.PushOperand(RuntimeValue.Number(intValue));
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.Pop:
                if (!session.TryPopOperand(out _))
                {
                    return Fault(session, "KESR3101", "Operand stack underflow while executing POP.");
                }

                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.Dup:
                if (!session.TryPeekOperand(out var duplicated))
                {
                    return Fault(session, "KESR3101", "Operand stack underflow while executing DUP.");
                }

                session.PushOperand(duplicated);
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.LoadVar:
                if (!TryReadOperand(instruction, 0, out var loadSlot))
                {
                    return Fault(session, "KESR3103", "Variable slot operand is missing for LOAD_VAR.");
                }

                if (!session.TryGetVariable(loadSlot, out var loadedValue))
                {
                    return Fault(session, "KESR3102", $"Variable slot '{loadSlot}' is not initialized.");
                }

                session.PushOperand(loadedValue);
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.StoreVar:
            case KlibOpCode.DefVar:
                if (!TryReadOperand(instruction, 0, out var storeSlot))
                {
                    return Fault(session, "KESR3103", $"Variable slot operand is missing for {instruction.OpCode}.");
                }

                if (!session.TryPopOperand(out var storedValue))
                {
                    return Fault(session, "KESR3101", $"Operand stack underflow while executing {instruction.OpCode}.");
                }

                session.SetVariable(storeSlot, storedValue);
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.Add:
                return ApplyNumberBinary(session, instruction, static (left, right) => left + right);

            case KlibOpCode.Sub:
                return ApplyNumberBinary(session, instruction, static (left, right) => left - right);

            case KlibOpCode.Mul:
                return ApplyNumberBinary(session, instruction, static (left, right) => left * right);

            case KlibOpCode.Div:
                return ApplyNumberBinary(session, instruction, static (left, right) => left / right);

            case KlibOpCode.Neg:
                if (!session.TryPopOperand(out var negValue) ||
                    negValue.Kind != RuntimeValueKind.Number ||
                    negValue.NumberValue is null)
                {
                    return Fault(session, "KESR3104", "Unary NEG requires a number operand.");
                }

                session.PushOperand(RuntimeValue.Number(-negValue.NumberValue.Value));
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.Eq:
                return ApplyEquality(session, instruction, equals: true);

            case KlibOpCode.Neq:
                return ApplyEquality(session, instruction, equals: false);

            case KlibOpCode.Lt:
                return ApplyNumberComparison(session, instruction, static (left, right) => left < right);

            case KlibOpCode.Le:
                return ApplyNumberComparison(session, instruction, static (left, right) => left <= right);

            case KlibOpCode.Gt:
                return ApplyNumberComparison(session, instruction, static (left, right) => left > right);

            case KlibOpCode.Ge:
                return ApplyNumberComparison(session, instruction, static (left, right) => left >= right);

            case KlibOpCode.And:
                return ApplyBoolBinary(session, instruction, static (left, right) => left && right);

            case KlibOpCode.Or:
                return ApplyBoolBinary(session, instruction, static (left, right) => left || right);

            case KlibOpCode.Not:
                if (!session.TryPopOperand(out var notValue) ||
                    notValue.Kind != RuntimeValueKind.Bool ||
                    notValue.BoolValue is null)
                {
                    return Fault(session, "KESR3105", "Unary NOT requires a bool operand.");
                }

                session.PushOperand(RuntimeValue.Bool(!notValue.BoolValue.Value));
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.ArrayNew:
                if (!TryReadOperand(instruction, 0, out var arrayCount) || arrayCount < 0)
                {
                    return Fault(session, "KESR3301", "ARRAY_NEW requires a non-negative count operand.");
                }

                var arrayValues = PopArguments(session, arrayCount);
                if (arrayValues is null)
                {
                    return Fault(session, "KESR3101", "Not enough operands to build an array.");
                }

                session.PushOperand(session.ObjectStore.CreateArray(arrayValues));
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.ArrayGet:
                if (!TryPopIndexAndReference(session, "ARRAY_GET", out var getReferenceId, out var getIndex, out var getFault))
                {
                    return getFault!;
                }

                if (!session.ObjectStore.TryGetArrayValue(getReferenceId!, getIndex, out var arrayValue, out var arrayError))
                {
                    return Fault(session, "KESR3302", arrayError ?? "ARRAY_GET failed.");
                }

                session.PushOperand(arrayValue);
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.ArraySet:
                if (!session.TryPopOperand(out var assignedValue))
                {
                    return Fault(session, "KESR3101", "Operand stack underflow while executing ARRAY_SET.");
                }

                if (!TryPopIndexAndReference(session, "ARRAY_SET", out var setReferenceId, out var setIndex, out var setFault))
                {
                    return setFault!;
                }

                if (!session.ObjectStore.TrySetArrayValue(setReferenceId!, setIndex, assignedValue, out var setError))
                {
                    return Fault(session, "KESR3302", setError ?? "ARRAY_SET failed.");
                }

                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.New:
                if (!TryReadOperand(instruction, 0, out var classIndex) ||
                    !TryReadOperand(instruction, 1, out var constructorArgc) ||
                    constructorArgc < 0)
                {
                    return Fault(session, "KESR3303", "NEW requires class and argc operands.");
                }

                if (PopArguments(session, constructorArgc) is null)
                {
                    return Fault(session, "KESR3101", "Not enough constructor arguments on the stack.");
                }

                if (!TryResolveString(session.Document, classIndex, out var classId, out var classError))
                {
                    return Fault(session, "KESR3303", classError ?? "Class reference could not be resolved.");
                }

                session.PushOperand(session.ObjectStore.CreateInstance(classId!));
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.GetField:
                if (!TryReadOperand(instruction, 0, out var getFieldIndex))
                {
                    return Fault(session, "KESR3304", "GET_FIELD field operand is missing.");
                }

                if (!TryResolveString(session.Document, getFieldIndex, out var getFieldId, out var getFieldResolveError))
                {
                    return Fault(session, "KESR3304", getFieldResolveError ?? "GET_FIELD field reference could not be resolved.");
                }

                if (!TryPopReference(session, "GET_FIELD", out var getReceiverId, out var getReceiverFault))
                {
                    return getReceiverFault!;
                }

                if (!session.ObjectStore.TryGetField(getReceiverId!, getFieldId!, out var fieldValue, out var getFieldError))
                {
                    return Fault(session, "KESR3304", getFieldError ?? "GET_FIELD failed.");
                }

                session.PushOperand(fieldValue);
                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.SetField:
                if (!TryReadOperand(instruction, 0, out var setFieldIndex))
                {
                    return Fault(session, "KESR3304", "SET_FIELD field operand is missing.");
                }

                if (!TryResolveString(session.Document, setFieldIndex, out var setFieldId, out var setFieldResolveError))
                {
                    return Fault(session, "KESR3304", setFieldResolveError ?? "SET_FIELD field reference could not be resolved.");
                }

                if (!session.TryPopOperand(out var setFieldValue))
                {
                    return Fault(session, "KESR3101", "Operand stack underflow while executing SET_FIELD.");
                }

                if (!TryPopReference(session, "SET_FIELD", out var setReceiverId, out var setReceiverFault))
                {
                    return setReceiverFault!;
                }

                if (!session.ObjectStore.TrySetField(setReceiverId!, setFieldId!, setFieldValue, out var setFieldError))
                {
                    return Fault(session, "KESR3304", setFieldError ?? "SET_FIELD failed.");
                }

                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            case KlibOpCode.Call:
            case KlibOpCode.CallVoid:
                return ExecuteCall(session, instruction);

            case KlibOpCode.CallMethod:
            case KlibOpCode.CallMethodVoid:
                return ExecuteMethodCall(session, instruction);

            case KlibOpCode.SysCall:
            case KlibOpCode.SysCallVoid:
                return Fault(session, "KESR3400", "Runtime syscall dispatch is not connected yet.");

            case KlibOpCode.Dispose:
                if (!TryPopReference(session, "DISPOSE", out var disposeReferenceId, out var disposeFault))
                {
                    return disposeFault!;
                }

                if (!session.ObjectStore.TryDispose(disposeReferenceId!, out var disposeError))
                {
                    return Fault(session, "KESR3305", disposeError ?? "DISPOSE failed.");
                }

                session.AdvanceAfter(instruction);
                return KesVmExecutionResult.Success();

            default:
                return Fault(session, "KESR3199", $"Opcode '{instruction.OpCode}' is not supported by this executor task.");
        }
    }

    private static KesVmExecutionResult Advance(KesVmSession session, KlibInstruction instruction)
    {
        session.AdvanceAfter(instruction);
        return KesVmExecutionResult.Success();
    }

    private static KesVmExecutionResult JumpToRelativeOffset(KesVmSession session, KlibInstruction instruction, int relativeOffset)
    {
        var targetOffset = instruction.Offset + GetInstructionSize(instruction) + relativeOffset;
        var target = session.Document.Instructions.FirstOrDefault(candidate => candidate.Offset == targetOffset);
        if (target is null)
        {
            return Fault(session, "KESR3203", $"Jump target offset '{targetOffset}' does not exist.");
        }

        session.SetInstructionIndex(target.Index);
        return KesVmExecutionResult.Success();
    }

    private static KesVmExecutionResult WaitForSelection(KesVmSession session, KlibInstruction instruction)
    {
        if (!session.TryPopOperand(out var promptValue))
        {
            return Fault(session, "KESR3101", "Operand stack underflow while executing SELECT.");
        }

        if (promptValue.Kind is not RuntimeValueKind.Null and not RuntimeValueKind.String)
        {
            return Fault(session, "KESR3204", "SELECT prompt must be a string or null.");
        }

        if (instruction.SelectCases is null || instruction.SelectCases.Count == 0)
        {
            return Fault(session, "KESR3204", "SELECT requires at least one case.");
        }

        var prompt = promptValue.Kind == RuntimeValueKind.String ? promptValue.StringValue : null;
        var baseOffset = instruction.Offset + GetInstructionSize(instruction);
        var offsets = new List<int>();
        var choices = new List<RuntimeSelectionChoice>();

        foreach (var selectCase in instruction.SelectCases)
        {
            if (selectCase.TextIndex < 0 || selectCase.TextIndex >= session.Document.Constants.Count)
            {
                return Fault(session, "KESR3204", $"SELECT case text index '{selectCase.TextIndex}' is invalid.");
            }

            var textConstant = session.Document.Constants[selectCase.TextIndex];
            if (textConstant.Kind != KlibConstantKind.String || textConstant.StringValue is null)
            {
                return Fault(session, "KESR3204", "SELECT case text must resolve to a string constant.");
            }

            var targetOffset = baseOffset + selectCase.Offset;
            var target = session.Document.Instructions.FirstOrDefault(candidate => candidate.Offset == targetOffset);
            if (target is null)
            {
                return Fault(session, "KESR3203", $"SELECT case target offset '{targetOffset}' does not exist.");
            }

            offsets.Add(selectCase.Offset);
            choices.Add(new RuntimeSelectionChoice(textConstant.StringValue, target.Index));
        }

        session.SetContinuation(new RuntimeContinuation(
            RuntimeContinuationKind.WaitingForSelection,
            instruction.Index,
            offsets,
            prompt,
            choices));
        return KesVmExecutionResult.Success();
    }

    private static KesVmExecutionResult ApplyNumberBinary(
        KesVmSession session,
        KlibInstruction instruction,
        Func<double, double, double> operation)
    {
        if (!TryPopNumberOperands(session, out var left, out var right))
        {
            return Fault(session, "KESR3104", "Numeric binary opcode requires two number operands.");
        }

        session.PushOperand(RuntimeValue.Number(operation(left, right)));
        session.AdvanceAfter(instruction);
        return KesVmExecutionResult.Success();
    }

    private static KesVmExecutionResult ApplyNumberComparison(
        KesVmSession session,
        KlibInstruction instruction,
        Func<double, double, bool> operation)
    {
        if (!TryPopNumberOperands(session, out var left, out var right))
        {
            return Fault(session, "KESR3104", "Numeric comparison opcode requires two number operands.");
        }

        session.PushOperand(RuntimeValue.Bool(operation(left, right)));
        session.AdvanceAfter(instruction);
        return KesVmExecutionResult.Success();
    }

    private static KesVmExecutionResult ApplyBoolBinary(
        KesVmSession session,
        KlibInstruction instruction,
        Func<bool, bool, bool> operation)
    {
        if (!session.TryPopOperand(out var right) ||
            !session.TryPopOperand(out var left) ||
            left.Kind != RuntimeValueKind.Bool ||
            right.Kind != RuntimeValueKind.Bool ||
            left.BoolValue is null ||
            right.BoolValue is null)
        {
            return Fault(session, "KESR3105", "Logical binary opcode requires two bool operands.");
        }

        session.PushOperand(RuntimeValue.Bool(operation(left.BoolValue.Value, right.BoolValue.Value)));
        session.AdvanceAfter(instruction);
        return KesVmExecutionResult.Success();
    }

    private static KesVmExecutionResult ApplyEquality(KesVmSession session, KlibInstruction instruction, bool equals)
    {
        if (!session.TryPopOperand(out var right) || !session.TryPopOperand(out var left))
        {
            return Fault(session, "KESR3101", "Operand stack underflow while executing equality opcode.");
        }

        var isEqual = Equals(left.ToObject(), right.ToObject());
        session.PushOperand(RuntimeValue.Bool(equals ? isEqual : !isEqual));
        session.AdvanceAfter(instruction);
        return KesVmExecutionResult.Success();
    }

    private static bool TryPopNumberOperands(KesVmSession session, out double left, out double right)
    {
        left = 0;
        right = 0;

        if (!session.TryPopOperand(out var rightValue) ||
            !session.TryPopOperand(out var leftValue) ||
            leftValue.Kind != RuntimeValueKind.Number ||
            rightValue.Kind != RuntimeValueKind.Number ||
            leftValue.NumberValue is null ||
            rightValue.NumberValue is null)
        {
            return false;
        }

        left = leftValue.NumberValue.Value;
        right = rightValue.NumberValue.Value;
        return true;
    }

    private static bool TryReadOperand(KlibInstruction instruction, int index, out int value)
    {
        value = 0;
        if (index < 0 || index >= instruction.Operands.Count)
        {
            return false;
        }

        value = instruction.Operands[index];
        return true;
    }

    private static IReadOnlyList<RuntimeValue>? PopArguments(KesVmSession session, int count)
    {
        var arguments = new RuntimeValue[count];
        for (var i = count - 1; i >= 0; i--)
        {
            if (!session.TryPopOperand(out var value))
            {
                return null;
            }

            arguments[i] = value;
        }

        return arguments;
    }

    private static bool TryPopIndexAndReference(
        KesVmSession session,
        string opcodeName,
        out string? referenceId,
        out int index,
        out KesVmExecutionResult? fault)
    {
        referenceId = null;
        index = 0;
        fault = null;

        if (!session.TryPopOperand(out var indexValue) ||
            indexValue.Kind != RuntimeValueKind.Number ||
            indexValue.NumberValue is null)
        {
            fault = Fault(session, "KESR3302", $"{opcodeName} requires a numeric index.");
            return false;
        }

        if (!TryPopReference(session, opcodeName, out referenceId, out fault))
        {
            return false;
        }

        index = (int)indexValue.NumberValue.Value;
        if (Math.Abs(indexValue.NumberValue.Value - index) > double.Epsilon)
        {
            fault = Fault(session, "KESR3302", $"{opcodeName} index must be an integer.");
            return false;
        }

        return true;
    }

    private static bool TryPopReference(
        KesVmSession session,
        string opcodeName,
        out string? referenceId,
        out KesVmExecutionResult? fault)
    {
        referenceId = null;
        fault = null;

        if (!session.TryPopOperand(out var referenceValue) ||
            referenceValue.Kind != RuntimeValueKind.Reference ||
            string.IsNullOrEmpty(referenceValue.ReferenceId))
        {
            fault = Fault(session, "KESR3300", $"{opcodeName} requires an object reference.");
            return false;
        }

        referenceId = referenceValue.ReferenceId;
        return true;
    }

    private static KesVmExecutionResult ExecuteCall(KesVmSession session, KlibInstruction instruction)
    {
        if (!TryReadOperand(instruction, 0, out var callIndex) ||
            !TryReadOperand(instruction, 1, out var argc) ||
            argc < 0)
        {
            return Fault(session, "KESR3310", "CALL requires target and argc operands.");
        }

        if (!TryResolveString(session.Document, callIndex, out var callName, out var callResolveError))
        {
            return Fault(session, "KESR3310", callResolveError ?? "CALL target could not be resolved.");
        }

        var arguments = PopArguments(session, argc);
        if (arguments is null)
        {
            return Fault(session, "KESR3101", "Not enough arguments on the stack for callable execution.");
        }

        var returnsValue = instruction.OpCode == KlibOpCode.Call;
        var result = InvokePureCall(session, callName!, arguments, returnsValue);
        if (!result.Succeeded)
        {
            return result;
        }

        session.AdvanceAfter(instruction);
        return KesVmExecutionResult.Success();
    }

    private static KesVmExecutionResult ExecuteMethodCall(KesVmSession session, KlibInstruction instruction)
    {
        if (!TryReadOperand(instruction, 0, out var methodIndex) ||
            !TryReadOperand(instruction, 1, out var argc) ||
            argc < 0)
        {
            return Fault(session, "KESR3311", "CALL_METHOD requires target and argc operands.");
        }

        if (!TryResolveString(session.Document, methodIndex, out var methodName, out var methodResolveError))
        {
            return Fault(session, "KESR3311", methodResolveError ?? "CALL_METHOD target could not be resolved.");
        }

        var arguments = PopArguments(session, argc);
        if (arguments is null)
        {
            return Fault(session, "KESR3101", "Not enough method arguments on the stack.");
        }

        if (!TryPopReference(session, "CALL_METHOD", out var receiverId, out var receiverFault))
        {
            return receiverFault!;
        }

        if (!StringComparer.Ordinal.Equals(methodName, "dispose"))
        {
            return Fault(session, "KESR3311", $"Method '{methodName}' is not supported.");
        }

        if (!session.ObjectStore.TryDispose(receiverId!, out var disposeError))
        {
            return Fault(session, "KESR3305", disposeError ?? "Method 'dispose' failed.");
        }

        if (instruction.OpCode == KlibOpCode.CallMethod)
        {
            session.PushOperand(RuntimeValue.Null);
        }

        session.AdvanceAfter(instruction);
        return KesVmExecutionResult.Success();
    }

    private static KesVmExecutionResult InvokePureCall(
        KesVmSession session,
        string callName,
        IReadOnlyList<RuntimeValue> arguments,
        bool returnsValue)
    {
        switch (callName)
        {
            case "number_to_string":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.Number || arguments[0].NumberValue is null)
                {
                    return Fault(session, "KESR3310", "Callable 'number_to_string' requires one number argument.");
                }

                if (returnsValue)
                {
                    var numberValue = arguments[0].NumberValue!.Value;
                    session.PushOperand(RuntimeValue.String(numberValue.ToString("G", System.Globalization.CultureInfo.InvariantCulture)));
                }

                return KesVmExecutionResult.Success();

            case "bool_to_string":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.Bool || arguments[0].BoolValue is null)
                {
                    return Fault(session, "KESR3310", "Callable 'bool_to_string' requires one bool argument.");
                }

                if (returnsValue)
                {
                    var boolValue = arguments[0].BoolValue!.Value;
                    session.PushOperand(RuntimeValue.String(boolValue ? "true" : "false"));
                }

                return KesVmExecutionResult.Success();

            case "array_len":
                if (arguments.Count != 1 ||
                    arguments[0].Kind != RuntimeValueKind.Reference ||
                    string.IsNullOrEmpty(arguments[0].ReferenceId))
                {
                    return Fault(session, "KESR3310", "Callable 'array_len' requires one array reference argument.");
                }

                var arrayReferenceId = arguments[0].ReferenceId!;
                if (!session.ObjectStore.TryGetArrayLength(arrayReferenceId, out var length, out var lengthError))
                {
                    return Fault(session, "KESR3310", lengthError ?? "Callable 'array_len' failed.");
                }

                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Number(length));
                }

                return KesVmExecutionResult.Success();

            case "str_len":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.String)
                {
                    return Fault(session, "KESR3310", "Callable 'str_len' requires one string argument.");
                }

                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Number(arguments[0].StringValue?.Length ?? 0));
                }

                return KesVmExecutionResult.Success();

            case "assert":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.Bool || arguments[0].BoolValue != true)
                {
                    return Fault(session, "KESR3310", "Callable 'assert' requires a true bool condition.");
                }

                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return KesVmExecutionResult.Success();

            default:
                return Fault(session, "KESR3310", $"Callable '{callName}' is not supported.");
        }
    }

    private static bool TryResolveString(KlibDocument document, int constantIndex, out string? value, out string? error)
    {
        value = null;
        error = null;

        if (constantIndex < 0 || constantIndex >= document.Constants.Count)
        {
            error = $"Constant index '{constantIndex}' is invalid.";
            return false;
        }

        var constant = document.Constants[constantIndex];
        if (constant.Kind == KlibConstantKind.String)
        {
            value = constant.StringValue ?? string.Empty;
            return true;
        }

        if (constant.ReferenceIndex is int referenceIndex &&
            referenceIndex >= 0 &&
            referenceIndex < document.Constants.Count &&
            document.Constants[referenceIndex].Kind == KlibConstantKind.String)
        {
            value = document.Constants[referenceIndex].StringValue ?? string.Empty;
            return true;
        }

        error = $"Constant index '{constantIndex}' does not resolve to a string.";
        return false;
    }

    private static int GetInstructionSize(KlibInstruction instruction)
    {
        if (instruction.OpCode == KlibOpCode.Select)
        {
            return 1 + sizeof(int) + ((instruction.SelectCases?.Count ?? 0) * 2 * sizeof(int));
        }

        return 1 + (instruction.Operands.Count * sizeof(int));
    }

    private static RuntimeValue ResolveConstant(KlibConstant constant)
    {
        return constant.Kind switch
        {
            KlibConstantKind.String => RuntimeValue.String(constant.StringValue ?? string.Empty),
            KlibConstantKind.Number => RuntimeValue.Number(constant.NumberValue ?? 0),
            KlibConstantKind.Bool => RuntimeValue.Bool(constant.BoolValue ?? false),
            KlibConstantKind.Null => RuntimeValue.Null,
            KlibConstantKind.ActorRef => RuntimeValue.Reference(constant.StringValue ?? string.Empty),
            KlibConstantKind.AssetRef => RuntimeValue.String(constant.StringValue ?? string.Empty),
            KlibConstantKind.LocaleKey => RuntimeValue.String(constant.StringValue ?? string.Empty),
            KlibConstantKind.ClassRef => RuntimeValue.String(constant.StringValue ?? string.Empty),
            KlibConstantKind.FieldRef => RuntimeValue.String(constant.StringValue ?? string.Empty),
            KlibConstantKind.MethodRef => RuntimeValue.String(constant.StringValue ?? string.Empty),
            _ => RuntimeValue.Null,
        };
    }

    private static KesVmExecutionResult Fault(KesVmSession session, string code, string message)
    {
        return KesVmExecutionResult.Failure(
            RuntimeFailureKind.Runtime,
            RuntimeDiagnostic.Error(
                code,
                message,
                RuntimeFailureKind.Runtime,
                new RuntimeSourceLocation(session.Document.Module.ScriptId, session.Position.InstructionIndex, null, null, null)));
    }
}

public sealed record KesVmExecutionResult(
    bool Succeeded,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public static KesVmExecutionResult Success()
    {
        return new KesVmExecutionResult(true, [], RuntimeFailureKind.None);
    }

    public static KesVmExecutionResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new KesVmExecutionResult(false, diagnostics, failureKind);
    }
}
