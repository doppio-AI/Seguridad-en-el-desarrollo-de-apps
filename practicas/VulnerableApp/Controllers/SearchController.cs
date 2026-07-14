using Microsoft.AspNetCore.Mvc;
using VulnerableApp.Data;
using VulnerableApp.Models;
using VulnerableApp.Security;

namespace VulnerableApp.Controllers
{
    public class SearchController : InstrumentedController<SearchController>
    {
        private readonly AppDbContext _db;

        public SearchController(AppDbContext db, ILogger<SearchController> logger) : base(logger)
        {
            _db = db;
        }

        public IActionResult Index(string search)
        {
            var safeSearch = SecurityPatternDetector.SanitizeForLog(search);
            return ExecuteLogged(nameof(Index), new { Search = safeSearch }, () =>
            {
                if (string.IsNullOrWhiteSpace(search))
                {
                    Logger.LogWarning("Busqueda vacia o sin criterio");
                    return View(new List<User>());
                }

                var normalizedSearch = search.Trim();
                if (SecurityPatternDetector.LooksLikeSqlInjection(normalizedSearch))
                {
                    Logger.LogWarning(
                        "Posible intento de SQL Injection detectado | Patron: {SearchPattern}",
                        safeSearch);
                }

                var users = _db.Users
                    .Where(u => u.Username.Contains(normalizedSearch))
                    .ToList();

                return View(users);
            });
        }
    }
}
