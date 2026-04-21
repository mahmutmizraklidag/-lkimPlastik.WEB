using ilkimPlastik.WEB;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
// 1. MVC ve Veritabanı Servisleri
builder.Services.AddControllersWithViews();
builder.Services.AddSqlServer<EfCoreContext>(builder.Configuration.GetConnectionString("dbConnection"));

// 2. Cookie Policy Yapılandırması (SameSite=None için kritik)
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.None; // Bankadan gelen POST verisi için şart
});

// 3. Session Yapılandırması
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    // Localhost'ta HTTPS kullanıyorsanız bunlar aktif olmalı:
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// 4. Diğer Yardımcı Servisler
builder.Services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();

// 5. Authentication (Kimlik Doğrulama)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.LogoutPath = "/Account/Logout";
        opt.AccessDeniedPath = "/Account/Login";

        opt.ExpireTimeSpan = TimeSpan.FromDays(365);
        opt.SlidingExpiration = true;

        opt.Cookie.HttpOnly = true;
        opt.Cookie.IsEssential = true;

        // Auth çerezi için de aynı SameSite politikasını uygulayalım
        opt.Cookie.SameSite = SameSiteMode.Lax;
        opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// =====================================================
// PIPELINE (SIRALAMA ÇOK ÖNEMLİ)
// =====================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 1. Önce Cookie Policy
app.UseCookiePolicy();

// 2. Sonra Session
app.UseSession();

// 3. Sonra Auth ve Authorization
app.UseAuthentication();
app.UseAuthorization();

// Route Yapılandırmaları
app.MapControllerRoute(
    name: "Admin",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();