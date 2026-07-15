using System;
using System.Collections.Generic;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using NUnit.Framework;

namespace KoromoEventScript.Unity.Runtime.Tests
{

public sealed class KesVmRuntimeTests
{
    [Test]
    public void Executor_RunsArithmeticModule()
    {
        var document = new KlibDocument(
            new KlibVersion(1, 0, 0),
            new KlibModuleInfo("runtime-test", "runtime-test", "runtime-test.kc", null),
            Array.Empty<KlibImport>(),
            Array.Empty<KlibConstant>(),
            Array.Empty<KlibVariable>(),
            new[]
            {
                Instruction(0, KlibOpCode.PushInt, 2),
                Instruction(1, KlibOpCode.PushInt, 3),
                Instruction(2, KlibOpCode.Add),
                Instruction(3, KlibOpCode.End),
            },
            Array.Empty<KlibLabel>(),
            new KlibDebugInfo(null, null, Array.Empty<KlibSourceMapping>()));

        var session = new KesVmSession(document);
        var result = new KesVmExecutor().Run(session);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(session.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
        Assert.That(session.OperandStack, Has.Count.EqualTo(1));
        Assert.That(session.OperandStack[0].Kind, Is.EqualTo(RuntimeValueKind.Number));
        Assert.That(session.OperandStack[0].NumberValue, Is.EqualTo(5d));
    }

    private static KlibInstruction Instruction(int index, KlibOpCode opcode, params int[] operands)
    {
        return new KlibInstruction(
            index,
            index,
            opcode,
            operands,
            null,
            KlibMappingKind.Synthetic);
    }
}
}
