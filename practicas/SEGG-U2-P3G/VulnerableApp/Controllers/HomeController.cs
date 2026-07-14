using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VulnerableApp.Models;

namespace VulnerableApp.Controllers;

public class HomeController : InstrumentedController<HomeController>
{
    public HomeController(ILogger<HomeController> logger) : base(logger)
    {
    }

    public IActionResult Index()
    {
        return ExecuteLogged(nameof(Index), safeParameters: null, View);
    }

    public IActionResult Privacy()
    {
        return ExecuteLogged(nameof(Privacy), safeParameters: null, View);
    }

    [HttpGet]
    public IActionResult ControlledException()
    {
        return ExecuteLogged(nameof(ControlledException), safeParameters: null, () =>
        {
            try
            {
                throw new InvalidOperationException(
                    "Excepcion controlada generada para la practica P3G.");
            }
            catch (InvalidOperationException exception)
            {
                Logger.LogWarning(
                    exception,
                    "Excepcion controlada atendida por HomeController");
                return UnprocessableEntity(new
                {
                    message = "Excepcion controlada",
                    correlationId = HttpContext.TraceIdentifier
                });
            }
        });
    }

    [HttpGet]
    public IActionResult UnhandledException()
    {
        return ExecuteLogged(nameof(UnhandledException), safeParameters: null, () =>
            throw new InvalidOperationException(
                "Excepcion no controlada generada para validar el middleware global."));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return ExecuteLogged(nameof(Error), safeParameters: null, () =>
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            Logger.LogError("Se mostro la pagina de error para la solicitud {RequestId}", requestId);
            return View(new ErrorViewModel { RequestId = requestId });
        });
    }
}
