using WinKeyCastr.Localization;
using Xunit;

namespace WinKeyCastr.Tests;

public class LocalizationTests
{
    [Fact]
    public void BothLanguagesResolveStrings()
    {
        Loc.Instance.SetLanguage(AppLanguage.English);
        Assert.Equal("Quit KeyCastr", Loc.T("Menu_Quit"));

        Loc.Instance.SetLanguage(AppLanguage.Russian);
        Assert.Equal("Выйти из KeyCastr", Loc.T("Menu_Quit"));

        Loc.Instance.SetLanguage(AppLanguage.English);
        Assert.Equal("General", Loc.T("Pane_General"));
    }

    [Fact]
    public void UnknownKeyFallsBackToKey() => Assert.Equal("No_Such_Key", Loc.T("No_Such_Key"));
}
