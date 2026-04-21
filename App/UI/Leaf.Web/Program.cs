using Leaf.Runtime.Bootstrap;
using Leaf.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["LeafRuntime:Urls"] ?? "http://0.0.0.0:8080");

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "leaf.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromDays(30);
});

builder.Services.AddLeafRuntime(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

    var openPath = path.StartsWith("/auth/login")
                   || path.StartsWith("/css")
                   || path.StartsWith("/js")
                   || path.StartsWith("/assets")
                   || path == "/favicon.ico";

    if (openPath)
    {
        await next();
        return;
    }

    if (!context.Session.GetInt32(SessionKeys.UserId).HasValue)
    {
        var remember = context.Request.Cookies["leaf_remember_user"];
        if (int.TryParse(remember, out var rememberedUserId) && rememberedUserId > 0)
        {
            context.Session.SetInt32(SessionKeys.UserId, rememberedUserId);
        }
    }

    if (!context.Session.GetInt32(SessionKeys.UserId).HasValue)
    {
        context.Response.Redirect("/auth/login");
        return;
    }

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

public partial class Program;
