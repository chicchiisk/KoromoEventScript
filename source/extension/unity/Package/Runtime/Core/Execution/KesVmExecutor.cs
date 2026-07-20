#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Stl;

namespace KoromoEventScript.Runtime.Core.Execution
{

public sealed class KesVmExecutor
{
    private const int DefaultMaxInstructionCount = 10_000;
    private readonly IRuntimeSyscallDispatcher syscallDispatcher;
    private readonly IRuntimeEffectSink? effectSink;
    private readonly IRuntimeGameParameterStore gameParameters;
    private readonly bool waitForHostEffects;
    private readonly Action<KlibDocument, KlibInstruction>? instructionExecuting;
    private readonly KesVmPerformanceCounters? performanceCounters;

    public static ISet<KlibOpCode> DispatchedOpCodes { get; } = new HashSet<KlibOpCode>
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

    public KesVmExecutor(
        IRuntimeSyscallDispatcher? syscallDispatcher = null,
        IRuntimeEffectSink? effectSink = null,
        IRuntimeGameParameterStore? gameParameters = null,
        bool waitForHostEffects = false,
        Action<KlibDocument, KlibInstruction>? instructionExecuting = null,
        KesVmPerformanceCounters? performanceCounters = null)
    {
        this.effectSink = effectSink;
        this.gameParameters = gameParameters ?? new RuntimeGameParameterStore();
        this.waitForHostEffects = waitForHostEffects;
        this.instructionExecuting = instructionExecuting;
        this.performanceCounters = performanceCounters;
        this.syscallDispatcher = syscallDispatcher ??
            new StlSyscallDispatcher(effectSink, this.gameParameters, waitForHostEffects);
    }

    public KesVmExecutionResult Run(KesVmSession session, int maxInstructionCount = DefaultMaxInstructionCount)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        performanceCounters?.BeginRun();
        var executed = 0;
        while (session.Continuation.Kind == RuntimeContinuationKind.Running)
        {
            if (executed++ >= maxInstructionCount)
            {
                return CompleteMeasuredRun(
                    Fault(session, "KESR3198", "VM execution exceeded the maximum instruction count."));
            }

            var instruction = session.CurrentInstruction();
            if (instruction is null)
            {
                return CompleteMeasuredRun(
                    Fault(session, "KESR3100", $"Instruction index '{session.Position.InstructionIndex}' does not exist."));
            }

            performanceCounters?.RecordInstruction(instruction.OpCode, session.OperandStack.Count);
            instructionExecuting?.Invoke(session.Document, instruction);
            var result = Execute(session, instruction);
            if (!result.Succeeded)
            {
                return CompleteMeasuredRun(result);
            }
        }

        return CompleteMeasuredRun(KesVmExecutionResult.Success());
    }

    private KesVmExecutionResult CompleteMeasuredRun(KesVmExecutionResult result)
    {
        performanceCounters?.CompleteRun(result.Succeeded);
        return result;
    }

    public KesVmExecutionResult ChooseSelection(KesVmSession session, int choiceIndex)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

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

    public KesVmExecutionResult ContinueAdvance(KesVmSession session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (session.Continuation.Kind != RuntimeContinuationKind.WaitingForAdvance)
        {
            return Fault(session, "KESR3207", "Session is not waiting for advance input.");
        }

        if (session.Continuation.ResumeInstructionIndex is not int resumeInstructionIndex)
        {
            session.SetContinuation(RuntimeContinuation.Completed);
            return KesVmExecutionResult.Success();
        }

        session.SetInstructionIndex(resumeInstructionIndex);
        session.SetContinuation(RuntimeContinuation.Running);
        return KesVmExecutionResult.Success();
    }

    private KesVmExecutionResult Execute(KesVmSession session, KlibInstruction instruction)
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
                return ExecuteSyscall(session, instruction);

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

    private void PublishSceneEffect(string name, IReadOnlyDictionary<string, string?> payload)
    {
        effectSink?.Publish(new RuntimeEffectBatch(
            new[] { new RuntimeEffect(RuntimeEffectKind.Scene, name, payload) },
            Array.Empty<RuntimeDiagnostic>()));
    }

    private void PublishAudioEffect(string name, IReadOnlyDictionary<string, string?> payload)
    {
        effectSink?.Publish(new RuntimeEffectBatch(
            new[] { new RuntimeEffect(RuntimeEffectKind.Audio, name, payload) },
            Array.Empty<RuntimeDiagnostic>()));
    }

    private KesVmExecutionResult WaitForPublishedHostEffect(
        KesVmSession session,
        KlibInstruction instruction,
        string operationName)
    {
        if (!waitForHostEffects)
        {
            return KesVmExecutionResult.Success();
        }

        session.SetContinuation(new RuntimeContinuation(
            RuntimeContinuationKind.WaitingForHost,
            FindNextInstructionIndex(session, instruction),
            Array.Empty<int>(),
            operationName,
            Array.Empty<RuntimeSelectionChoice>()));
        return KesVmExecutionResult.Success();
    }

    private static string ReadActorStringFieldOrDefault(KesVmSession session, string actorReference, string fieldName, string fallback)
    {
        return session.ObjectStore.TryGetField(actorReference, fieldName, out var value, out _) &&
            value.Kind == RuntimeValueKind.String &&
            !string.IsNullOrWhiteSpace(value.StringValue)
            ? value.StringValue!
            : fallback;
    }

    private static string NormalizeActorReference(string actorReference)
    {
        var dotIndex = actorReference.LastIndexOf('.');
        return dotIndex >= 0 && dotIndex + 1 < actorReference.Length
            ? actorReference[(dotIndex + 1)..]
            : actorReference;
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

    private KesVmExecutionResult ExecuteCall(KesVmSession session, KlibInstruction instruction)
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
        var result = InvokePureCall(session, instruction, callName!, arguments, returnsValue);
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

    private KesVmExecutionResult ExecuteSyscall(KesVmSession session, KlibInstruction instruction)
    {
        if (!TryReadOperand(instruction, 0, out var syscallIndex) ||
            !TryReadOperand(instruction, 1, out var argc) ||
            argc < 0)
        {
            return Fault(session, "KESR3400", "SYSCALL requires target and argc operands.");
        }

        if (!TryResolveString(session.Document, syscallIndex, out var syscallId, out var syscallResolveError))
        {
            return Fault(session, "KESR3400", syscallResolveError ?? "SYSCALL target could not be resolved.");
        }

        var arguments = PopArguments(session, argc);
        if (arguments is null)
        {
            return Fault(session, "KESR3101", "Not enough arguments on the stack for syscall execution.");
        }

        var returnsValue = instruction.OpCode == KlibOpCode.SysCall;
        var result = syscallDispatcher.Invoke(
            new RuntimeSyscallInvocation(
                syscallId!,
                arguments,
                returnsValue,
                new RuntimeSourceLocation(session.Document.Module.ScriptId, session.Position.InstructionIndex, null, null, null)),
            session);
        if (!result.Succeeded)
        {
            return KesVmExecutionResult.Failure(result.FailureKind, result.Diagnostics.ToArray());
        }

        if (returnsValue)
        {
            if (result.ReturnValue is null)
            {
                return Fault(session, "KESR3404", $"Syscall '{syscallId}' did not return a value.");
            }

            session.PushOperand(result.ReturnValue.Value);
        }

        var resumeInstructionIndex = FindNextInstructionIndex(session, instruction);
        session.AdvanceAfter(instruction);
        if (result.WaitForAdvance)
        {
            session.SetContinuation(new RuntimeContinuation(
                RuntimeContinuationKind.WaitingForAdvance,
                resumeInstructionIndex,
                Array.Empty<int>(),
                null,
                Array.Empty<RuntimeSelectionChoice>()));
        }
        else if (result.WaitForHost)
        {
            session.SetContinuation(new RuntimeContinuation(
                RuntimeContinuationKind.WaitingForHost,
                resumeInstructionIndex,
                Array.Empty<int>(),
                syscallId,
                Array.Empty<RuntimeSelectionChoice>()));
        }

        return KesVmExecutionResult.Success();
    }

    private KesVmExecutionResult InvokeMappedSyscall(
        KesVmSession session,
        KlibInstruction instruction,
        string syscallId,
        IReadOnlyList<RuntimeValue> arguments,
        bool returnsValue)
    {
        var result = syscallDispatcher.Invoke(
            new RuntimeSyscallInvocation(
                syscallId,
                arguments,
                returnsValue,
                new RuntimeSourceLocation(session.Document.Module.ScriptId, session.Position.InstructionIndex, null, null, null)),
            session);
        if (!result.Succeeded)
        {
            return KesVmExecutionResult.Failure(result.FailureKind, result.Diagnostics.ToArray());
        }

        if (returnsValue)
        {
            if (result.ReturnValue is null)
            {
                return Fault(session, "KESR3404", $"Syscall '{syscallId}' did not return a value.");
            }

            session.PushOperand(result.ReturnValue.Value);
        }

        if (result.WaitForAdvance)
        {
            session.SetContinuation(new RuntimeContinuation(
                RuntimeContinuationKind.WaitingForAdvance,
                FindNextInstructionIndex(session, instruction),
                Array.Empty<int>(),
                null,
                Array.Empty<RuntimeSelectionChoice>()));
        }
        else if (result.WaitForHost)
        {
            session.SetContinuation(new RuntimeContinuation(
                RuntimeContinuationKind.WaitingForHost,
                FindNextInstructionIndex(session, instruction),
                Array.Empty<int>(),
                syscallId,
                Array.Empty<RuntimeSelectionChoice>()));
        }

        return KesVmExecutionResult.Success();
    }

    private KesVmExecutionResult InvokePureCall(
        KesVmSession session,
        KlibInstruction instruction,
        string callName,
        IReadOnlyList<RuntimeValue> arguments,
        bool returnsValue)
    {
        switch (callName)
        {
            case "print":
                return InvokeMappedSyscall(session, instruction, "core.print", arguments, returnsValue);

            case "range":
                return InvokeMappedSyscall(session, instruction, "core.range", arguments, returnsValue);

            case "rt_back":
                return InvokeMappedSyscall(session, instruction, "scene.rt_back", arguments, returnsValue);

            case "rt_front":
                return InvokeMappedSyscall(session, instruction, "scene.rt_front", arguments, returnsValue);

            case "camera_autofocus":
                return InvokeMappedSyscall(session, instruction, "scene.camera_autofocus", arguments, returnsValue);

            case "p":
                return InvokeMappedSyscall(session, instruction, "text.p", arguments, returnsValue);

            case "r":
                return InvokeMappedSyscall(session, instruction, "text.r", arguments, returnsValue);

            case "l":
                return InvokeMappedSyscall(session, instruction, "text.l", arguments, returnsValue);

            case "cm":
                return InvokeMappedSyscall(session, instruction, "text.cm", arguments, returnsValue);

            case "wait_click":
                return InvokeMappedSyscall(session, instruction, "text.wait_click", arguments, returnsValue);

            case "save":
                if (arguments.Count == 1)
                {
                    return InvokeMappedSyscall(session, instruction, "state.save", new[] { arguments[0], RuntimeValue.String(string.Empty) }, returnsValue);
                }

                return InvokeMappedSyscall(session, instruction, "state.save", arguments, returnsValue);

            case "load":
                return InvokeMappedSyscall(session, instruction, "state.load", arguments, returnsValue);

            case "autosave":
                return InvokeMappedSyscall(session, instruction, "state.autosave", arguments, returnsValue);

            case "mark_read":
                return InvokeMappedSyscall(session, instruction, "state.mark_read", arguments, returnsValue);

            case "is_read":
                return InvokeMappedSyscall(session, instruction, "state.is_read", arguments, returnsValue);

            case "wait":
                return InvokeMappedSyscall(session, instruction, "system.wait", arguments, returnsValue);

            case "set_auto":
                return InvokeMappedSyscall(session, instruction, "system.set_auto", arguments, returnsValue);

            case "set_skip":
                return InvokeMappedSyscall(session, instruction, "system.set_skip", arguments, returnsValue);

            case "set_config_string":
                return InvokeMappedSyscall(session, instruction, "system.set_config_string", arguments, returnsValue);

            case "set_config_number":
                return InvokeMappedSyscall(session, instruction, "system.set_config_number", arguments, returnsValue);

            case "set_config_bool":
                return InvokeMappedSyscall(session, instruction, "system.set_config_bool", arguments, returnsValue);

            case "get_config":
                return InvokeMappedSyscall(session, instruction, "system.get_config", arguments, returnsValue);

            case "vo":
                return arguments.Count == 0
                    ? InvokeMappedSyscall(session, instruction, "audio.vo_auto", arguments, returnsValue)
                    : InvokeMappedSyscall(session, instruction, "text.vo", arguments, returnsValue);

            case "vf":
                if (arguments.Count != 2 || arguments[0].Kind != RuntimeValueKind.Reference || string.IsNullOrEmpty(arguments[0].ReferenceId) || arguments[1].Kind != RuntimeValueKind.String)
                {
                    return Fault(session, "KESR3310", "Callable 'vf' requires actor and expression arguments.");
                }

                session.ObjectStore.EnsureActorReference(arguments[0].ReferenceId!);
                var vfActor = arguments[0].ReferenceId!;
                PublishSceneEffect(
                    "actor.face",
                    new Dictionary<string, string?>
                    {
                        ["actor"] = vfActor,
                        ["assetBaseName"] = ReadActorStringFieldOrDefault(session, vfActor, "assetBaseName", NormalizeActorReference(vfActor)),
                        ["exp"] = arguments[1].StringValue,
                    });
                return InvokeMappedSyscall(session, instruction, "audio.vo_auto", Array.Empty<RuntimeValue>(), returnsValue);

            case "standby":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.Reference || string.IsNullOrEmpty(arguments[0].ReferenceId))
                {
                    return Fault(session, "KESR3310", $"Callable '{callName}' requires one actor reference argument.");
                }

                session.ObjectStore.EnsureActorReference(arguments[0].ReferenceId!);
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return KesVmExecutionResult.Success();

            case "action_jump":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.Reference || string.IsNullOrEmpty(arguments[0].ReferenceId))
                {
                    return Fault(session, "KESR3310", "Callable 'action_jump' requires one actor reference argument.");
                }

                session.ObjectStore.EnsureActorReference(arguments[0].ReferenceId!);
                PublishSceneEffect(
                    "actor.action_jump",
                    new Dictionary<string, string?>
                    {
                        ["actor"] = arguments[0].ReferenceId,
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "actor.action_jump");

            case "bg":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.String)
                {
                    return Fault(session, "KESR3310", "Callable 'bg' requires one background id argument.");
                }

                PublishSceneEffect(
                    "scene.bg",
                    new Dictionary<string, string?>
                    {
                        ["id"] = arguments[0].StringValue,
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "scene.bg");

            case "show":
                if (arguments.Count is < 1 or > 6 || arguments[0].Kind != RuntimeValueKind.Reference || string.IsNullOrEmpty(arguments[0].ReferenceId))
                {
                    return Fault(session, "KESR3310", "Callable 'show' requires actor and optional display arguments.");
                }

                session.ObjectStore.EnsureActorReference(arguments[0].ReferenceId!);
                if (arguments.Count >= 2 && arguments[1].Kind != RuntimeValueKind.Number)
                {
                    return Fault(session, "KESR3310", "Callable 'show' position argument must be a number.");
                }

                if (arguments.Count >= 3 && arguments[2].Kind != RuntimeValueKind.String)
                {
                    return Fault(session, "KESR3310", "Callable 'show' face argument must be a string.");
                }

                if (arguments.Count >= 4 && arguments[3].Kind != RuntimeValueKind.Number)
                {
                    return Fault(session, "KESR3310", "Callable 'show' layer argument must be a number.");
                }

                if (arguments.Count >= 5 && arguments[4].Kind != RuntimeValueKind.Number)
                {
                    return Fault(session, "KESR3310", "Callable 'show' z argument must be a number.");
                }

                if (arguments.Count >= 6 && arguments[5].Kind != RuntimeValueKind.Bool)
                {
                    return Fault(session, "KESR3310", "Callable 'show' bustup argument must be a bool.");
                }

                var showActor = arguments[0].ReferenceId!;
                var showFace = arguments.Count >= 3
                    ? arguments[2].StringValue ?? string.Empty
                    : ReadActorStringFieldOrDefault(session, showActor, "defaultFace", "normal");
                PublishSceneEffect(
                    "actor.show",
                    new Dictionary<string, string?>
                    {
                        ["actor"] = showActor,
                        ["assetBaseName"] = ReadActorStringFieldOrDefault(session, showActor, "assetBaseName", NormalizeActorReference(showActor)),
                        ["face"] = showFace,
                        ["pos"] = arguments.Count >= 2 ? arguments[1].NumberValue?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : "0",
                        ["layer"] = arguments.Count >= 4 ? arguments[3].NumberValue?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : "0",
                        ["z"] = arguments.Count >= 5 ? arguments[4].NumberValue?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : "0",
                        ["bustup"] = arguments.Count >= 6 ? arguments[5].BoolValue.GetValueOrDefault().ToString().ToLowerInvariant() : "false",
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "actor.show");

            case "hide":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.Reference || string.IsNullOrEmpty(arguments[0].ReferenceId))
                {
                    return Fault(session, "KESR3310", "Callable 'hide' requires one actor reference argument.");
                }

                session.ObjectStore.EnsureActorReference(arguments[0].ReferenceId!);
                PublishSceneEffect(
                    "actor.hide",
                    new Dictionary<string, string?>
                    {
                        ["actor"] = arguments[0].ReferenceId,
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "actor.hide");

            case "face":
                if (arguments.Count != 2 || arguments[0].Kind != RuntimeValueKind.Reference || string.IsNullOrEmpty(arguments[0].ReferenceId) || arguments[1].Kind != RuntimeValueKind.String)
                {
                    return Fault(session, "KESR3310", "Callable 'face' requires actor and expression arguments.");
                }

                session.ObjectStore.EnsureActorReference(arguments[0].ReferenceId!);
                var faceActor = arguments[0].ReferenceId!;
                PublishSceneEffect(
                    "actor.face",
                    new Dictionary<string, string?>
                    {
                        ["actor"] = faceActor,
                        ["assetBaseName"] = ReadActorStringFieldOrDefault(session, faceActor, "assetBaseName", NormalizeActorReference(faceActor)),
                        ["exp"] = arguments[1].StringValue,
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "actor.face");

            case "move":
                if (arguments.Count is < 2 or > 3 || arguments[0].Kind != RuntimeValueKind.Reference || string.IsNullOrEmpty(arguments[0].ReferenceId) || arguments[1].Kind != RuntimeValueKind.Number)
                {
                    return Fault(session, "KESR3310", "Callable 'move' requires actor, position, and optional duration arguments.");
                }

                if (arguments.Count == 3 && arguments[2].Kind != RuntimeValueKind.Number)
                {
                    return Fault(session, "KESR3310", "Callable 'move' duration argument must be a number.");
                }

                session.ObjectStore.EnsureActorReference(arguments[0].ReferenceId!);
                PublishSceneEffect(
                    "actor.move",
                    new Dictionary<string, string?>
                    {
                        ["actor"] = arguments[0].ReferenceId,
                        ["pos"] = arguments[1].NumberValue?.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                        ["duration"] = arguments.Count == 3 ? arguments[2].NumberValue?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : "0",
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "actor.move");

            case "trans":
                if (arguments.Count > 2 || (arguments.Count >= 1 && arguments[0].Kind != RuntimeValueKind.String))
                {
                    return Fault(session, "KESR3310", "Callable 'trans' requires optional transition id and duration arguments.");
                }

                if (arguments.Count == 2 && arguments[1].Kind != RuntimeValueKind.Number)
                {
                    return Fault(session, "KESR3310", "Callable 'trans' duration argument must be a number.");
                }

                PublishSceneEffect(
                    "scene.trans",
                    new Dictionary<string, string?>
                    {
                        ["effect"] = arguments.Count >= 1 ? arguments[0].StringValue : "crossfade",
                        ["duration"] = arguments.Count == 2 ? arguments[1].NumberValue?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : "0",
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "scene.trans");

            case "bgm":
                if (arguments.Count is < 1 or > 3 || arguments[0].Kind != RuntimeValueKind.String)
                {
                    return Fault(session, "KESR3310", "Callable 'bgm' requires id, optional loop, and optional fade arguments.");
                }

                if (arguments.Count >= 2 && arguments[1].Kind != RuntimeValueKind.Bool)
                {
                    return Fault(session, "KESR3310", "Callable 'bgm' loop argument must be a bool.");
                }

                if (arguments.Count >= 3 && arguments[2].Kind != RuntimeValueKind.Number)
                {
                    return Fault(session, "KESR3310", "Callable 'bgm' fade argument must be a number.");
                }

                var bgmFade = arguments.Count >= 3 ? arguments[2].NumberValue.GetValueOrDefault() : 0d;
                if (bgmFade < 0 || double.IsNaN(bgmFade) || double.IsInfinity(bgmFade))
                {
                    return Fault(session, "KESR3310", "Callable 'bgm' fade must be finite and non-negative.");
                }

                PublishAudioEffect(
                    "audio.bgm",
                    new Dictionary<string, string?>
                    {
                        ["id"] = arguments[0].StringValue,
                        ["loop"] = (arguments.Count >= 2 ? arguments[1].BoolValue.GetValueOrDefault() : true).ToString().ToLowerInvariant(),
                        ["fade"] = bgmFade.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "audio.bgm");

            case "bgm_stop":
                if (arguments.Count > 1 || (arguments.Count == 1 && arguments[0].Kind != RuntimeValueKind.Number))
                {
                    return Fault(session, "KESR3310", "Callable 'bgm_stop' requires an optional fade argument.");
                }

                var bgmStopFade = arguments.Count == 1 ? arguments[0].NumberValue.GetValueOrDefault() : 0d;
                if (bgmStopFade < 0 || double.IsNaN(bgmStopFade) || double.IsInfinity(bgmStopFade))
                {
                    return Fault(session, "KESR3310", "Callable 'bgm_stop' fade must be finite and non-negative.");
                }

                PublishAudioEffect(
                    "audio.bgm_stop",
                    new Dictionary<string, string?>
                    {
                        ["fade"] = bgmStopFade.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "audio.bgm_stop");

            case "se":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.String)
                {
                    return Fault(session, "KESR3310", "Callable 'se' requires one sound effect id argument.");
                }

                PublishAudioEffect(
                    "audio.se",
                    new Dictionary<string, string?>
                    {
                        ["id"] = arguments[0].StringValue,
                    });
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "audio.se");

            case "se_stop":
                if (arguments.Count > 1 || (arguments.Count == 1 && arguments[0].Kind is not RuntimeValueKind.String and not RuntimeValueKind.Null))
                {
                    return Fault(session, "KESR3310", "Callable 'se_stop' requires an optional sound effect id argument.");
                }

                if (arguments.Count == 0 || arguments[0].Kind == RuntimeValueKind.Null)
                {
                    PublishAudioEffect("audio.se_stop_all", new Dictionary<string, string?>());
                }
                else
                {
                    PublishAudioEffect(
                        "audio.se_stop",
                        new Dictionary<string, string?>
                        {
                            ["id"] = arguments[0].StringValue,
                        });
                }

                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(
                    session,
                    instruction,
                    arguments.Count == 0 || arguments[0].Kind == RuntimeValueKind.Null
                        ? "audio.se_stop_all"
                        : "audio.se_stop");

            case "se_stop_all":
                if (arguments.Count != 0)
                {
                    return Fault(session, "KESR3310", "Callable 'se_stop_all' requires no arguments.");
                }

                PublishAudioEffect("audio.se_stop_all", new Dictionary<string, string?>());
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "audio.se_stop_all");

            case "voice_stop":
                if (arguments.Count != 0)
                {
                    return Fault(session, "KESR3310", "Callable 'voice_stop' requires no arguments.");
                }

                PublishAudioEffect("audio.voice_stop", new Dictionary<string, string?>());
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return WaitForPublishedHostEffect(session, instruction, "audio.voice_stop");

            case "number_to_string":
                return InvokeMappedSyscall(session, instruction, "core.number_to_string", arguments, returnsValue);

            case "bool_to_string":
                return InvokeMappedSyscall(session, instruction, "core.bool_to_string", arguments, returnsValue);

            case "array_len":
                return InvokeMappedSyscall(session, instruction, "core.array_len", arguments, returnsValue);

            case "str_len":
                return InvokeMappedSyscall(session, instruction, "core.str_len", arguments, returnsValue);

            case "assert":
                return InvokeMappedSyscall(session, instruction, "core.assert", arguments, returnsValue);

            case "set_param_string":
                if (arguments.Count != 2 || arguments[0].Kind != RuntimeValueKind.String || arguments[1].Kind != RuntimeValueKind.String)
                {
                    return Fault(session, "KESR3310", "Callable 'set_param_string' requires key:string and value:string arguments.");
                }

                gameParameters.Set(arguments[0].StringValue ?? string.Empty, RuntimeValue.String(arguments[1].StringValue ?? string.Empty));
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return KesVmExecutionResult.Success();

            case "set_param_number":
                if (arguments.Count != 2 || arguments[0].Kind != RuntimeValueKind.String || arguments[1].Kind != RuntimeValueKind.Number || arguments[1].NumberValue is null)
                {
                    return Fault(session, "KESR3310", "Callable 'set_param_number' requires key:string and value:number arguments.");
                }

                gameParameters.Set(arguments[0].StringValue ?? string.Empty, RuntimeValue.Number(arguments[1].NumberValue.GetValueOrDefault()));
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return KesVmExecutionResult.Success();

            case "set_param_bool":
                if (arguments.Count != 2 || arguments[0].Kind != RuntimeValueKind.String || arguments[1].Kind != RuntimeValueKind.Bool || arguments[1].BoolValue is null)
                {
                    return Fault(session, "KESR3310", "Callable 'set_param_bool' requires key:string and value:bool arguments.");
                }

                gameParameters.Set(arguments[0].StringValue ?? string.Empty, RuntimeValue.Bool(arguments[1].BoolValue.GetValueOrDefault()));
                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.Null);
                }

                return KesVmExecutionResult.Success();

            case "get_param":
                if (arguments.Count != 1 || arguments[0].Kind != RuntimeValueKind.String)
                {
                    return Fault(session, "KESR3310", "Callable 'get_param' requires one key:string argument.");
                }

                var key = arguments[0].StringValue ?? string.Empty;
                if (!gameParameters.TryGet(key, out var parameterValue))
                {
                    return Fault(session, "KESR3406", $"Game parameter '{key}' is not defined.");
                }

                if (returnsValue)
                {
                    session.PushOperand(RuntimeValue.String(FormatRuntimeValue(parameterValue)));
                }

                return KesVmExecutionResult.Success();

            default:
                return Fault(session, "KESR3310", $"Callable '{callName}' is not supported.");
        }
    }

    private static string FormatRuntimeValue(RuntimeValue value)
    {
        return value.Kind switch
        {
            RuntimeValueKind.Bool => value.BoolValue == true ? "true" : "false",
            RuntimeValueKind.Number => value.NumberValue.GetValueOrDefault().ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            RuntimeValueKind.String => value.StringValue ?? string.Empty,
            RuntimeValueKind.Null => string.Empty,
            RuntimeValueKind.Reference => value.ReferenceId ?? string.Empty,
            _ => string.Empty,
        };
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

    private static int? FindNextInstructionIndex(KesVmSession session, KlibInstruction instruction)
    {
        return session.GetNextInstructionIndex(instruction);
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
    private static readonly KesVmExecutionResult SuccessfulResult =
        new KesVmExecutionResult(true, Array.Empty<RuntimeDiagnostic>(), RuntimeFailureKind.None);

    public static KesVmExecutionResult Success()
    {
        return SuccessfulResult;
    }

    public static KesVmExecutionResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new KesVmExecutionResult(false, diagnostics, failureKind);
    }
}

public sealed class KesVmPerformanceCounters
{
    private readonly long[] opcodeCounts = new long[256];

    public long RunInvocations { get; private set; }

    public long SuccessfulRunInvocations { get; private set; }

    public long FailedRunInvocations { get; private set; }

    public long TotalInstructions { get; private set; }

    public int MaximumObservedOperandStackDepth { get; private set; }

    internal void BeginRun()
    {
        RunInvocations++;
    }

    internal void RecordInstruction(KlibOpCode opCode, int operandStackDepth)
    {
        TotalInstructions++;
        opcodeCounts[(byte)opCode]++;
        if (operandStackDepth > MaximumObservedOperandStackDepth)
        {
            MaximumObservedOperandStackDepth = operandStackDepth;
        }
    }

    internal void CompleteRun(bool succeeded)
    {
        if (succeeded)
        {
            SuccessfulRunInvocations++;
        }
        else
        {
            FailedRunInvocations++;
        }
    }

    public long GetOpcodeCount(KlibOpCode opCode)
    {
        return opcodeCounts[(byte)opCode];
    }

    public KesVmPerformanceSnapshot CaptureSnapshot()
    {
        var capturedOpcodeCounts = new Dictionary<KlibOpCode, long>();
        for (var index = 0; index < opcodeCounts.Length; index++)
        {
            if (opcodeCounts[index] > 0)
            {
                capturedOpcodeCounts[(KlibOpCode)(byte)index] = opcodeCounts[index];
            }
        }

        return new KesVmPerformanceSnapshot(
            RunInvocations,
            SuccessfulRunInvocations,
            FailedRunInvocations,
            TotalInstructions,
            MaximumObservedOperandStackDepth,
            capturedOpcodeCounts);
    }

    public void Reset()
    {
        Array.Clear(opcodeCounts, 0, opcodeCounts.Length);
        RunInvocations = 0;
        SuccessfulRunInvocations = 0;
        FailedRunInvocations = 0;
        TotalInstructions = 0;
        MaximumObservedOperandStackDepth = 0;
    }
}

public sealed record KesVmPerformanceSnapshot(
    long RunInvocations,
    long SuccessfulRunInvocations,
    long FailedRunInvocations,
    long TotalInstructions,
    int MaximumObservedOperandStackDepth,
    IReadOnlyDictionary<KlibOpCode, long> OpcodeCounts);
}
