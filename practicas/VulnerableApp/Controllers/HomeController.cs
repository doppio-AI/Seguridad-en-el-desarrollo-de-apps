using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VulnerableApp.Models;

namespace VulnerableApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    private string? CurrentUser => HttpContext.Session.GetString("User");
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    public IActionResult Index()
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Inicio Home.Index. Usuario:{User} IP:{IP}", CurrentUser, ClientIp);

        var result = View();

        sw.Stop();
        _logger.LogInformation("Fin Home.Index. DuracionMs:{DuracionMs}", sw.ElapsedMilliseconds);
        return result;
    }

    public IActionResult Privacy()
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Inicio Home.Privacy. Usuario:{User} IP:{IP}", CurrentUser, ClientIp);

        var result = View();

        sw.Stop();
        _logger.LogInformation("Fin Home.Privacy. DuracionMs:{DuracionMs}", sw.ElapsedMilliseconds);
        return result;
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var sw = Stopwatch.StartNew();
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        _logger.LogWarning(
            "Home.Error visualizado. RequestId:{RequestId} Usuario:{User} IP:{IP}",
            requestId, CurrentUser, ClientIp);

        var result = View(new ErrorViewModel { RequestId = requestId });

        sw.Stop();
        _logger.LogInformation("Fin Home.Error. DuracionMs:{DuracionMs}", sw.ElapsedMilliseconds);
        return result;
    }
}
