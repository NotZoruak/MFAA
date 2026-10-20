using Avalonia.Controls;
using Avalonia.LogicalTree;
using MFAAvalonia.Features.CombatStrategies;
using MFAAvalonia.Features.CombatStrategies.Controls;
using System.Linq;
using Xunit;

namespace MFAAvalonia.Tests;

[Collection("策略界面")]
public sealed class CombatStrategyConditionUiTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UltimateCondition_SelectsMultiplePositionsWithoutExtraDropdowns(bool ready)
    {
        var predicate = State(ready, 1);
        var editor = new StrategyConditionEditor(new ConditionGroup { Children = [predicate] }, () => { });
        var controls = editor.GetLogicalDescendants().ToArray();
        Assert.Equal(2, controls.OfType<ComboBox>().Count());
        Assert.Equal(3, controls.OfType<CheckBox>().Count());
        var choice = controls.OfType<ComboBox>().Single(x => x.Name == "ConditionMetric");
        Assert.Equal(ready ? "奥义已就绪" : "奥义未就绪", choice.SelectedItem!.ToString());
        controls.OfType<CheckBox>().Single(x => Equals(x.Content, "位置 3")).IsChecked = true;
        Assert.Equal([1, 3], predicate.Slots);
        Assert.Equal(ConditionMatch.All, predicate.SlotMatch);
        Assert.Equal(ready, predicate.Expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UltimateCondition_ExcludesOwnPosition(bool ready)
    {
        var editor = new StrategyConditionEditor(new ConditionGroup { Children = [State(ready, 1)] }, () => { }, ownerSlot: 2);
        var slots = editor.GetLogicalDescendants().OfType<CheckBox>().ToArray();
        Assert.Equal(2, slots.Length);
        Assert.DoesNotContain(slots, x => Equals(x.Content, "位置 2"));
    }

    [Fact]
    public void SwitchingReadiness_PreservesCheckedPositions()
    {
        var predicate = State(true, 1, 3);
        var editor = new StrategyConditionEditor(new ConditionGroup { Children = [predicate] }, () => { });
        var choice = editor.GetLogicalDescendants().OfType<ComboBox>().Single(x => x.Name == "ConditionMetric");
        choice.SelectedItem = choice.Items.Cast<object>().Single(x => x.ToString() == "奥义未就绪");
        Assert.False(predicate.Expected);
        Assert.Equal([1, 3], predicate.Slots);
    }

    [Theory]
    [InlineData(ConditionMetric.EnemyHpPercent)]
    [InlineData(ConditionMetric.AllyHpPercent)]
    public void HealthCondition_OffersOnlyPercentageAndPersistsValue(ConditionMetric metric)
    {
        var predicate = new ConditionPredicate { Metric = ConditionMetric.StageNormalLinks, Comparison = ComparisonOperator.GreaterOrEqual, Value = 1 };
        var document = StrategyDefaults.CreateEditable("百分比策略");
        var root = new ConditionGroup { Children = [predicate] };
        document.Stages[0].Actions.Ultimates[0].Condition = root;
        var editor = new StrategyConditionEditor(root, () => { });
        var choice = editor.GetLogicalDescendants().OfType<ComboBox>().Single(x => x.Name == "ConditionMetric");
        var labels = choice.Items.Cast<object>().Select(x => x.ToString()).ToArray();
        Assert.DoesNotContain("敌方血量数值", labels);
        Assert.DoesNotContain("我方血量数值", labels);
        Assert.DoesNotContain("奥义已释放", labels);
        Assert.DoesNotContain("位置奥义已释放", labels);
        Assert.Contains("奥义已就绪", labels);
        Assert.Contains("奥义未就绪", labels);
        choice.SelectedItem = choice.Items.Cast<object>().Single(x => x.ToString() == (metric == ConditionMetric.EnemyHpPercent ? "敌方血量百分比" : "我方血量百分比"));
        var number = editor.GetLogicalDescendants().OfType<NumericUpDown>().Single();
        Assert.Equal(100m, number.Maximum);
        number.Value = 35.5m;
        var saved = StrategyJson.Deserialize(StrategyJson.Serialize(document));
        var restored = Assert.IsType<ConditionPredicate>(Assert.IsType<ConditionGroup>(saved.Stages[0].Actions.Ultimates[0].Condition).Children[0]);
        Assert.Equal(metric, restored.Metric);
        Assert.Equal(35.5m, restored.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnyPosition_UsesConditionGroupAndPersistsReadiness(bool ready)
    {
        var document = StrategyDefaults.CreateEditable("任一位置");
        document.Stages[0].Actions.Ultimates[2].Condition = new ConditionGroup
        {
            Match = ConditionMatch.Any,
            Children = [State(ready, 1), State(ready, 2)]
        };
        var saved = StrategyJson.Deserialize(StrategyJson.Serialize(document));
        var root = Assert.IsType<ConditionGroup>(saved.Stages[0].Actions.Ultimates[2].Condition);
        Assert.Equal(ConditionMatch.Any, root.Match);
        Assert.All(root.Children, x => Assert.Equal(ready, Assert.IsType<ConditionPredicate>(x).Expected));
    }

    [Theory]
    [InlineData("enemy_hp")]
    [InlineData("ally_hp")]
    [InlineData("ultimate_released")]
    public void RemovedMetrics_AreRejectedWithoutLegacyConversion(string metric)
    {
        var document = StrategyDefaults.CreateEditable("新格式");
        document.Stages[0].Actions.Ultimates[0].Condition = new ConditionGroup
        {
            Children = [new ConditionPredicate { Metric = ConditionMetric.StageNormalLinks, Comparison = ComparisonOperator.GreaterOrEqual, Value = 1 }]
        };
        var json = StrategyJson.Serialize(document).Replace("stage_normal_links", metric);
        Assert.Throws<StrategyValidationException>(() => StrategyJson.Deserialize(json));
    }

    private static ConditionPredicate State(bool ready, params int[] slots) =>
        new() { Metric = ConditionMetric.UltimateReady, Slots = slots.ToList(), SlotMatch = ConditionMatch.All, Expected = ready };
}
