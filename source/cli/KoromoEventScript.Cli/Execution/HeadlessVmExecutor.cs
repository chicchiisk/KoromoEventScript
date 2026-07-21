using KoromoEventScript.Cli.Compilation;

namespace KoromoEventScript.Cli.Execution;

public sealed class HeadlessVmExecutor
{
    private readonly HeadlessVmCallableDispatcher callableDispatcher;

    public HeadlessVmExecutor()
        : this(new HeadlessVmCallableDispatcher())
    {
    }

    public HeadlessVmExecutor(HeadlessVmCallableDispatcher callableDispatcher)
    {
        this.callableDispatcher = callableDispatcher;
    }

    internal HeadlessVmExecutionResult RunToBoundary(
        KlibDocument document,
        HeadlessVmRuntimeState runtimeState,
        int startOffset,
        HeadlessVmObservationLog observation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(runtimeState);
        ArgumentNullException.ThrowIfNull(observation);

        if (document.Instructions.Count == 0)
        {
            return new HeadlessVmExecutionResult(
                HeadlessVmState.Completed(document.Module.ScriptId, startOffset),
                observation);
        }

        var instructionsByOffset = document.Instructions.ToDictionary(static instruction => instruction.Offset);
        var offset = startOffset;
        var currentObservation = observation;

        while (true)
        {
            if (!instructionsByOffset.TryGetValue(offset, out var instruction))
            {
                return Fault(document, offset, $"Unknown instruction offset '{offset}'.", currentObservation);
            }

            switch (instruction.OpCode)
            {
                case KlibOpCode.Label:
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.Jump:
                    offset = ResolveRelativeTarget(instruction, instruction.Operands[0]);
                    if (!instructionsByOffset.ContainsKey(offset))
                    {
                        return Fault(document, instruction.Offset, $"Jump target '{offset}' does not exist.", currentObservation);
                    }

                    break;

                case KlibOpCode.PushNull:
                    runtimeState.PushOperand(HeadlessVmRuntimeValue.Null());
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.PushConst:
                    runtimeState.PushOperand(ResolveConstant(document, instruction.Operands[0]));
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.PushTrue:
                    runtimeState.PushOperand(new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Bool, BoolValue: true));
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.PushFalse:
                    runtimeState.PushOperand(new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Bool, BoolValue: false));
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.PushInt:
                    runtimeState.PushOperand(new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Number, NumberValue: instruction.Operands[0]));
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.Pop:
                    if (!runtimeState.TryPopOperand(out _))
                    {
                        return Fault(document, instruction.Offset, "Operand stack underflow while executing POP.", currentObservation);
                    }

                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.Dup:
                    if (!runtimeState.TryPeekOperand(out var duplicated))
                    {
                        return Fault(document, instruction.Offset, "Operand stack underflow while executing DUP.", currentObservation);
                    }

                    runtimeState.PushOperand(duplicated!);
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.LoadVar:
                    if (!runtimeState.VariableValues.TryGetValue(instruction.Operands[0], out var loadedValue))
                    {
                        return Fault(document, instruction.Offset, $"Variable slot '{instruction.Operands[0]}' is not initialized.", currentObservation);
                    }

                    runtimeState.PushOperand(loadedValue ?? HeadlessVmRuntimeValue.Null());
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.StoreVar:
                case KlibOpCode.DefVar:
                    if (!runtimeState.TryPopOperand(out var storedValue))
                    {
                        return Fault(document, instruction.Offset, $"Operand stack underflow while executing '{instruction.OpCode}'.", currentObservation);
                    }

                    runtimeState.VariableValues[instruction.Operands[0]] = storedValue!;
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.AddVar:
                    if (!runtimeState.VariableValues.TryGetValue(instruction.Operands[0], out var addTarget) ||
                        !runtimeState.VariableValues.TryGetValue(instruction.Operands[1], out var addSource) ||
                        addTarget.Kind != HeadlessVmRuntimeValueKind.Number ||
                        addSource.Kind != HeadlessVmRuntimeValueKind.Number ||
                        addTarget.NumberValue is not double addTargetNumber ||
                        addSource.NumberValue is not double addSourceNumber)
                    {
                        return Fault(document, instruction.Offset, "ADD_VAR requires two initialized number variables.", currentObservation);
                    }

                    runtimeState.VariableValues[instruction.Operands[0]] = new HeadlessVmRuntimeValue(
                        HeadlessVmRuntimeValueKind.Number,
                        NumberValue: addTargetNumber + addSourceNumber);
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.IncrementVar:
                    if (!runtimeState.VariableValues.TryGetValue(instruction.Operands[0], out var incrementTarget) ||
                        incrementTarget.Kind != HeadlessVmRuntimeValueKind.Number ||
                        incrementTarget.NumberValue is not double incrementNumber)
                    {
                        return Fault(document, instruction.Offset, "INCREMENT_VAR requires an initialized number variable.", currentObservation);
                    }

                    runtimeState.VariableValues[instruction.Operands[0]] = new HeadlessVmRuntimeValue(
                        HeadlessVmRuntimeValueKind.Number,
                        NumberValue: incrementNumber + instruction.Operands[1]);
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.Add:
                    {
                        var addResult = ApplyNumericBinaryOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left + right);
                        if (addResult is not null)
                        {
                            return addResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Sub:
                    {
                        var subResult = ApplyNumericBinaryOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left - right);
                        if (subResult is not null)
                        {
                            return subResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Mul:
                    {
                        var mulResult = ApplyNumericBinaryOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left * right);
                        if (mulResult is not null)
                        {
                            return mulResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Div:
                    {
                        var divResult = ApplyNumericBinaryOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left / right);
                        if (divResult is not null)
                        {
                            return divResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Neg:
                    if (!runtimeState.TryPopOperand(out var negValue) || negValue?.Kind != HeadlessVmRuntimeValueKind.Number || negValue.NumberValue is null)
                    {
                        return Fault(document, instruction.Offset, "Unary NEG requires a number operand.", currentObservation);
                    }

                    runtimeState.PushOperand(new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Number, NumberValue: -negValue.NumberValue.Value));
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.Eq:
                    {
                        var eqResult = ApplyEqualityOperation(runtimeState, instruction.Offset, document, currentObservation, equals: true);
                        if (eqResult is not null)
                        {
                            return eqResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Neq:
                    {
                        var neqResult = ApplyEqualityOperation(runtimeState, instruction.Offset, document, currentObservation, equals: false);
                        if (neqResult is not null)
                        {
                            return neqResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Lt:
                    {
                        var ltResult = ApplyNumericComparisonOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left < right);
                        if (ltResult is not null)
                        {
                            return ltResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Le:
                    {
                        var leResult = ApplyNumericComparisonOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left <= right);
                        if (leResult is not null)
                        {
                            return leResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Gt:
                    {
                        var gtResult = ApplyNumericComparisonOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left > right);
                        if (gtResult is not null)
                        {
                            return gtResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Ge:
                    {
                        var geResult = ApplyNumericComparisonOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left >= right);
                        if (geResult is not null)
                        {
                            return geResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.And:
                    {
                        var andResult = ApplyBooleanBinaryOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left && right);
                        if (andResult is not null)
                        {
                            return andResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Or:
                    {
                        var orResult = ApplyBooleanBinaryOperation(runtimeState, instruction.Offset, document, currentObservation, static (left, right) => left || right);
                        if (orResult is not null)
                        {
                            return orResult;
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Not:
                    if (!runtimeState.TryPopOperand(out var notValue) || notValue?.Kind != HeadlessVmRuntimeValueKind.Bool || notValue.BoolValue is null)
                    {
                        return Fault(document, instruction.Offset, "Unary NOT requires a bool operand.", currentObservation);
                    }

                    runtimeState.PushOperand(new HeadlessVmRuntimeValue(HeadlessVmRuntimeValueKind.Bool, BoolValue: !notValue.BoolValue.Value));
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.ArrayNew:
                    {
                        var values = PopArguments(runtimeState, instruction.Offset, instruction.Operands[0], document);
                        if (values is null)
                        {
                            return Fault(document, instruction.Offset, "Not enough operands to build an array.", currentObservation);
                        }

                        runtimeState.PushOperand(runtimeState.ObjectStore.CreateArray(values));
                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.ArrayNewFilled:
                    {
                        if (!runtimeState.TryPopOperand(out var fillValue) ||
                            !runtimeState.TryPopOperand(out var lengthValue) ||
                            lengthValue?.Kind != HeadlessVmRuntimeValueKind.Number ||
                            lengthValue.NumberValue is not double lengthNumber ||
                            lengthNumber < 0 ||
                            lengthNumber > int.MaxValue ||
                            Math.Abs(lengthNumber - (int)lengthNumber) > double.Epsilon)
                        {
                            return Fault(document, instruction.Offset, "ARRAY_NEW_FILLED requires a non-negative integer length and a fill value.", currentObservation);
                        }

                        var values = new HeadlessVmRuntimeValue[(int)lengthNumber];
                        for (var index = 0; index < values.Length; index++)
                        {
                            values[index] = fillValue!;
                        }

                        runtimeState.PushOperand(runtimeState.ObjectStore.CreateArray(values));
                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.ArrayGet:
                case KlibOpCode.NumberArrayGet:
                    {
                        var arrayAccess = PopIndexAndReference(runtimeState, instruction.Offset, document, currentObservation, "ARRAY_GET");
                        if (arrayAccess.Fault is not null)
                        {
                            return arrayAccess.Fault;
                        }

                        if (!runtimeState.ObjectStore.TryGetArrayValue(arrayAccess.ReferenceId!, arrayAccess.Index, out var arrayValue, out var error))
                        {
                            return Fault(document, instruction.Offset, error ?? "ARRAY_GET failed.", currentObservation);
                        }

                        runtimeState.PushOperand(arrayValue);
                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.ArraySet:
                case KlibOpCode.NumberArraySet:
                    {
                        if (!runtimeState.TryPopOperand(out var assignedValue))
                        {
                            return Fault(document, instruction.Offset, "ARRAY_SET requires a value operand.", currentObservation);
                        }

                        var arrayAccess = PopIndexAndReference(runtimeState, instruction.Offset, document, currentObservation, "ARRAY_SET");
                        if (arrayAccess.Fault is not null)
                        {
                            return arrayAccess.Fault;
                        }

                        if (!runtimeState.ObjectStore.TrySetArrayValue(arrayAccess.ReferenceId!, arrayAccess.Index, assignedValue!, out var error))
                        {
                            return Fault(document, instruction.Offset, error ?? "ARRAY_SET failed.", currentObservation);
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.New:
                    {
                        var constructorArguments = PopArguments(runtimeState, instruction.Offset, instruction.Operands[1], document);
                        if (constructorArguments is null)
                        {
                            return Fault(document, instruction.Offset, "Not enough constructor arguments on the stack.", currentObservation);
                        }

                        var classId = ResolveString(document, instruction.Operands[0]);
                        runtimeState.PushOperand(runtimeState.ObjectStore.CreateInstance(classId));
                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.GetField:
                    {
                        if (!runtimeState.TryPopOperand(out var receiver) || receiver?.Kind != HeadlessVmRuntimeValueKind.Reference || string.IsNullOrEmpty(receiver.ReferenceId))
                        {
                            return Fault(document, instruction.Offset, "GET_FIELD requires an object reference receiver.", currentObservation);
                        }

                        var fieldId = ResolveString(document, instruction.Operands[0]);
                        if (receiver.ReferenceId.StartsWith("actor.", StringComparison.Ordinal))
                        {
                            var actorResult = callableDispatcher.InvokeActorPropertyGet(receiver.ReferenceId, fieldId, runtimeState.ObjectStore, currentObservation);
                            if (actorResult.Outcome == HeadlessVmCallableOutcomeKind.Fault)
                            {
                                return Fault(document, instruction.Offset, actorResult.FaultMessage ?? "GET_FIELD failed.", currentObservation);
                            }

                            currentObservation = actorResult.Observation;
                            if (actorResult.HasReturnValue && actorResult.ReturnValue is not null)
                            {
                                runtimeState.PushOperand(actorResult.ReturnValue);
                            }

                            offset = GetNextOffset(document, instruction);
                            break;
                        }

                        if (!runtimeState.ObjectStore.TryGetField(receiver.ReferenceId, fieldId, out var fieldValue, out var error))
                        {
                            return Fault(document, instruction.Offset, error ?? "GET_FIELD failed.", currentObservation);
                        }

                        runtimeState.PushOperand(fieldValue);
                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.SetField:
                    {
                        if (!runtimeState.TryPopOperand(out var fieldValue) ||
                            !runtimeState.TryPopOperand(out var receiver) ||
                            receiver?.Kind != HeadlessVmRuntimeValueKind.Reference ||
                            string.IsNullOrEmpty(receiver.ReferenceId))
                        {
                            return Fault(document, instruction.Offset, "SET_FIELD requires a receiver and value.", currentObservation);
                        }

                        var fieldId = ResolveString(document, instruction.Operands[0]);
                        if (!runtimeState.ObjectStore.TrySetField(receiver.ReferenceId, fieldId, fieldValue!, out var error))
                        {
                            return Fault(document, instruction.Offset, error ?? "SET_FIELD failed.", currentObservation);
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.CallMethod:
                case KlibOpCode.CallMethodVoid:
                    {
                        var methodArguments = PopArguments(runtimeState, instruction.Offset, instruction.Operands[1], document);
                        if (methodArguments is null)
                        {
                            return Fault(document, instruction.Offset, "Not enough method arguments on the stack.", currentObservation);
                        }

                        if (!runtimeState.TryPopOperand(out var receiver) || receiver?.Kind != HeadlessVmRuntimeValueKind.Reference || string.IsNullOrEmpty(receiver.ReferenceId))
                        {
                            return Fault(document, instruction.Offset, "Method call requires an object reference receiver.", currentObservation);
                        }

                        var methodName = ResolveString(document, instruction.Operands[0]);
                        var result = callableDispatcher.InvokeMethod(
                            methodName,
                            receiver.ReferenceId,
                            methodArguments,
                            instruction.OpCode == KlibOpCode.CallMethod,
                            runtimeState.ObjectStore,
                            currentObservation);
                        if (result.Outcome == HeadlessVmCallableOutcomeKind.Fault)
                        {
                            return Fault(document, instruction.Offset, result.FaultMessage ?? $"Method '{methodName}' failed.", currentObservation);
                        }

                        currentObservation = result.Observation;
                        if (result.HasReturnValue && result.ReturnValue is not null)
                        {
                            runtimeState.PushOperand(result.ReturnValue);
                        }

                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.Dispose:
                    if (!runtimeState.TryPopOperand(out var disposeReceiver) ||
                        disposeReceiver?.Kind != HeadlessVmRuntimeValueKind.Reference ||
                        string.IsNullOrEmpty(disposeReceiver.ReferenceId))
                    {
                        return Fault(document, instruction.Offset, "DISPOSE requires an object reference receiver.", currentObservation);
                    }

                    if (!runtimeState.ObjectStore.TryDispose(disposeReceiver.ReferenceId, out var disposeError))
                    {
                        return Fault(document, instruction.Offset, disposeError ?? "DISPOSE failed.", currentObservation);
                    }

                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.Call:
                case KlibOpCode.CallVoid:
                    {
                        var callableName = ResolveString(document, instruction.Operands[0]);
                        var arguments = PopArguments(runtimeState, instruction.Offset, instruction.Operands[1], document);
                        if (arguments is null)
                        {
                            return Fault(document, instruction.Offset, "Not enough arguments on the stack for callable execution.", currentObservation);
                        }

                        var result = callableDispatcher.InvokeCall(
                            callableName,
                            arguments,
                            instruction.OpCode == KlibOpCode.Call,
                            runtimeState.ObjectStore,
                            currentObservation);
                        if (result.Outcome == HeadlessVmCallableOutcomeKind.Fault)
                        {
                            return Fault(document, instruction.Offset, result.FaultMessage ?? $"Callable '{callableName}' failed.", currentObservation);
                        }

                        if (result.HasReturnValue && result.ReturnValue is not null)
                        {
                            runtimeState.PushOperand(result.ReturnValue);
                        }

                        currentObservation = result.Observation;
                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                case KlibOpCode.SysCall:
                case KlibOpCode.SysCallVoid:
                    {
                        var syscallName = ResolveString(document, instruction.Operands[0]);
                        var arguments = PopArguments(runtimeState, instruction.Offset, instruction.Operands[1], document);
                        if (arguments is null)
                        {
                            return Fault(document, instruction.Offset, "Not enough arguments on the stack for syscall execution.", currentObservation);
                        }

                        var nextOffset = GetNextOffset(document, instruction);
                        var result = callableDispatcher.InvokeSysCall(
                            syscallName,
                            arguments,
                            instruction.OpCode == KlibOpCode.SysCall,
                            currentObservation);
                        if (result.Outcome == HeadlessVmCallableOutcomeKind.Fault)
                        {
                            return Fault(document, instruction.Offset, result.FaultMessage ?? $"Syscall '{syscallName}' failed.", currentObservation);
                        }

                        currentObservation = result.Observation;
                        if (result.HasReturnValue && result.ReturnValue is not null)
                        {
                            runtimeState.PushOperand(result.ReturnValue);
                        }

                        if (result.Outcome == HeadlessVmCallableOutcomeKind.WaitForAdvance)
                        {
                            return new HeadlessVmExecutionResult(
                                HeadlessVmState.WaitingForAdvance(document.Module.ScriptId, nextOffset),
                                currentObservation);
                        }

                        offset = nextOffset;
                        break;
                    }

                case KlibOpCode.JumpFalse:
                    if (!runtimeState.TryPopOperand(out var conditionValue) ||
                        conditionValue?.Kind != HeadlessVmRuntimeValueKind.Bool ||
                        conditionValue.BoolValue is null)
                    {
                        return Fault(document, instruction.Offset, "JUMP_FALSE requires a bool operand.", currentObservation);
                    }

                    if (conditionValue.BoolValue.Value)
                    {
                        offset = GetNextOffset(document, instruction);
                        break;
                    }

                    offset = ResolveRelativeTarget(instruction, instruction.Operands[0]);
                    if (!instructionsByOffset.ContainsKey(offset))
                    {
                        return Fault(document, instruction.Offset, $"Jump target '{offset}' does not exist.", currentObservation);
                    }

                    break;

                case KlibOpCode.Select:
                    {
                        runtimeState.TryPopOperand(out var prompt);
                        var choices = (instruction.SelectCases ?? [])
                            .Select(@case => new HeadlessVmChoice(
                                ResolveString(document, @case.TextIndex),
                                ResolveRelativeTarget(instruction, @case.Offset)))
                            .ToArray();
                        currentObservation = currentObservation.ShowChoices(ValueToDisplayString(prompt), choices);
                        return new HeadlessVmExecutionResult(
                            HeadlessVmState.WaitingForSelection(document.Module.ScriptId, instruction.Offset, choices),
                            currentObservation);
                    }

                case KlibOpCode.End:
                    return new HeadlessVmExecutionResult(
                        HeadlessVmState.Completed(document.Module.ScriptId, instruction.Offset),
                        currentObservation.ClearChoices());

                default:
                    return Fault(document, instruction.Offset, $"Unsupported opcode '{instruction.OpCode}'.", currentObservation);
            }
        }
    }

    private static HeadlessVmExecutionResult Fault(KlibDocument document, int instructionOffset, string message, HeadlessVmObservationLog observation)
    {
        var mapping = document.Debug.SourceMappings.FirstOrDefault(mapping => mapping.BytecodeOffset == instructionOffset);
        return new HeadlessVmExecutionResult(
            HeadlessVmState.Faulted(new HeadlessVmFault(
                message,
                document.Module.ScriptId,
                instructionOffset,
                mapping?.Line,
                mapping?.Column)),
            observation.ClearChoices());
    }

    private static HeadlessVmRuntimeValue[]? PopArguments(HeadlessVmRuntimeState runtimeState, int instructionOffset, int argumentCount, KlibDocument document)
    {
        if (runtimeState.OperandCount < argumentCount)
        {
            return null;
        }

        var arguments = new HeadlessVmRuntimeValue[argumentCount];
        for (var index = argumentCount - 1; index >= 0; index--)
        {
            runtimeState.TryPopOperand(out var value);
            arguments[index] = value!;
        }

        return arguments;
    }

    private static HeadlessVmRuntimeValue ResolveConstant(KlibDocument document, int constantIndex)
    {
        var constant = document.Constants[constantIndex];
        return constant.Kind switch
        {
            KlibConstantKind.String => HeadlessVmRuntimeValue.FromObject(constant.StringValue),
            KlibConstantKind.Number => HeadlessVmRuntimeValue.FromObject(constant.NumberValue),
            KlibConstantKind.Bool => HeadlessVmRuntimeValue.FromObject(constant.BoolValue),
            KlibConstantKind.Null => HeadlessVmRuntimeValue.Null(),
            _ => ResolveReferenceValue(document, constant),
        };
    }

    private static string ResolveString(KlibDocument document, int constantIndex)
    {
        return ValueToDisplayString(ResolveConstant(document, constantIndex)) ?? string.Empty;
    }

    private static HeadlessVmRuntimeValue ResolveReferenceValue(KlibDocument document, KlibConstant constant)
    {
        if (!constant.ReferenceIndex.HasValue)
        {
            return HeadlessVmRuntimeValue.Null();
        }

        return new HeadlessVmRuntimeValue(
            HeadlessVmRuntimeValueKind.Reference,
            ReferenceId: document.Constants[constant.ReferenceIndex.Value].StringValue);
    }

    private static string? ValueToDisplayString(HeadlessVmRuntimeValue? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Kind switch
        {
            HeadlessVmRuntimeValueKind.Null => null,
            HeadlessVmRuntimeValueKind.String => value.StringValue,
            HeadlessVmRuntimeValueKind.Bool => value.BoolValue is true ? "true" : "false",
            HeadlessVmRuntimeValueKind.Number => value.NumberValue?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            HeadlessVmRuntimeValueKind.Reference => value.ReferenceId,
            _ => value.ToString(),
        };
    }

    private static int GetNextOffset(KlibDocument document, KlibInstruction instruction)
    {
        var nextIndex = instruction.Index + 1;
        return nextIndex >= document.Instructions.Count
            ? instruction.Offset
            : document.Instructions[nextIndex].Offset;
    }

    private static int ResolveRelativeTarget(KlibInstruction instruction, int relativeOffset)
    {
        return instruction.Offset + GetInstructionSize(instruction) + relativeOffset;
    }

    private static int GetInstructionSize(KlibInstruction instruction)
    {
        if (instruction.OpCode == KlibOpCode.Select)
        {
            return 1 + 4 + ((instruction.SelectCases?.Count ?? 0) * 8);
        }

        return 1 + (instruction.Operands.Count * 4);
    }

    private static (string? ReferenceId, int Index, HeadlessVmExecutionResult? Fault) PopIndexAndReference(
        HeadlessVmRuntimeState runtimeState,
        int instructionOffset,
        KlibDocument document,
        HeadlessVmObservationLog observation,
        string opcodeName)
    {
        if (!runtimeState.TryPopOperand(out var indexValue) ||
            indexValue?.Kind != HeadlessVmRuntimeValueKind.Number ||
            indexValue.NumberValue is null)
        {
            return (null, 0, Fault(document, instructionOffset, $"{opcodeName} requires a number index.", observation));
        }

        if (!runtimeState.TryPopOperand(out var referenceValue) ||
            referenceValue?.Kind != HeadlessVmRuntimeValueKind.Reference ||
            string.IsNullOrEmpty(referenceValue.ReferenceId))
        {
            return (null, 0, Fault(document, instructionOffset, $"{opcodeName} requires an array reference.", observation));
        }

        return (referenceValue.ReferenceId, (int)indexValue.NumberValue.Value, null);
    }

    private static HeadlessVmExecutionResult? ApplyNumericBinaryOperation(
        HeadlessVmRuntimeState runtimeState,
        int instructionOffset,
        KlibDocument document,
        HeadlessVmObservationLog observation,
        Func<double, double, double> operation)
    {
        if (!TryPopBinaryOperands(runtimeState, instructionOffset, document, observation, out var left, out var right, out var fault))
        {
            return fault;
        }

        if (left!.Kind != HeadlessVmRuntimeValueKind.Number || right!.Kind != HeadlessVmRuntimeValueKind.Number ||
            left.NumberValue is null || right.NumberValue is null)
        {
            return Fault(document, instructionOffset, "Numeric opcode requires number operands.", observation);
        }

        runtimeState.PushOperand(new HeadlessVmRuntimeValue(
            HeadlessVmRuntimeValueKind.Number,
            NumberValue: operation(left.NumberValue.Value, right.NumberValue.Value)));
        return null;
    }

    private static HeadlessVmExecutionResult? ApplyNumericComparisonOperation(
        HeadlessVmRuntimeState runtimeState,
        int instructionOffset,
        KlibDocument document,
        HeadlessVmObservationLog observation,
        Func<double, double, bool> operation)
    {
        if (!TryPopBinaryOperands(runtimeState, instructionOffset, document, observation, out var left, out var right, out var fault))
        {
            return fault;
        }

        if (left!.Kind != HeadlessVmRuntimeValueKind.Number || right!.Kind != HeadlessVmRuntimeValueKind.Number ||
            left.NumberValue is null || right.NumberValue is null)
        {
            return Fault(document, instructionOffset, "Comparison opcode requires number operands.", observation);
        }

        runtimeState.PushOperand(new HeadlessVmRuntimeValue(
            HeadlessVmRuntimeValueKind.Bool,
            BoolValue: operation(left.NumberValue.Value, right.NumberValue.Value)));
        return null;
    }

    private static HeadlessVmExecutionResult? ApplyBooleanBinaryOperation(
        HeadlessVmRuntimeState runtimeState,
        int instructionOffset,
        KlibDocument document,
        HeadlessVmObservationLog observation,
        Func<bool, bool, bool> operation)
    {
        if (!TryPopBinaryOperands(runtimeState, instructionOffset, document, observation, out var left, out var right, out var fault))
        {
            return fault;
        }

        if (left!.Kind != HeadlessVmRuntimeValueKind.Bool || right!.Kind != HeadlessVmRuntimeValueKind.Bool ||
            left.BoolValue is null || right.BoolValue is null)
        {
            return Fault(document, instructionOffset, "Logical opcode requires bool operands.", observation);
        }

        runtimeState.PushOperand(new HeadlessVmRuntimeValue(
            HeadlessVmRuntimeValueKind.Bool,
            BoolValue: operation(left.BoolValue.Value, right.BoolValue.Value)));
        return null;
    }

    private static HeadlessVmExecutionResult? ApplyEqualityOperation(
        HeadlessVmRuntimeState runtimeState,
        int instructionOffset,
        KlibDocument document,
        HeadlessVmObservationLog observation,
        bool equals)
    {
        if (!TryPopBinaryOperands(runtimeState, instructionOffset, document, observation, out var left, out var right, out var fault))
        {
            return fault;
        }

        var isEqual = Equals(left!.ToObject(), right!.ToObject());
        runtimeState.PushOperand(new HeadlessVmRuntimeValue(
            HeadlessVmRuntimeValueKind.Bool,
            BoolValue: equals ? isEqual : !isEqual));
        return null;
    }

    private static bool TryPopBinaryOperands(
        HeadlessVmRuntimeState runtimeState,
        int instructionOffset,
        KlibDocument document,
        HeadlessVmObservationLog observation,
        out HeadlessVmRuntimeValue? left,
        out HeadlessVmRuntimeValue? right,
        out HeadlessVmExecutionResult? fault)
    {
        left = null;
        right = null;
        fault = null;
        if (!runtimeState.TryPopOperand(out right) || !runtimeState.TryPopOperand(out left))
        {
            fault = Fault(document, instructionOffset, "Operand stack underflow while executing binary opcode.", observation);
            return false;
        }

        return true;
    }
}

public sealed record HeadlessVmExecutionResult(
    HeadlessVmState State,
    HeadlessVmObservationLog Observation);
