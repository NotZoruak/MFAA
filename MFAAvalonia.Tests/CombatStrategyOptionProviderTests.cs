using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Features.CombatStrategies;
using Xunit;

namespace MFAAvalonia.Tests;

public sealed class CombatStrategyOptionProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "maa-strategy-options", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateCases_IncludesBuiltinDefaultWithoutDefaultFile()
    {
        var cases = StrategyOptionProvider.CreateCases(_directory);
        var selected = Assert.Single(cases);
        Assert.Equal("default", selected.Name);
        Assert.Equal("默认策略", selected.Label);
        Assert.False(File.Exists(Path.Combine(_directory, "default.json")));
        Assert.Equal("default", ReadStrategyId(selected));
    }

    [Theory]
    [InlineData("zh-CN", "11111111111111111111111111111111", "22222222222222222222222222222222")]
    [InlineData("en-US", "22222222222222222222222222222222", "11111111111111111111111111111111")]
    public void CreateCases_EnumeratesValidFilesAndPassesStrategyIds(string culture, string firstId, string secondId)
    {
        using var scope = new CultureScope(culture);
        Directory.CreateDirectory(_directory);
        WriteStrategy("22222222222222222222222222222222", "夜战策略");
        WriteStrategy("11111111111111111111111111111111", "默认队伍策略");
        File.WriteAllText(Path.Combine(_directory, "default.json"), "{}", Encoding.UTF8);
        File.WriteAllText(Path.Combine(_directory, "33333333333333333333333333333333.json"), "{破损", Encoding.UTF8);

        var cases = StrategyOptionProvider.CreateCases(_directory);

        Assert.Equal(new[] { "default", firstId, secondId }, cases.Select(item => item.Name));
        var defaultTeam = Assert.Single(cases, item => item.Name == "11111111111111111111111111111111");
        Assert.Equal("默认队伍策略", defaultTeam.Label);
        Assert.Equal("11111111111111111111111111111111", ReadStrategyId(defaultTeam));
        var nightTeam = Assert.Single(cases, item => item.Name == "22222222222222222222222222222222");
        Assert.Equal("夜战策略", nightTeam.Label);
        Assert.Equal("22222222222222222222222222222222", ReadStrategyId(nightTeam));
    }

    [Fact]
    public void SelectedStrategy_IsRestoredByIdWhenCaseOrderChanges()
    {
        Directory.CreateDirectory(_directory);
        WriteStrategy("22222222222222222222222222222222", "夜战策略");
        WriteStrategy("11111111111111111111111111111111", "默认队伍策略");
        var option = new MaaInterface.MaaInterfaceSelectOption { Name = StrategyOptionProvider.OptionName, Index = 1 };
        StrategyOptionProvider.RememberSelectedCase(option, "11111111111111111111111111111111");
        var definition = new MaaInterface.MaaInterfaceOption
        {
            Cases = StrategyOptionProvider.CreateCases(_directory)
                .OrderBy(item => item.Name == "default" ? 0 : 1)
                .ThenByDescending(item => item.Name)
                .ToList()
        };

        Assert.True(StrategyOptionProvider.RestoreSelectedCase(definition, option));
        Assert.Equal(2, option.Index);
    }

    private void WriteStrategy(string id, string name)
    {
        var strategy = StrategyDefaults.CreateEditable(name);
        strategy.Id = id;
        File.WriteAllText(Path.Combine(_directory, id + ".json"), StrategyJson.Serialize(strategy), Encoding.UTF8);
    }

    private static string? ReadStrategyId(MaaInterface.MaaInterfaceOptionCase item) =>
        item.PipelineOverride?["RB_RunBattle"]?["action"]?["param"]?["custom_action_param"]?["strategy_id"]?.ToString();

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _original = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        public void Dispose() => CultureInfo.CurrentCulture = _original;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
