using System.Text.Json;

namespace IbkrConduit.Streaming.Mappers;

/// <summary>Maps an <c>act</c> WebSocket frame to an <see cref="AccountStatusEvent"/>, including the nested <see cref="AccountProperties"/>, <see cref="AccountFeatures"/>, and <see cref="StreamingServerInfo"/> sub-records.</summary>
internal static class AccountStatusMapper
{
    public static AccountStatusEvent Map(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var args))
        {
            return new AccountStatusEvent();
        }

        return new AccountStatusEvent
        {
            Accounts = ReadStringList(args, "accounts"),
            AcctProps = ReadAcctProps(args),
            Aliases = ReadStringDict(args, "aliases"),
            AllowFeatures = args.TryGetProperty("allowFeatures", out var feats)
                ? MapAccountFeatures(feats)
                : null,
            ChartPeriods = ReadChartPeriods(args),
            Groups = ReadStringList(args, "groups"),
            Profiles = ReadStringList(args, "profiles"),
            SelectedAccount = args.TryGetProperty("selectedAccount", out var sa) ? sa.GetString() ?? string.Empty : string.Empty,
            ServerInfo = args.TryGetProperty("serverInfo", out var si) ? MapServerInfo(si) : null,
            SessionId = args.TryGetProperty("sessionId", out var sid) ? sid.GetString() ?? string.Empty : string.Empty,
            IsFT = args.TryGetProperty("isFT", out var ft) && ft.ValueKind == JsonValueKind.True,
            IsPaper = args.TryGetProperty("isPaper", out var ip) && ip.ValueKind == JsonValueKind.True,
        };
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        var list = new List<string>(arr.GetArrayLength());
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString()!);
            }
        }
        return list;
    }

    private static Dictionary<string, string> ReadStringDict(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>();
        }
        var dict = new Dictionary<string, string>();
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                dict[prop.Name] = prop.Value.GetString()!;
            }
        }
        return dict;
    }

    private static Dictionary<string, AccountProperties> ReadAcctProps(JsonElement parent)
    {
        if (!parent.TryGetProperty("acctProps", out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, AccountProperties>();
        }
        var dict = new Dictionary<string, AccountProperties>();
        foreach (var prop in obj.EnumerateObject())
        {
            dict[prop.Name] = MapAccountProperties(prop.Value);
        }
        return dict;
    }

    private static AccountProperties MapAccountProperties(JsonElement el) =>
        new()
        {
            HasChildAccounts = el.TryGetProperty("hasChildAccounts", out var h) && h.ValueKind == JsonValueKind.True,
            SupportsCashQty = el.TryGetProperty("supportsCashQty", out var sc) && sc.ValueKind == JsonValueKind.True,
            NoFXConv = el.TryGetProperty("noFXConv", out var nf) && nf.ValueKind == JsonValueKind.True,
            IsProp = el.TryGetProperty("isProp", out var ip) && ip.ValueKind == JsonValueKind.True,
            SupportsFractions = el.TryGetProperty("supportsFractions", out var sf) && sf.ValueKind == JsonValueKind.True,
            AllowCustomerTime = el.TryGetProperty("allowCustomerTime", out var ac) && ac.ValueKind == JsonValueKind.True,
            LiteUnderPro = el.TryGetProperty("liteUnderPro", out var lup) && lup.ValueKind == JsonValueKind.True,
            AutoFx = el.TryGetProperty("autoFx", out var afx) && afx.ValueKind == JsonValueKind.True,
        };

    private static AccountFeatures MapAccountFeatures(JsonElement el) =>
        new()
        {
            ShowGFIS = el.TryGetProperty("showGFIS", out var v1) && v1.ValueKind == JsonValueKind.True,
            ShowEUCostReport = el.TryGetProperty("showEUCostReport", out var v2) && v2.ValueKind == JsonValueKind.True,
            AllowEventContract = el.TryGetProperty("allowEventContract", out var v3) && v3.ValueKind == JsonValueKind.True,
            AllowFXConv = el.TryGetProperty("allowFXConv", out var v4) && v4.ValueKind == JsonValueKind.True,
            AllowFinancialLens = el.TryGetProperty("allowFinancialLens", out var v5) && v5.ValueKind == JsonValueKind.True,
            AllowMTA = el.TryGetProperty("allowMTA", out var v6) && v6.ValueKind == JsonValueKind.True,
            AllowTypeAhead = el.TryGetProperty("allowTypeAhead", out var v7) && v7.ValueKind == JsonValueKind.True,
            AllowEventTrading = el.TryGetProperty("allowEventTrading", out var v8) && v8.ValueKind == JsonValueKind.True,
            SnapshotRefreshTimeout = el.TryGetProperty("snapshotRefreshTimeout", out var srt) && srt.ValueKind == JsonValueKind.Number ? srt.GetInt32() : null,
            LiteUser = el.TryGetProperty("liteUser", out var v10) && v10.ValueKind == JsonValueKind.True,
            ShowWebNews = el.TryGetProperty("showWebNews", out var v11) && v11.ValueKind == JsonValueKind.True,
            Research = el.TryGetProperty("research", out var v12) && v12.ValueKind == JsonValueKind.True,
            DebugPnl = el.TryGetProperty("debugPnl", out var v13) && v13.ValueKind == JsonValueKind.True,
            ShowTaxOpt = el.TryGetProperty("showTaxOpt", out var v14) && v14.ValueKind == JsonValueKind.True,
            ShowImpactDashboard = el.TryGetProperty("showImpactDashboard", out var v15) && v15.ValueKind == JsonValueKind.True,
            AllowDynAccount = el.TryGetProperty("allowDynAccount", out var v16) && v16.ValueKind == JsonValueKind.True,
            AllowCrypto = el.TryGetProperty("allowCrypto", out var v17) && v17.ValueKind == JsonValueKind.True,
            AllowFA = el.TryGetProperty("allowFA", out var v18) && v18.ValueKind == JsonValueKind.True,
            AllowLiteUnderPro = el.TryGetProperty("allowLiteUnderPro", out var v19) && v19.ValueKind == JsonValueKind.True,
            AllowedAssetTypes = el.TryGetProperty("allowedAssetTypes", out var aat) ? aat.GetString() : null,
            RestrictTradeSubscription = el.TryGetProperty("restrictTradeSubscription", out var v20) && v20.ValueKind == JsonValueKind.True,
            ShowUkUserLabels = el.TryGetProperty("showUkUserLabels", out var v21) && v21.ValueKind == JsonValueKind.True,
            SideBySide = el.TryGetProperty("sideBySide", out var v22) && v22.ValueKind == JsonValueKind.True,
        };

    private static Dictionary<string, IReadOnlyList<string>> ReadChartPeriods(JsonElement parent)
    {
        if (!parent.TryGetProperty("chartPeriods", out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }
        var dict = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            var list = new List<string>(prop.Value.GetArrayLength());
            foreach (var item in prop.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    list.Add(item.GetString()!);
                }
            }
            dict[prop.Name] = list;
        }
        return dict;
    }

    private static StreamingServerInfo MapServerInfo(JsonElement el) =>
        new()
        {
            ServerName = el.TryGetProperty("serverName", out var sn) ? sn.GetString() ?? string.Empty : string.Empty,
            ServerVersion = el.TryGetProperty("serverVersion", out var sv) ? sv.GetString() ?? string.Empty : string.Empty,
        };
}
