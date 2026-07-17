using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SendClient.Components;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------
// Services
// -------------------------------------------------------

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
    .SetApplicationName("SendClient");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
})
.AddOpenIdConnect(options =>
{
    options.Authority = builder.Configuration["Authentik:BaseUrl"]
                        + "/application/o/juna/";
    options.ClientId = builder.Configuration["Authentik:ClientId"];
    options.ClientSecret = builder.Configuration["Authentik:ClientSecret"];
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;
    options.CallbackPath = "/signin-oidc";

    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.NonceCookie.SameSite = SameSiteMode.None;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("groups");

    options.TokenValidationParameters = new()
    {
        NameClaimType = "name",
        RoleClaimType = "groups"
    };

    options.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = ctx =>
        {
            var identity = ctx.Principal?.Identity as ClaimsIdentity;
            if (identity == null) return Task.CompletedTask;

            var groups = ctx.Principal!
                .FindAll("groups")
                .ToList();

            foreach (var group in groups)
                identity.AddClaim(new Claim(ClaimTypes.Role, group.Value));

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanSend", policy =>
        policy.RequireRole("Test1"));
});

builder.Services.AddCascadingAuthenticationState();

// -------------------------------------------------------
// Pipeline
// -------------------------------------------------------

var app = builder.Build();

// Muss als allererstes stehen!
app.UseForwardedHeaders();
// Cloudflare Tunnel setzt CF-Visitor statt X-Forwarded-Proto
app.Use(async (context, next) =>
{
    var cfVisitor = context.Request.Headers["CF-Visitor"].ToString();
    if (cfVisitor.Contains("\"scheme\":\"https\""))
    {
        context.Request.Scheme = "https";
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Kein UseHttpsRedirection hinter Cloudflare Tunnel!
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/debug-proto", (HttpContext ctx) =>
    Results.Ok(new
    {
        scheme = ctx.Request.Scheme,
        host = ctx.Request.Host.ToString(),
        proto = ctx.Request.Headers["X-Forwarded-Proto"].ToString(),
        cfVisitor = ctx.Request.Headers["CF-Visitor"].ToString()
    }));

app.MapGet("/login", () =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = "/" },
        new[] { OpenIdConnectDefaults.AuthenticationScheme }
    ));

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = "/" });
});

app.Run();