using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using SukiUI.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MFAAvalonia.Features.CombatStrategies.Controls;

public sealed class StrategyStageCard : UserControl
{
    private readonly StrategyEditorSession _session;
    private readonly StrategyStage _stage;
    private readonly StackPanel _body = new() { Spacing = 8 };
    private readonly Button _title;
    private readonly PathIcon _disclosure = new()
    {
        Width = 14,
        Height = 14,
        Margin = new Thickness(8, 0, 4, 0),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Right,
        IsHitTestVisible = false
    };
    private readonly HashSet<int> _openSlots = [];
    private readonly HashSet<string> _openRules = [];
    public Control DragHandle { get; }
    public StrategyStage Stage => _stage;
    public bool IsExpanded
    {
        get => _body.IsVisible;
        set { _body.IsVisible = value; UpdateTitle(value); }
    }

    public StrategyStageCard(StrategyEditorSession session, StrategyStage stage, Action<string> showError, Func<string, string, Task<string?>> rename)
    {
        _session = session; _stage = stage;
        DragHandle = StrategyUi.DragHandle();
        DragHandle.IsEnabled = !session.IsReadOnly;
        _title = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            FontSize = 15,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };
        _title.Classes.Add("BasicTight");
        _title.Click += (_, _) => IsExpanded = !IsExpanded;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        header.Children.Add(DragHandle);
        Grid.SetColumn(_title, 1);
        header.Children.Add(_title);
        Grid.SetColumn(_disclosure, 2);
        header.Children.Add(_disclosure);
        _disclosure.Bind(PathIcon.ForegroundProperty, new DynamicResourceExtension("SukiLowText"));
        var renameItem = new MenuItem
        {
            Header = "重命名",
            IsEnabled = !session.IsReadOnly,
            Icon = CreateMenuIcon(FluentIcons.Common.Icon.NotepadEdit)
        };
        renameItem.Click += async (_, _) => { var name = await rename("重命名阶段", stage.Name); if (name != null) { stage.Name = name; session.StructuralChange(); } };
        var copy = new MenuItem
        {
            Header = "复制阶段",
            IsEnabled = !session.IsReadOnly,
            Icon = CreateMenuIcon(FluentIcons.Common.Icon.Copy)
        };
        copy.Click += (_, _) => session.CopyStage(stage);
        var initial = new MenuItem
        {
            Header = "设为初始阶段",
            IsEnabled = !session.IsReadOnly && session.Current.InitialStageId != stage.Id,
            Icon = CreateMenuIcon(FluentIcons.Common.Icon.Star)
        };
        initial.Click += (_, _) => session.SetInitialStage(stage);
        var deleteIcon = CreateMenuIcon(FluentIcons.Common.Icon.Delete);
        deleteIcon.Bind(Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty,
            new DynamicResourceExtension("SukiDangerColor"));
        var delete = new MenuItem
        {
            Header = "删除阶段",
            IsEnabled = !session.IsReadOnly,
            Icon = deleteIcon,
            Classes = { "DangerContextDelete" }
        };
        delete.Click += (_, _) => { try { session.DeleteStage(stage); } catch (InvalidOperationException ex) { showError(ex.Message); } };
        var menu = new MFAAvalonia.Controls.LifecycleSafeContextMenu { Items = { renameItem, copy, initial, delete } };
        _body.IsEnabled = !session.IsReadOnly;
        _body.Margin = new Thickness(14, 8, 14, 12);
        AddSection("行动优先级", BuildPriority(), "连线固定最后；普通操作与乱舞共用此顺序。");
        var bombs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        foreach (var level in new[] { 1, 2, 3 })
            bombs.Children.Add(StrategyUi.Check($"{level} 级", stage.Actions.BombLevels.Contains(level), value =>
            {
                if (value && !stage.Actions.BombLevels.Contains(level)) stage.Actions.BombLevels.Add(level);
                if (!value) stage.Actions.BombLevels.Remove(level);
                stage.Actions.BombLevels.Sort(); session.MarkDirty();
            }));
        AddSection("炸弹使用", bombs);
        AddSection("奥义", BuildUltimates());
        AddSection("普通连线", BuildNormalLinks());
        AddSection("乱舞", BuildFrenzy());
        AddSection("阶段转变", BuildTransitions());
        var headerBorder = new Border
        {
            Child = header,
            Padding = new Thickness(14, 12),
            BorderThickness = new Thickness(0, 0, 0, 1),
            // 整块标题栏接收右键，包括留白、边距和折叠箭头所在区域。
            Background = Brushes.Transparent,
            ContextMenu = menu
        };
        headerBorder.Bind(Border.BorderBrushProperty,
            new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("SukiControlBorderBrush"));
        var stageContent = new StackPanel { Spacing = 0, Children = { headerBorder, _body } };
        var stageCard = StrategyUi.Card(stageContent);
        stageCard.Padding = new Thickness(0);
        Content = stageCard;
        IsExpanded = true;
    }

    private void UpdateTitle(bool expanded)
    {
        var titleText = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        if (_session.Current.InitialStageId == _stage.Id)
        {
            var badgeText = new TextBlock
            {
                Text = "初始阶段",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiPrimaryColor"));
            var badge = new Border
            {
                Child = badgeText,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 3)
            };
            badge.Bind(Border.BackgroundProperty, new DynamicResourceExtension("SukiPrimaryColor10"));
            titleText.Children.Add(badge);
        }

        titleText.Children.Add(new TextBlock
        {
            Text = _stage.Name,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        _disclosure.Data = expanded ? Icons.ChevronDown : Icons.ChevronRight;
        _title.Content = titleText;
    }

    private static FluentIcons.Avalonia.Fluent.FluentIcon CreateMenuIcon(FluentIcons.Common.Icon icon) => new()
    {
        Icon = icon,
        IconSize = FluentIcons.Common.IconSize.Size16,
        IconVariant = FluentIcons.Common.IconVariant.Regular
    };

    private void AddSection(string name, Control content, string? description = null)
    {
        Control heading = StrategyUi.Section(name);
        if (description != null)
        {
            var inlineHeading = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 12
            };
            inlineHeading.Children.Add(heading);
            var descriptionText = StrategyUi.Text(description, muted: true);
            descriptionText.VerticalAlignment = VerticalAlignment.Center;
            descriptionText.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            Grid.SetColumn(descriptionText, 1);
            inlineHeading.Children.Add(descriptionText);
            heading = inlineHeading;
        }

        var section = new Border
        {
            Child = StrategyUi.Column(heading, content),
            Padding = new Thickness(0, 8, 0, 12),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        section.Bind(Border.BorderBrushProperty,
            new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("SukiControlBorderBrush"));
        _body.Children.Add(section);
    }

    private Control BuildPriority()
    {
        CheckBox? bombFirst = null;
        CheckBox? ultimateFirst = null;
        var syncing = false;
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };

        void Sync()
        {
            if (bombFirst == null || ultimateFirst == null) return;
            syncing = true;
            var selected = _stage.Actions.Priority.FirstOrDefault();
            bombFirst.IsChecked = selected == StrategyAction.Bomb;
            ultimateFirst.IsChecked = selected == StrategyAction.Ultimate;
            syncing = false;
        }

        void Select(StrategyAction action, bool isChecked)
        {
            if (syncing) return;
            var selected = _stage.Actions.Priority.FirstOrDefault();
            if (!isChecked)
            {
                if (selected == action) Sync();
                return;
            }

            if (selected != action)
            {
                _stage.Actions.Priority.Clear();
                _stage.Actions.Priority.Add(action);
                _stage.Actions.Priority.Add(action == StrategyAction.Bomb ? StrategyAction.Ultimate : StrategyAction.Bomb);
                _session.MarkDirty();
            }
            Sync();
        }

        bombFirst = StrategyUi.Check("先使用炸弹", _stage.Actions.Priority.FirstOrDefault() == StrategyAction.Bomb,
            value => Select(StrategyAction.Bomb, value));
        ultimateFirst = StrategyUi.Check("先使用奥义", _stage.Actions.Priority.FirstOrDefault() == StrategyAction.Ultimate,
            value => Select(StrategyAction.Ultimate, value));
        choices.Children.Add(bombFirst);
        choices.Children.Add(ultimateFirst);
        return choices;
    }

    private Control BuildUltimates()
    {
        var list = new StackPanel { Spacing = 8 };
        Render(); return list;
        void Render()
        {
            list.Children.Clear();
            foreach (var ultimate in _stage.Actions.Ultimates)
            {
                var handle = StrategyUi.DragHandle();
                var details = new StackPanel { IsVisible = _openSlots.Contains(ultimate.Slot) };
                var root = ultimate.Condition as ConditionGroup ?? new ConditionGroup();
                details.Children.Add(new StrategyConditionEditor(root, () => { ultimate.Condition = root.Children.Count == 0 ? null : root; _session.MarkDirty(); }, ultimate.Slot));
                var toggle = StrategyUi.Button("前置条件", () => { details.IsVisible = !details.IsVisible; if (details.IsVisible) _openSlots.Add(ultimate.Slot); else _openSlots.Remove(ultimate.Slot); });
                var row = StrategyUi.Column(StrategyUi.Row(handle, StrategyUi.Check($"位置 {ultimate.Slot}", ultimate.Enabled, value => { ultimate.Enabled = value; _session.MarkDirty(); }), toggle), details);
                list.Children.Add(row);
                StrategyReorder.Attach(handle, row, list, _stage.Actions.Ultimates, ultimate, _session.MarkDirty);
            }
        }
    }

    private Control BuildNormalLinks()
    {
        var settings = _stage.Actions.NormalLink;
        var colors = new WrapPanel { Orientation = Orientation.Horizontal, IsVisible = settings.PrioritizeColors };
        var toggle = StrategyUi.Check("设置目标颜色优先级", settings.PrioritizeColors, value =>
        {
            settings.PrioritizeColors = value;
            colors.IsVisible = value;
            _session.MarkDirty();
        });
        Render();

        return StrategyUi.Column(toggle, colors);

        void Render()
        {
            colors.Children.Clear();
            var rankedColors = settings.ColorPriority.Take(settings.ColorPriorityCount).ToList();
            foreach (var choice in StrategyUi.Colors)
            {
                var rank = rankedColors.IndexOf(choice.Value);
                var indicatorText = new TextBlock
                {
                    Text = rank >= 0 ? (rank + 1).ToString() : string.Empty,
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White
                };
                var indicator = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(5),
                    BorderThickness = new Thickness(1),
                    Child = indicatorText
                };
                indicator.Bind(Border.BackgroundProperty,
                    new DynamicResourceExtension(rank >= 0 ? "SukiPrimaryColor" : "SukiCardBackground"));
                indicator.Bind(Border.BorderBrushProperty,
                    new DynamicResourceExtension(rank >= 0 ? "SukiPrimaryColor" : "SukiControlBorderBrush"));

                var label = StrategyUi.Text(choice.Label);
                var content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { indicator, label }
                };
                var option = new Button
                {
                    Content = content,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(4, 3),
                    VerticalAlignment = VerticalAlignment.Center
                };
                option.Click += (_, _) => SelectColor(choice.Value);
                option.Margin = new Thickness(0, 3, 16, 3);
                colors.Children.Add(option);
            }
        }

        void SelectColor(GhoulColor color)
        {
            var priority = settings.ColorPriority;
            var count = settings.ColorPriorityCount;
            var ranked = priority.Take(count).ToList();
            var unranked = priority.Skip(count).ToList();

            if (count == priority.Count)
            {
                ranked.Remove(color);
                priority.Clear();
                priority.Add(color);
                priority.AddRange(ranked);
                settings.ColorPriorityCount = 1;
            }
            else if (ranked.Remove(color))
            {
                unranked.Insert(0, color);
                priority.Clear();
                priority.AddRange(ranked);
                priority.AddRange(unranked);
                settings.ColorPriorityCount = ranked.Count;
            }
            else
            {
                unranked.Remove(color);
                ranked.Add(color);
                priority.Clear();
                priority.AddRange(ranked);
                priority.AddRange(unranked);
                settings.ColorPriorityCount = ranked.Count;
            }

            _session.MarkDirty();
            Render();
        }
    }

    private Control BuildFrenzy()
    {
        var selected = _stage.Frenzy.ReservedColor ?? GhoulColor.Red;
        var color = StrategyUi.Choice(StrategyUi.Colors, selected, value => { selected = value; if (_stage.Frenzy.ReservedColor != null) { _stage.Frenzy.ReservedColor = value; _session.MarkDirty(); } }, 140);
        color.Width = 140;
        color.MaxWidth = 160;
        color.HorizontalAlignment = HorizontalAlignment.Left;
        color.IsVisible = _stage.Frenzy.ReservedColor != null;
        var reservedColor = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                StrategyUi.Check("保留指定颜色", _stage.Frenzy.ReservedColor != null, value => { _stage.Frenzy.ReservedColor = value ? selected : null; color.IsVisible = value; _session.MarkDirty(); }),
                color
            }
        };
        return StrategyUi.Row(
            StrategyUi.Check("乱舞期间使用奥义", _stage.Frenzy.AllowUltimates, value => { _stage.Frenzy.AllowUltimates = value; _session.MarkDirty(); }),
            StrategyUi.Check("乱舞期间使用炸弹", _stage.Frenzy.AllowBombs, value => { _stage.Frenzy.AllowBombs = value; _session.MarkDirty(); }),
            reservedColor);
    }

    private Control BuildTransitions()
    {
        var list = new StackPanel { Spacing = 10 };
        var add = StrategyUi.Button("添加转变规则", () => { var rule = new StageTransition(); _stage.Transitions.Add(rule); _openRules.Add(rule.Id); _session.MarkDirty(); Render(); });
        Render(); return StrategyUi.Column(list, add);
        void Render()
        {
            list.Children.Clear();
            foreach (var rule in _stage.Transitions)
            {
                var handle = StrategyUi.DragHandle();
                var target = StrategyUi.Choice(_session.Current.Stages.Select(x => new EditorChoice<string>(x.Id, x.Name)), rule.TargetStageId, value => { rule.TargetStageId = value; _session.MarkDirty(); });
                target.PlaceholderText = "选择目标阶段";
                var details = new StackPanel { IsVisible = _openRules.Contains(rule.Id) };
                details.Children.Add(new StrategyConditionEditor((ConditionGroup)rule.Condition, _session.MarkDirty));
                var edit = StrategyUi.Button("转变条件", () => { details.IsVisible = !details.IsVisible; if (details.IsVisible) _openRules.Add(rule.Id); else _openRules.Remove(rule.Id); });
                var remove = StrategyUi.Button("删除规则", () => { _stage.Transitions.Remove(rule); _session.MarkDirty(); Render(); });
                var row = new Border
                {
                    Child = StrategyUi.Column(StrategyUi.Row(handle, StrategyUi.Text("转到"), target, edit, remove), details),
                    Padding = new Thickness(0, 4, 0, 10),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                row.Bind(Border.BorderBrushProperty,
                    new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("SukiControlBorderBrush"));
                list.Children.Add(row);
                StrategyReorder.Attach(handle, row, list, _stage.Transitions, rule, _session.MarkDirty);
            }
        }
    }
}
