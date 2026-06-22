using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests.Execution;

public sealed class KesVmOpcodeCoverageTests
{
    [Test]
    public void ExecutorDispatchesEveryKlibOpcode()
    {
        var allOpcodes = Enum.GetValues<KlibOpCode>().OrderBy(static opcode => opcode).ToArray();
        var dispatchedOpcodes = KesVmExecutor.DispatchedOpCodes.OrderBy(static opcode => opcode).ToArray();
        var missing = allOpcodes.Except(dispatchedOpcodes).ToArray();
        var extra = dispatchedOpcodes.Except(allOpcodes).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.Empty, $"Missing dispatch opcodes: {string.Join(", ", missing)}");
            Assert.That(extra, Is.Empty, $"Unknown dispatch opcodes: {string.Join(", ", extra)}");
        });
    }
}
