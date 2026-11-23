using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Tests;

[Collection(PostgresCollection.Name)]
public sealed class AccountFlowTests
{
    [PostgresFact]
    public async Task ParallelLogins_KeepUniqueAuditEntries_AndProfileUpdatesOnlyCurrentCustomer()
    {
        await using var host = await PostgresTestHost.CreateAsync(useCookieAuth: true);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(PostgresTestConnection.Require()).Options);
        const string password = "Example-Profile-001";
        var account = new TaiKhoan
        {
            TenDangNhap = "test-" + Guid.NewGuid().ToString("N"),
            Email = host.Data.CustomerAEmail,
            HoTen = "Khách gốc",
            MatKhauHash = PasswordHelper.Hash(password),
            VaiTro = VaiTro.KhachHang
        };
        db.TaiKhoans.Add(account);
        await db.SaveChangesAsync();

        using var first = host.CreateClient();
        using var second = host.CreateClient();
        using var third = host.CreateClient();
        using var guest = host.CreateClient();
        using var guestProfile = await guest.GetAsync("/ho-so");
        Assert.Equal(HttpStatusCode.Redirect, guestProfile.StatusCode);
        var logins = await Task.WhenAll(LoginAsync(first, account.Email, password),
            LoginAsync(second, account.Email, password), LoginAsync(third, account.Email, password));
        foreach (var response in logins)
        {
            using (response) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        var codes = await db.NhatKyHoatDongs.Where(a => a.TaiKhoanId == account.Id && a.HanhDong == "Đăng nhập")
            .Select(a => a.MaNhatKy).ToListAsync();
        Assert.Equal(3, codes.Distinct().Count());
        await db.Entry(account).ReloadAsync();
        Assert.NotNull(account.LanDangNhapCuoi);
        Assert.Equal(host.Data.CustomerAId, account.KhachHangId);

        using var profile = await SubmitAsync(first, "/ho-so", "/ho-so", new()
        {
            ["FullName"] = "Khách Đã Cập Nhật", ["Phone"] = "+84912345678", ["Email"] = host.Data.CustomerBEmail,
            ["Id"] = "1", ["VaiTro"] = "Admin"
        });
        Assert.Equal("/ho-so", profile.Headers.Location?.OriginalString);
        Assert.Contains("Đã lưu hồ sơ", WebUtility.HtmlDecode(await first.GetStringAsync("/ho-so")));
        Assert.Contains("Khách Đã Cập Nhật", WebUtility.HtmlDecode(await second.GetStringAsync("/ho-so")));
        await db.Entry(account).ReloadAsync();
        Assert.Equal("+84912345678", account.SoDienThoai);
        Assert.Equal(host.Data.CustomerAEmail, account.Email);
        Assert.Equal(VaiTro.KhachHang, account.VaiTro);
        Assert.Equal(host.Data.CustomerAId, account.KhachHangId);
        Assert.Equal("0900000001", await db.DatVes.Where(b => b.MaDatVe == host.Data.PnrA).Select(b => b.SoDienThoaiLienHe).SingleAsync());
    }

    [PostgresFact]
    public async Task UsernameRegistration_RequiresProfile_AndDoesNotClaimBookingsByContactGmail()
    {
        await using var host = await PostgresTestHost.CreateAsync(useCookieAuth: true);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(PostgresTestConnection.Require()).Options);
        using var customer = host.CreateClient();
        var username = "onboarding-" + Guid.NewGuid().ToString("N");
        const string password = "Example-Password-001";
        const string gmail = "onboarding.fixture@gmail.com";
        var existing = await db.KhachHangs.SingleAsync(x => x.Id == host.Data.CustomerAId);
        existing.Email = gmail;
        await db.SaveChangesAsync();

        using var register = await SubmitAsync(customer, "/dang-ky", "/dang-ky",
            new() { ["Username"] = username, ["Password"] = password, ["VaiTro"] = "Admin" });
        Assert.Equal("/dang-nhap", register.Headers.Location?.OriginalString);
        Assert.False(Directory.Exists(host.OutboxPath));
        using var login = await LoginAsync(customer, username.ToUpperInvariant(), password);
        Assert.Equal("/ho-so", login.Headers.Location?.OriginalString);
        using var blocked = await customer.GetAsync("/ve-cua-toi");
        Assert.StartsWith("/ho-so?", blocked.Headers.Location?.OriginalString);
        using var invalid = await SubmitAsync(customer, "/ho-so", "/ho-so",
            new() { ["FullName"] = "Khách mới", ["Email"] = gmail });
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        using var saved = await SubmitAsync(customer, "/ho-so", "/ho-so",
            new() { ["FullName"] = "Khách mới", ["Phone"] = "0900000010", ["Email"] = gmail });
        Assert.Equal("/", saved.Headers.Location?.OriginalString);
        var account = await db.TaiKhoans.AsNoTracking().SingleAsync(x => x.TenDangNhap == username);
        Assert.Equal(VaiTro.KhachHang, account.VaiTro);
        Assert.NotEqual(password, account.MatKhauHash);
        Assert.Equal(gmail, account.GmailLienHe);
        Assert.NotNull(account.KhachHangId);
        Assert.NotEqual(existing.Id, account.KhachHangId);
        Assert.DoesNotContain(host.Data.PnrA, await customer.GetStringAsync("/ve-cua-toi"));
    }

    [PostgresFact]
    public async Task LegacyAccountReset_UpdatesLoginAndRevokesExistingSession()
    {
        await using var host = await PostgresTestHost.CreateAsync(useCookieAuth: true);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(PostgresTestConnection.Require()).Options);
        using var customer = host.CreateClient();
        using var recovery = host.CreateClient();
        var email = host.Data.CustomerAEmail;
        const string firstPassword = "Example-Password-001";
        const string newPassword = "Example-Password-002";

        db.TaiKhoans.Add(new TaiKhoan
        {
            TenDangNhap = "reset-" + Guid.NewGuid().ToString("N"), Email = email,
            HoTen = "Khách Kiểm Thử", MatKhauHash = PasswordHelper.Hash(firstPassword)
        });
        await db.SaveChangesAsync();

        using var login = await LoginAsync(customer, email, firstPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var tickets = await customer.GetStringAsync("/ve-cua-toi");
        Assert.Contains(host.Data.PnrA, tickets);
        Assert.DoesNotContain(host.Data.PnrB, tickets);

        using var resetRequest = await SubmitAsync(recovery, "/quen-mat-khau", "/quen-mat-khau", new() { ["Email"] = email });
        Assert.Equal(HttpStatusCode.OK, resetRequest.StatusCode);
        var resetLink = await ReadEmailLinkAsync(host.OutboxPath, "/dat-lai-mat-khau");
        using var resetForm = await recovery.GetAsync(resetLink.PathAndQuery);
        Assert.Equal("no-referrer", Assert.Single(resetForm.Headers.GetValues("Referrer-Policy")));
        var resetFields = new Dictionary<string, string>
        {
            ["Token"] = QueryHelpers.ParseQuery(resetLink.Query)["token"].ToString(),
            ["Password"] = newPassword, ["ConfirmPassword"] = newPassword
        };
        using var reset = await SubmitAsync(recovery, resetLink.PathAndQuery, "/dat-lai-mat-khau", resetFields);
        Assert.Equal("/dang-nhap", reset.Headers.Location?.OriginalString);
        using var revoked = await customer.GetAsync("/ve-cua-toi");
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        using var replay = await recovery.PostAsync("/dat-lai-mat-khau", new FormUrlEncodedContent(resetFields));
        Assert.Contains("đã sử dụng", WebUtility.HtmlDecode(await replay.Content.ReadAsStringAsync()));
        using var oldLogin = await LoginAsync(recovery, email, firstPassword);
        Assert.Contains("mật khẩu không đúng", WebUtility.HtmlDecode(await oldLogin.Content.ReadAsStringAsync()));
        using var newLogin = await LoginAsync(recovery, email, newPassword);
        Assert.Equal(HttpStatusCode.Redirect, newLogin.StatusCode);
    }

    [PostgresFact]
    public async Task UnknownEmailGetsGenericRecoveryPageWithoutEmail()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        using var client = host.CreateClient();
        using var response = await SubmitAsync(client, "/quen-mat-khau", "/quen-mat-khau",
            new() { ["Email"] = "unregistered@example.test" });
        Assert.Contains("Nếu email đủ điều kiện", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.False(Directory.Exists(host.OutboxPath));
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        SubmitAsync(client, "/dang-nhap", "/dang-nhap", new() { ["Email"] = email, ["Password"] = password });

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string formPath, string target,
        Dictionary<string, string> fields)
    {
        string html = await client.GetStringAsync(formPath);
        string token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        fields["__RequestVerificationToken"] = token;
        return await client.PostAsync(target, new FormUrlEncodedContent(fields));
    }

    private static async Task<Uri> ReadEmailLinkAsync(string outbox, string route)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        while (!deadline.IsCancellationRequested)
        {
            foreach (var path in Directory.Exists(outbox) ? Directory.GetFiles(outbox, "*.eml") : [])
            {
                var mime = File.ReadAllText(path);
                var part = Regex.Match(mime, "Content-Type: text/html; charset=utf-8\\r\\nContent-Transfer-Encoding: base64\\r\\n\\r\\n([A-Za-z0-9+/=\\r\\n]+)\\r\\n--");
                if (!part.Success) continue;
                var html = Encoding.UTF8.GetString(Convert.FromBase64String(part.Groups[1].Value));
                var link = Regex.Match(html, "href=\"([^\"]+)\"").Groups[1].Value;
                if (Uri.TryCreate(WebUtility.HtmlDecode(link), UriKind.Absolute, out var uri) && uri.AbsolutePath == route)
                    return uri;
            }
            await Task.Delay(150);
        }
        throw new InvalidOperationException($"Missing outbox email for {route}.");
    }
}
