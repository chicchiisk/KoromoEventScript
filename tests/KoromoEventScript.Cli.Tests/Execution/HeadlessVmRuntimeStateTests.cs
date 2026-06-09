using KoromoEventScript.Cli.Execution;

namespace KoromoEventScript.Cli.Tests.Execution;

public class HeadlessVmRuntimeStateTests
{
    [Test]
    public void RuntimeValue_FromObject_NormalizesPrimitiveValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HeadlessVmRuntimeValue.FromObject(null).Kind, Is.EqualTo(HeadlessVmRuntimeValueKind.Null));
            Assert.That(HeadlessVmRuntimeValue.FromObject("alpha").StringValue, Is.EqualTo("alpha"));
            Assert.That(HeadlessVmRuntimeValue.FromObject(42d).NumberValue, Is.EqualTo(42d));
            Assert.That(HeadlessVmRuntimeValue.FromObject(true).BoolValue, Is.True);
        });
    }

    [Test]
    public void RuntimeState_TryPopOperand_ReturnsFalseWhenStackIsEmpty()
    {
        var state = new HeadlessVmRuntimeState();

        var popped = state.TryPopOperand(out var value);

        Assert.Multiple(() =>
        {
            Assert.That(popped, Is.False);
            Assert.That(value, Is.Null);
        });
    }

    [Test]
    public void RuntimeState_PushOperand_StoresTypedValuesInLifoOrder()
    {
        var state = new HeadlessVmRuntimeState();
        state.PushOperand(HeadlessVmRuntimeValue.FromObject("first"));
        state.PushOperand(HeadlessVmRuntimeValue.FromObject(2d));

        var firstPop = state.TryPopOperand(out var topValue);
        var secondPop = state.TryPopOperand(out var nextValue);

        Assert.Multiple(() =>
        {
            Assert.That(firstPop, Is.True);
            Assert.That(secondPop, Is.True);
            Assert.That(topValue?.Kind, Is.EqualTo(HeadlessVmRuntimeValueKind.Number));
            Assert.That(topValue?.NumberValue, Is.EqualTo(2d));
            Assert.That(nextValue?.Kind, Is.EqualTo(HeadlessVmRuntimeValueKind.String));
            Assert.That(nextValue?.StringValue, Is.EqualTo("first"));
        });
    }

    [Test]
    public void RuntimeState_ExportAndRestoreOperands_PreservesStackOrder()
    {
        var original = new HeadlessVmRuntimeState();
        original.PushOperand(HeadlessVmRuntimeValue.FromObject("first"));
        original.PushOperand(HeadlessVmRuntimeValue.FromObject(2d));

        var restored = new HeadlessVmRuntimeState();
        restored.RestoreOperands(original.ExportOperands());

        var firstPop = restored.TryPopOperand(out var topValue);
        var secondPop = restored.TryPopOperand(out var nextValue);

        Assert.Multiple(() =>
        {
            Assert.That(firstPop, Is.True);
            Assert.That(secondPop, Is.True);
            Assert.That(topValue?.NumberValue, Is.EqualTo(2d));
            Assert.That(nextValue?.StringValue, Is.EqualTo("first"));
        });
    }
}
