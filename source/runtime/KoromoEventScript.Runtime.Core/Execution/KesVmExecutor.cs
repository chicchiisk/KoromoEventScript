using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Execution;

public sealed class KesVmExecutor
{
    private const int DefaultMaxInstructionCount = 10_000;

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

    private static KesVmExecutionResult Execute(KesVmSession session, KlibInstruction instruction)
    {
        switch (instruction.OpCode)
        {
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

            default:
                return Fault(session, "KESR3199", $"Opcode '{instruction.OpCode}' is not supported by this executor task.");
        }
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
