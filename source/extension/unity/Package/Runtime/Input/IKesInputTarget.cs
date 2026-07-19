using KoromoEventScript.Runtime.Core.Execution;

namespace KoromoEventScript.Unity
{

public interface IKesInputTarget
{
    RuntimeContinuation Continuation { get; }

    bool ContinueAdvance();

    bool ChooseSelection(int choiceIndex);
}
}
