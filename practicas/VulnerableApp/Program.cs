using Microsoft.EntityFrameworkCore;
using Serilog;
using VulnerableApp.Data;
using VulnerableApp.Middleware;
using VulnerableApp.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.WithProperty("Application", "VulnerableApp"));

    // Add services to the container.
    builder.Services.AddControllersWithViews();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSingleton<ICommentStore, InMemoryCommentStore>();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(20);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ExceptionLoggingMiddleware>();

    app.UseHttpsRedirection();
    app.UseRouting();

    app.UseSession();
    app.UseAuthorization();

    app.MapStaticAssets();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    Log.Information("VulnerableApp iniciada en el entorno {Environment}",
        app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "VulnerableApp termino inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
