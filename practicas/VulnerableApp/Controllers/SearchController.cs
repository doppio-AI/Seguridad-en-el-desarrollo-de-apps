using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VulnerableApp.Data;
using VulnerableApp.Models;
using VulnerableApp.Security;

namespace VulnerableApp.Controllers
{
    public class SearchController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ILogger<SearchController> _logger;

        public SearchController(AppDbContext db, ILogger<SearchController> logger)
        {
            _db = db;
            _logger = logger;
        }

        public IActionResult Index(string search)
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation("Inicio Search.Index");

            _logger.LogInformation(
                "Usuario:{User} IP:{IP} Ruta:{Route} Parametro:{Search}",
                HttpContext.Session.GetString("User"),
                HttpContext.Connection.RemoteIpAddress,
                HttpContext.Request.Path,
                search);

            // Punto de prueba controlado para SEGG-U2-P3G-5: permite provocar una
            // excepción CONTROLADA (capturada y registrada aquí mismo, sin propagarse
            // al Exception Middleware) usando un valor de búsqueda reservado.
            if (search == "__test_controlled_error__")
            {
                try
                {
                    throw new InvalidOperationException("Excepción de prueba controlada (P3G-5)");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Excepción controlada capturada en Search.Index (prueba P3G-5)");
                    return View(new List<User>());
                }
            }

            if (SecurityPatternDetector.LooksLikeSqlInjection(search))
            {
                _logger.LogWarning(
                    "Posible intento de SQL Injection detectado en Search. Usuario:{User} IP:{IP} Valor:{Valor}",
                    HttpContext.Session.GetString("User"), HttpContext.Connection.RemoteIpAddress,
                    SecurityPatternDetector.SafeSnippet(search));
            }

            try
            {
                if (string.IsNullOrEmpty(search))
                {
                    _logger.LogWarning("Search.Index recibió un término de búsqueda vacío");
                    return View(new List<User>());
                }

                var users = _db.Users.Where(u => u.Username.Contains(search)).ToList();

                _logger.LogInformation("Search.Index resultados encontrados: {Cantidad}", users.Count);

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Search.Index. Parametro:{Search}", search);
                throw;
            }
            finally
            {
                sw.Stop();
                _logger.LogInformation("Fin Search.Index. DuracionMs:{DuracionMs}", sw.ElapsedMilliseconds);
            }
        }
    }
}