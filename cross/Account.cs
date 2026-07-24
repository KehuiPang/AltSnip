// 无为账号（基础版）：打开浏览器登录 + 读共享凭证 ~/.wuwei/auth.json + 查余额。
// token 文件与 wuwei-voice/wuwei-pro 三端共享（access_token/refresh_token/expires_at）。
// 客户端只跟官网通信：不接 Supabase、不持有任何 key。
// TODO(完整版)：本地回环 server 自动回写 token（方案同 wuwei-pro desktop/main/wuwei-auth.ts）。
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace WuweiShot;

internal static class Account
{
    public const string Site = "https://wuweiai.io";

    public static string AuthPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wuwei", "auth.json");

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public sealed record AuthInfo(string AccessToken, string RefreshToken, long ExpiresAt);
    public sealed record MeInfo(string Name, string Email, long Balance);

    /// <summary>读共享凭证；没有或不合法返回 null（= 未登录）。</summary>
    public static AuthInfo? ReadAuth()
    {
        try
        {
            if (!File.Exists(AuthPath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(AuthPath));
            var r = doc.RootElement;
            var at = r.TryGetProperty("access_token", out var a) ? a.GetString() : null;
            if (string.IsNullOrEmpty(at)) return null;
            var rt = r.TryGetProperty("refresh_token", out var t) ? t.GetString() ?? "" : "";
            var ea = r.TryGetProperty("expires_at", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 0;
            return new AuthInfo(at!, rt, ea);
        }
        catch { return null; }
    }

    public static bool IsLoggedIn => ReadAuth() != null;

    /// <summary>打开系统浏览器到官网登录页（基础版只开浏览器；登录完成后引导用户回托盘点刷新）。</summary>
    public static void OpenLogin()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = $"{Site}/auth/desktop",
            UseShellExecute = true,
        });
    }

    public static void Logout()
    {
        try { if (File.Exists(AuthPath)) File.Delete(AuthPath); } catch { }
    }

    /// <summary>查账号 + 无为币余额（官网 /api/me）；未登录/401 抛 InvalidOperationException。</summary>
    public static async Task<MeInfo> FetchMeAsync()
    {
        var auth = ReadAuth() ?? throw new InvalidOperationException("未登录");
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{Site}/api/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        using var resp = await Http.SendAsync(req).ConfigureAwait(false);
        if ((int)resp.StatusCode == 401) throw new InvalidOperationException("登录已过期，请重新登录");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
        var r = doc.RootElement;
        string name = "", email = "";
        if (r.TryGetProperty("user", out var u))
        {
            name = u.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            email = u.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
        }
        long bal = 0;
        if (r.TryGetProperty("coin", out var c) && c.TryGetProperty("balance", out var b) && b.ValueKind == JsonValueKind.Number)
            bal = b.GetInt64();
        return new MeInfo(name, email, bal);
    }
}
