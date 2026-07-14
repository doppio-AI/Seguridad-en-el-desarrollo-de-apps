using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace VulnerableApp.Controllers;

public abstract class InstrumentedController<TController> : Controller
{
    protected InstrumentedController(ILogger<TController> logger)
    {
        Logger = logger;
    }

    protected ILogger<TController> Logger { get; }

    protected IActionResult ExecuteLogged(
        string actionName,
        object? safeParameters,
        Func<IActionResult> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var controllerName = typeof(TController).Name;
        var user = GetCurrentUser();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        var outcome = "Excepcion";

        Logger.LogInformation(
            "Inicio {Controller}.{Action} | Usuario: {User} | IP: {IP} | Parametros: {@Parameters}",
            controllerName,
            actionName,
            user,
            ipAddress,
            safeParameters);

        try
        {
            var result = action();
            outcome = result.GetType().Name;
            return result;
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "Error en {Controller}.{Action} | Usuario: {User} | IP: {IP} | DuracionMs: {ElapsedMs}",
                controllerName,
                actionName,
                user,
                ipAddress,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            Logger.LogInformation(
                "Fin {Controller}.{Action} | Resultado: {Outcome} | DuracionMs: {ElapsedMs}",
                controllerName,
                actionName,
                outcome,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private string GetCurrentUser()
    {
        var session = HttpContext.Features.Get<ISessionFeature>()?.Session;
        return session?.GetString("User")
            ?? User.Identity?.Name
            ?? "Anonimo";
    }
}
