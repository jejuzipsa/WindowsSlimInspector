using Microsoft.Win32;
using WindowsSlimInspector.Models;

namespace WindowsSlimInspector.Services;

public sealed class WindowsSlimService
{
    private sealed record RegSetting(string Id, string Name, string Description, RegistryHive Hive, string SubKey, string ValueName, object DisabledValue, object EnabledValue);

    private readonly List<RegSetting> _settings =
    [
        new("ads", "광고 / 추천", "Windows 추천 및 프로모션 콘텐츠를 줄입니다.", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, 1),
        new("consumer", "Consumer Experience", "추천 앱 및 소비자 환경 콘텐츠를 비활성화합니다.", RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1, 0),
        new("tips", "Tips / Welcome Experience", "Windows 팁과 환영 환경 노출을 줄입니다.", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0, 1),
        new("bing", "Bing 웹 검색", "시작 메뉴 검색의 웹 결과를 끄고 로컬 검색은 유지합니다.", RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, 0),
        new("searchhighlights", "Search Highlights", "검색창의 온라인 하이라이트 콘텐츠를 끕니다.", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0, 1),
        new("copilot", "Copilot", "Windows Copilot 정책 노출을 비활성화합니다.", RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, 0),
        new("adid", "광고 ID / 맞춤 콘텐츠", "광고 ID 기반 개인화 사용을 제한합니다.", RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1)
    ];

    public IReadOnlyList<FeatureItem> CreateFeatureItems()
    {
        var list = _settings.Select(s => new FeatureItem { Id = s.Id, Name = s.Name, Description = s.Description }).ToList();
        list.Insert(5, new FeatureItem { Id = "widgets", Name = "Widgets / News", Description = "Windows 위젯 및 뉴스 피드를 비활성화합니다." });
        list.Add(new FeatureItem { Id = "phonelink", Name = "Phone Link", Description = "Phone Link 자동 시작/백그라운드 사용을 관리합니다." });
        list.Add(new FeatureItem { Id = "teams", Name = "Teams / Chat", Description = "개인용 Teams/Chat 자동 시작을 관리합니다." });
        return list;
    }

    public Task RefreshAsync(IEnumerable<FeatureItem> items)
    {
        foreach (var item in items)
        {
            try
            {
                if (item.Id == "widgets")
                {
                    item.State = IsWidgetsDisabled() ? FeatureState.Disabled : FeatureState.Enabled;
                    item.Detail = "Widgets policy/taskbar state";
                    continue;
                }

                var setting = _settings.FirstOrDefault(x => x.Id == item.Id);
                if (setting is not null)
                {
                    item.State = ReadSetting(setting) ? FeatureState.Disabled : FeatureState.Enabled;
                    item.Detail = "Registry policy/state";
                    continue;
                }

                item.State = item.Id switch
                {
                    "phonelink" => IsRunEntryPresent("PhoneExperienceHost") ? FeatureState.Enabled : FeatureState.Disabled,
                    "teams" => IsTeamsStartupPresent() ? FeatureState.Enabled : FeatureState.Disabled,
                    _ => FeatureState.Unsupported
                };
            }
            catch (Exception ex) { item.State = FeatureState.Error; item.Detail = ex.Message; }
        }
        return Task.CompletedTask;
    }

    public async Task ApplyAsync(IEnumerable<FeatureItem> items, bool disable, Action<string> log)
    {
        foreach (var item in items.Where(x => x.IsSelected))
        {
            try
            {
                if (item.Id == "widgets")
                {
                    SetWidgets(disable);
                    var verified = IsWidgetsDisabled() == disable;
                    log($"{item.Name}: {(verified ? (disable ? "Disabled" : "Restored") : "ERROR - state verification failed")}");
                    if (!verified) { item.State = FeatureState.Error; item.Detail = "State verification failed"; }
                    continue;
                }

                var setting = _settings.FirstOrDefault(x => x.Id == item.Id);
                if (setting is not null) { WriteSetting(setting, disable); log($"{item.Name}: {(disable ? "Disabled" : "Restored")}"); }
                else if (item.Id == "phonelink") { SetRunEntry("PhoneExperienceHost", disable); log($"{item.Name}: startup {(disable ? "disabled" : "restored when available")}"); }
                else if (item.Id == "teams") { SetTeamsStartup(disable); log($"{item.Name}: startup {(disable ? "disabled" : "restored when available")}"); }
            }
            catch (Exception ex) { item.State = FeatureState.Error; item.Detail = ex.Message; log($"{item.Name}: ERROR - {ex.Message}"); }
        }
        await RefreshAsync(items);
    }

    private static bool IsWidgetsDisabled()
    {
        var machinePolicy = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests");
        if (machinePolicy == 0) return true;
        var taskbar = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa");
        return taskbar == 0;
    }

    private static void SetWidgets(bool disable)
    {
        // TaskbarDa is per-user and hides the Widgets entry point on Windows 11.
        WriteDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", disable ? 0 : 1);

        // Microsoft-supported device policy. Some systems protect this policy key; the
        // per-user state above remains a safe fallback rather than failing the whole action.
        try { WriteDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", disable ? 0 : 1); }
        catch (UnauthorizedAccessException) { }
    }

    private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(subKey, false);
        var value = key?.GetValue(valueName);
        return value is null ? null : Convert.ToInt32(value);
    }

    private static void WriteDword(RegistryHive hive, string subKey, string valueName, int value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(subKey, true) ?? throw new InvalidOperationException("Registry key create failed");
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }

    private static bool ReadSetting(RegSetting setting)
    {
        using var baseKey = RegistryKey.OpenBaseKey(setting.Hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(setting.SubKey, false);
        var value = key?.GetValue(setting.ValueName);
        return value is not null && Equals(Convert.ToInt32(value), Convert.ToInt32(setting.DisabledValue));
    }

    private static void WriteSetting(RegSetting setting, bool disable)
    {
        using var baseKey = RegistryKey.OpenBaseKey(setting.Hive, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(setting.SubKey, true) ?? throw new InvalidOperationException("Registry key create failed");
        key.SetValue(setting.ValueName, disable ? setting.DisabledValue : setting.EnabledValue, RegistryValueKind.DWord);
    }

    private static bool IsRunEntryPresent(string name) { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue(name) is not null; }
    private static void SetRunEntry(string name, bool disable) { using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true); if (disable) key?.DeleteValue(name, false); }
    private static bool IsTeamsStartupPresent() { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValueNames().Any(n => n.Contains("Teams", StringComparison.OrdinalIgnoreCase)) == true; }
    private static void SetTeamsStartup(bool disable) { if (!disable) return; using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true); if (key is null) return; foreach (var name in key.GetValueNames().Where(n => n.Contains("Teams", StringComparison.OrdinalIgnoreCase)).ToArray()) key.DeleteValue(name, false); }
}
