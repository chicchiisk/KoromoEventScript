using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Manifests;

namespace KoromoEventScript.Runtime.Core.Tests.Manifests;

public sealed class RuntimeTriggerEvaluatorTests
{
    [Test]
    public void IsMatch_WithFromAndParamConditions_ReturnsTrueOnlyWhenAllConditionsMatch()
    {
        var parameters = new RuntimeGameParameterStore();
        parameters.Set("route_1", RuntimeValue.String("chapter002_intro"));
        var evaluator = new RuntimeTriggerEvaluator(parameters);
        var trigger = new RuntimeTrigger(
            [
                new RuntimeTriggerCondition("from", "chapter001_intro", null, null),
                new RuntimeTriggerCondition("is", null, "route_1", new RuntimeTriggerValue("string", "chapter002_intro")),
            ],
            []);

        Assert.Multiple(() =>
        {
            Assert.That(evaluator.IsMatch(trigger, "chapter001_intro"), Is.True);
            Assert.That(evaluator.IsMatch(trigger, "chapter003_intro"), Is.False);
        });
    }

    [Test]
    public void IsMatch_WithMultipleOrGroups_ReturnsTrueWhenAnyGroupMatches()
    {
        var parameters = new RuntimeGameParameterStore();
        parameters.Set("route_2", RuntimeValue.String("chapter004_intro"));
        var evaluator = new RuntimeTriggerEvaluator(parameters);
        var trigger = new RuntimeTrigger(
            [],
            [
                new RuntimeTrigger(
                    [
                        new RuntimeTriggerCondition("from", "chapter002_intro", null, null),
                        new RuntimeTriggerCondition("is", null, "route_2", new RuntimeTriggerValue("string", "chapter004_intro")),
                    ],
                    []),
                new RuntimeTrigger(
                    [
                        new RuntimeTriggerCondition("from", "chapter003_intro", null, null),
                        new RuntimeTriggerCondition("is", null, "route_2", new RuntimeTriggerValue("string", "chapter004_intro")),
                    ],
                    []),
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluator.IsMatch(trigger, "chapter003_intro"), Is.True);
            Assert.That(evaluator.IsMatch(trigger, "chapter004_intro"), Is.False);
        });
    }
}
