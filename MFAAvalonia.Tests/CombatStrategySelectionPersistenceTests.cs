using Avalonia.Controls;
using Avalonia.LogicalTree;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Features.CombatStrategies;
using MFAAvalonia.Helper;
using MFAAvalonia.Helper.Converters;
using Newtonsoft.Json;
using MFAAvalonia.ViewModels.Windows;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace MFAAvalonia.Tests;

[Collection("策略界面")]
public sealed class CombatStrategySelectionPersistenceTests : IDisposable
{
    private readonly ConcurrentDictionary<Type, Lazy<object>> _services =
        (ConcurrentDictionary<Type, Lazy<object>>)typeof(Instances)
            .GetField("ServiceCache", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    private readonly Lazy<object>? _previousRoot;

    public CombatStrategySelectionPersistenceTests()
    {
        // 为真实选项控件提供空闲状态，不启动主窗口的其它服务。
        _services.TryGetValue(typeof(RootViewModel), out _previousRoot);
        _services[typeof(RootViewModel)] = new Lazy<object>(() => new TestRootViewModel());
    }

    [Theory(DisplayName = "实际选项面板保存策略标识，重启及目录重排后恢复同一策略")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ComboBox_选择保存再加载后保留策略(int selectedIndex)
    {
        var definition = CreateDefinition();
        var option = new MaaInterface.MaaInterfaceSelectOption
        {
            Name = StrategyOptionProvider.OptionName,
            Index = selectedIndex == 0 ? 2 : 0
        };
        string? saved = null;
        var generator = new TaskOptionGenerator(null!, () =>
            saved = JsonConvert.SerializeObject(new List<MaaInterface.MaaInterfaceSelectOption> { option },
                new MaaInterfaceSelectOptionConverter(false)));
        var combo = CreateCombo(generator, definition, option);

        combo.SelectedIndex = selectedIndex;

        Assert.NotNull(saved);
        var restored = Assert.Single(JsonConvert.DeserializeObject<List<MaaInterface.MaaInterfaceSelectOption>>(saved!)!);
        var selectedId = definition.Cases![selectedIndex].Name;
        Assert.Equal(selectedId, restored.Data?[StrategyOptionProvider.SelectedCaseDataKey]);
        Assert.NotSame(option, restored);

        // 模拟退出后重建目录，策略的展示顺序与原来不同。
        var reloadedDefinition = CreateDefinition();
        reloadedDefinition.Cases = reloadedDefinition.Cases!.AsEnumerable().Reverse().ToList();
        TaskLoader.SetDefaultOptionValue(new MaaInterface
        {
            Option = new() { [StrategyOptionProvider.OptionName] = reloadedDefinition }
        }, restored);

        Assert.Equal(selectedId, reloadedDefinition.Cases[restored.Index!.Value].Name);
    }

    [Fact(DisplayName = "普通下拉选项保存索引但不写入策略标识")]
    public void ComboBox_普通选项不写入策略元数据()
    {
        var definition = CreateDefinition();
        var option = new MaaInterface.MaaInterfaceSelectOption { Name = "选择部队", Index = 0 };
        var saved = false;
        var combo = CreateCombo(new TaskOptionGenerator(null!, () => saved = true), definition, option);

        combo.SelectedIndex = 1;

        Assert.True(saved);
        Assert.Equal(1, option.Index);
        Assert.Null(option.Data);
    }

    private static ComboBox CreateCombo(TaskOptionGenerator generator,
        MaaInterface.MaaInterfaceOption definition, MaaInterface.MaaInterfaceSelectOption option)
    {
        var method = typeof(TaskOptionGenerator).GetMethod("CreateComboBoxControl", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var panel = (Control)method.Invoke(generator, [option, definition, null, false])!;
        return panel.GetLogicalDescendants().OfType<ComboBox>().Single();
    }

    private static MaaInterface.MaaInterfaceOption CreateDefinition() => new()
    {
        Type = "select",
        DefaultCases = ["default"],
        Cases =
        [
            new() { Name = "default", Label = "默认策略" },
            new() { Name = "11111111111111111111111111111111", Label = "第一策略" },
            new() { Name = "22222222222222222222222222222222", Label = "第二策略" }
        ]
    };

    public void Dispose()
    {
        if (_previousRoot != null)
            _services[typeof(RootViewModel)] = _previousRoot;
        else
            _services.TryRemove(typeof(RootViewModel), out _);
    }

    private sealed class TestRootViewModel : RootViewModel
    {
        protected override void Initialize() { }
    }
}
