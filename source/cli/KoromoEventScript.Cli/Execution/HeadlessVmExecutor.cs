using KoromoEventScript.Cli.Compilation;

namespace KoromoEventScript.Cli.Execution;

public sealed class HeadlessVmExecutor
{
    public HeadlessVmExecutionResult RunToBoundary(KlibDocument document, int startOffset, HeadlessVmObservationLog observation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(observation);

        if (document.Instructions.Count == 0)
        {
            return new HeadlessVmExecutionResult(
                HeadlessVmState.Completed(document.Module.ScriptId, startOffset),
                observation);
        }

        var instructionsByOffset = document.Instructions.ToDictionary(static instruction => instruction.Offset);
        var stack = new Stack<object?>();
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
                    stack.Push(null);
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.PushConst:
                    stack.Push(ResolveConstant(document, instruction.Operands[0]));
                    offset = GetNextOffset(document, instruction);
                    break;

                case KlibOpCode.SysCallVoid:
                    {
                        var syscallName = ResolveString(document, instruction.Operands[0]);
                        var arguments = PopArguments(stack, instruction.Offset, instruction.Operands[1], document);
                        if (arguments is null)
                        {
                            return Fault(document, instruction.Offset, "Not enough arguments on the stack for syscall execution.", currentObservation);
                        }

                        var nextOffset = GetNextOffset(document, instruction);
                        if (string.Equals(syscallName, "scenario.say", StringComparison.Ordinal))
                        {
                            currentObservation = currentObservation.AppendSay(
                                ValueToDisplayString(arguments[0]),
                                ValueToDisplayString(arguments[1]) ?? string.Empty);
                            return new HeadlessVmExecutionResult(
                                HeadlessVmState.WaitingForAdvance(document.Module.ScriptId, nextOffset),
                                currentObservation);
                        }

                        if (string.Equals(syscallName, "scenario.nar", StringComparison.Ordinal))
                        {
                            currentObservation = currentObservation.AppendNarration(ValueToDisplayString(arguments[0]) ?? string.Empty);
                            return new HeadlessVmExecutionResult(
                                HeadlessVmState.WaitingForAdvance(document.Module.ScriptId, nextOffset),
                                currentObservation);
                        }

                        offset = nextOffset;
                        break;
                    }

                case KlibOpCode.Select:
                    {
                        var prompt = stack.Count > 0 ? stack.Pop() : null;
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

    private static object?[]? PopArguments(Stack<object?> stack, int instructionOffset, int argumentCount, KlibDocument document)
    {
        if (stack.Count < argumentCount)
        {
            return null;
        }

        var arguments = new object?[argumentCount];
        for (var index = argumentCount - 1; index >= 0; index--)
        {
            arguments[index] = stack.Pop();
        }

        return arguments;
    }

    private static object? ResolveConstant(KlibDocument document, int constantIndex)
    {
        var constant = document.Constants[constantIndex];
        return constant.Kind switch
        {
            KlibConstantKind.String => constant.StringValue,
            KlibConstantKind.Number => constant.NumberValue,
            KlibConstantKind.Bool => constant.BoolValue,
            KlibConstantKind.Null => null,
            _ => ResolveReferenceValue(document, constant),
        };
    }

    private static string ResolveString(KlibDocument document, int constantIndex)
    {
        return ValueToDisplayString(ResolveConstant(document, constantIndex)) ?? string.Empty;
    }

    private static object? ResolveReferenceValue(KlibDocument document, KlibConstant constant)
    {
        if (!constant.ReferenceIndex.HasValue)
        {
            return null;
        }

        return document.Constants[constant.ReferenceIndex.Value].StringValue;
    }

    private static string? ValueToDisplayString(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            double number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
}

public sealed record HeadlessVmExecutionResult(
    HeadlessVmState State,
    HeadlessVmObservationLog Observation);
