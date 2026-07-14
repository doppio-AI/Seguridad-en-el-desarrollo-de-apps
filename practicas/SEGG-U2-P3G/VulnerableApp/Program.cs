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

    // Servicios
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

    // Pipeline HTTP
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ExceptionLoggingMiddleware>();

    app.UseHttpsRedirection();

    // Esta línea es la importante para que carguen CSS, JS, imágenes y Bootstrap
    app.UseStaticFiles();

    app.UseRouting();

    app.UseSession();

    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    Log.Information("VulnerableApp iniciada en el entorno {Environment}",
        app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "VulnerableApp terminó inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;