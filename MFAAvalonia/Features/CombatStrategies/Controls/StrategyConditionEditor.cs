using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using System;
using System.Linq;

namespace MFAAvalonia.Features.CombatStrategies.Controls;

public sealed class StrategyConditionEditor : UserControl
{
    private readonly Action _changed;
    private readonly int? _ownerSlot;
    private sealed record ConditionKind(ConditionMetric Metric, bool? Expected = null);

    public StrategyConditionEditor(ConditionGroup root, Action changed, int? ownerSlot = null)
    {
        _changed = changed;
        _ownerSlot = ownerSlot;
        Content = BuildGroup(root, null, 0);
    }

    private Control BuildGroup(ConditionGroup group, Action? remove, int depth)
    {
        var children = new StackPanel { Spacing = 8 };
        var mode = StrategyUi.Choice(StrategyUi.Matches, group.Match, value => { group.Match = value; _changed(); }, 130);
        mode.Name = "ConditionMatch";
        var addCondition = StrategyUi.Button("添加条件", () =>
        {
            group.Children.Add(NewPredicate());
            RefreshChildren();
            _changed();
        });
        addCondition.Name = "AddCondition";
        var addGroup = StrategyUi.Button("添加条件组", () =>
        {
            group.Children.Add(new ConditionGroup { Children = [NewPredicate()] });
            RefreshChildren();
            _changed();
        });
        addGroup.Name = "AddConditionGroup";
        addGroup.IsEnabled = depth < StrategyValidator.MaxConditionDepth - 2;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        header.Children.Add(StrategyUi.Row(mode, addCondition, addGroup));
        if (remove != null)
        {
            var close = RemoveButton(remove, "移除条件组");
            close.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
        }
        var branch = new Border
        {
            Child = children,
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(12, 0, 0, 0),
            Margin = new Thickness(3, 2, 0, 0)
        };
        branch.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("SukiPrimaryColor25"));
        var body = StrategyUi.Column(header, branch);
        var card = new Border
        {
            Child = body,
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1)
        };
        card.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("SukiControlBorderBrush"));
        card.Bind(Border.BackgroundProperty, new DynamicResourceExtension(depth % 2 == 0 ? "SukiCardBackground" : "SukiPrimaryColor5"));
        RefreshChildren();
        return card;

        void RefreshChildren()
        {
            children.Children.Clear();
            if (group.Children.Count == 0)
                children.Children.Add(StrategyUi.Text("尚未添加条件"));
            foreach (var condition in group.Children.ToArray())
            {
                void Remove()
                {
                    group.Children.Remove(condition);
                    RefreshChildren();
                    _changed();
                }
                children.Children.Add(condition is ConditionGroup nested
                    ? BuildGroup(nested, Remove, depth + 1)
                    : BuildPredicate((ConditionPredicate)condition, Remove));
            }
        }
    }

    private Control BuildPredicate(ConditionPredicate predicate, Action remove)
    {
        var host = new StackPanel { Spacing = 4 };
        Render();
        return host;

        void Render()
        {
            host.Children.Clear();
            var selected = new ConditionKind(predicate.Metric, predicate.Metric == ConditionMetric.UltimateReady ? predicate.Expected : null);
            var metric = StrategyUi.Choice(Metrics, selected, value =>
            {
                var positions = predicate.Metric == ConditionMetric.UltimateReady && value.Metric == ConditionMetric.UltimateReady
                    ? predicate.Slots?.ToList() : null;
                predicate.Metric = value.Metric;
                predicate.Comparison = null;
                predicate.Value = null;
                predicate.Slots = null;
                predicate.SlotMatch = null;
                predicate.Expected = null;
                if (value.Metric == ConditionMetric.UltimateReady)
                {
                    predicate.Slots = positions ?? [Enumerable.Range(1, 3).First(x => x != _ownerSlot)];
                    predicate.SlotMatch = ConditionMatch.All;
                    predicate.Expected = value.Expected;
                }
                else if (value.Metric == ConditionMetric.FrenzyActive) predicate.Expected = true;
                else { predicate.Comparison = ComparisonOperator.GreaterOrEqual; predicate.Value = 1; }
                _changed();
                Render();
            }, 180);
            metric.Name = "ConditionMetric";
            var row = StrategyUi.Row(metric);
            if (predicate.Metric == ConditionMetric.UltimateReady)
            {
                foreach (var slot in Enumerable.Range(1, 3))
                {
                    if (slot == _ownerSlot) continue;
                    var position = StrategyUi.Check($"位置 {slot}", predicate.Slots?.Contains(slot) == true, selected =>
                    {
                        predicate.Slots ??= [];
                        if (selected && !predicate.Slots.Contains(slot)) predicate.Slots.Add(slot);
                        if (!selected) predicate.Slots.Remove(slot);
                        predicate.Slots.Sort();
                        predicate.SlotMatch = ConditionMatch.All;
                        _changed();
                    });
                    ToolTip.SetTip(position, "多选时，所选位置都需满足此状态；任一满足请使用条件组。");
                    row.Children.Add(position);
                }
            }
            else if (predicate.Metric == ConditionMetric.FrenzyActive)
                row.Children.Add(StrategyUi.Choice(
                    new[] { new EditorChoice<bool>(true, "处于乱舞"), new EditorChoice<bool>(false, "不处于乱舞") },
                    predicate.Expected ?? true, value => { predicate.Expected = value; _changed(); }));
            else
            {
                row.Children.Add(StrategyUi.Choice(Comparisons, predicate.Comparison ?? ComparisonOperator.GreaterOrEqual, value => { predicate.Comparison = value; _changed(); }, 85));
                var percent = predicate.Metric is ConditionMetric.EnemyHpPercent or ConditionMetric.AllyHpPercent;
                var number = new NumericUpDown
                {
                    Name = "ConditionValue", Value = predicate.Value, Minimum = 0,
                    Maximum = percent ? 100 : 999999999999m, Increment = 1,
                    Width = 84,
                    Height = 36, MinHeight = 36, MaxHeight = 36,
                    Padding = new Thickness(8, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    ShowButtonSpinner = false,
                    FormatString = "0.##"
                };
                number.PropertyChanged += (_, e) =>
                {
                    if (e.Property != NumericUpDown.ValueProperty) return;
                    predicate.Value = number.Value;
                    _changed();
                };
                row.Children.Add(number);
                if (percent) row.Children.Add(StrategyUi.Text("%"));
            }
            row.Children.Add(RemoveButton(remove, "移除条件"));
            host.Children.Add(row);
        }
    }

    private static Button RemoveButton(Action remove, string description)
    {
        var button = StrategyUi.Button("×", remove);
        button.Width = button.Height = 30;
        button.MinWidth = button.MinHeight = 0;
        button.Padding = new Thickness(0);
        button.FontSize = 20;
        button.FontWeight = FontWeight.Normal;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.Classes.Add("BasicTight");
        ToolTip.SetTip(button, description);
        AutomationProperties.SetName(button, description);
        return button;
    }

    private static ConditionPredicate NewPredicate() => new()
    {
        Metric = ConditionMetric.StageNormalLinks,
        Comparison = ComparisonOperator.GreaterOrEqual,
        Value = 1
    };

    private static readonly EditorChoice<ConditionKind>[] Metrics =
    [
        new(new(ConditionMetric.BattleNormalLinks), "本场普通连线手数"),
        new(new(ConditionMetric.StageNormalLinks), "当前阶段普通连线手数"),
        new(new(ConditionMetric.EnemyHpPercent), "敌方血量百分比"),
        new(new(ConditionMetric.AllyHpPercent), "我方血量百分比"),
        new(new(ConditionMetric.UltimateReady, true), "奥义已就绪"),
        new(new(ConditionMetric.UltimateReady, false), "奥义未就绪"),
        new(new(ConditionMetric.FrenzyActive), "乱舞状态")
    ];

    private static readonly EditorChoice<ComparisonOperator>[] Comparisons =
    [
        new(ComparisonOperator.GreaterOrEqual, "≥"), new(ComparisonOperator.LessOrEqual, "≤"),
        new(ComparisonOperator.Greater, ">"), new(ComparisonOperator.Less, "<"),
        new(ComparisonOperator.Equal, "="), new(ComparisonOperator.NotEqual, "≠")
    ];
}
