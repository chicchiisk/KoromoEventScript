using System.Globalization;
using KoromoEventScript.Runtime.Core.Execution;

namespace KoromoEventScript.Runtime.Core.Manifests;

public sealed class RuntimeTriggerEvaluator
{
    private readonly IRuntimeGameParameterStore gameParameters;

    public RuntimeTriggerEvaluator(IRuntimeGameParameterStore gameParameters)
    {
        this.gameParameters = gameParameters;
    }

    public bool IsMatch(RuntimeTrigger? trigger, string? previousEventId)
    {
        if (trigger is null)
        {
            return false;
        }

        return trigger.Conditions.All(condition => IsConditionMatch(condition, previousEventId)) &&
            (trigger.Or.Count == 0 || trigger.Or.Any(or => IsMatch(or, previousEventId)));
    }

    private bool IsConditionMatch(RuntimeTriggerCondition condition, string? previousEventId)
    {
        return condition.Kind switch
        {
            "from" => StringComparer.Ordinal.Equals(condition.From, previousEventId),
            "is" => IsParamMatch(condition),
            _ => false,
        };
    }

    private bool IsParamMatch(RuntimeTriggerCondition condition)
    {
        if (string.IsNullOrWhiteSpace(condition.Param) ||
            condition.Value is null ||
            !gameParameters.TryGet(condition.Param, out var actual))
        {
            return false;
        }

        return condition.Value.Kind switch
        {
            "bool" => actual.Kind == RuntimeValueKind.Bool &&
                string.Equals(actual.BoolValue == true ? "true" : "false", condition.Value.Text, StringComparison.OrdinalIgnoreCase),
            "number" => actual.Kind == RuntimeValueKind.Number &&
                double.TryParse(condition.Value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedNumber) &&
                Math.Abs(actual.NumberValue.GetValueOrDefault() - expectedNumber) < double.Epsilon,
            "string" => actual.Kind == RuntimeValueKind.String &&
                StringComparer.Ordinal.Equals(actual.StringValue ?? string.Empty, condition.Value.Text),
            _ => false,
        };
    }
}
