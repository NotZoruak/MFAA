using MFAAvalonia.Extensions.MaaFW;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MFAAvalonia.Features.CombatStrategies;

public static class StrategyOptionProvider
{
    public const string OptionName = "选择作战策略";
    public const string SelectedCaseDataKey = "__selected_strategy_case";

    public static List<MaaInterface.MaaInterfaceOptionCase> CreateCases(string directory, Action<string>? warning = null)
    {
        var store = new StrategyStore(directory);
        var catalog = store.List();
        foreach (var error in catalog.Errors)
            warning?.Invoke($"作战策略未加入任务选项：{error}");

        return catalog.Strategies
            .Select(summary => new MaaInterface.MaaInterfaceOptionCase
            {
                Name = summary.Id,
                Label = summary.Name,
                DisplayName = summary.Name,
                PipelineOverride = BuildOverride(summary.Id)
            })
            .ToList();
    }

    public static void Refresh(MaaInterface? maaInterface, string directory, Action<string>? warning = null)
    {
        if (maaInterface?.Option?.TryGetValue(OptionName, out var option) != true || !option.IsSelect)
            return;

        option.Cases = CreateCases(directory, warning);
        option.DefaultCases = ["default"];
    }

    public static void RememberSelectedCase(MaaInterface.MaaInterfaceSelectOption option, string? caseName)
    {
        if (option.Name != OptionName || string.IsNullOrWhiteSpace(caseName))
            return;
        option.Data ??= new Dictionary<string, string?>();
        option.Data[SelectedCaseDataKey] = caseName;
    }

    public static bool RestoreSelectedCase(
        MaaInterface.MaaInterfaceOption definition,
        MaaInterface.MaaInterfaceSelectOption option)
    {
        if (option.Name != OptionName || option.Data?.TryGetValue(SelectedCaseDataKey, out var caseName) != true)
            return false;

        var index = definition.Cases?.FindIndex(item => item.Name == caseName) ?? -1;
        if (index < 0)
            return false;
        option.Index = index;
        return true;
    }

    private static Dictionary<string, JToken> BuildOverride(string strategyId) => new()
    {
        ["RB_RunBattle"] = new JObject
        {
            ["action"] = new JObject
            {
                ["param"] = new JObject
                {
                    ["custom_action_param"] = new JObject
                    {
                        ["strategy_id"] = strategyId
                    }
                }
            }
        }
    };
}
