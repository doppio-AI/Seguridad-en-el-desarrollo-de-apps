using Microsoft.AspNetCore.Mvc;
using VulnerableApp.Data;
using VulnerableApp.Security;

namespace VulnerableApp.Controllers
{
    public class AuthController : InstrumentedController<AuthController>
    {
        private readonly AppDbContext _db;

        public AuthController(AppDbContext db, ILogger<AuthController> logger) : base(logger)
        {
            _db = db;
        }

        public IActionResult Login()
        {
            return ExecuteLogged(nameof(Login), safeParameters: null, View);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string username, string password)
        {
            // La contrasena se usa para validar credenciales, pero nunca forma parte
            // de los parametros seguros enviados al sistema de logging.
            var safeUsername = SecurityPatternDetector.SanitizeForLog(username);
            return ExecuteLogged(nameof(Login), new { Username = safeUsername }, () =>
            {
                Logger.LogInformation("Intento de autenticacion para {Username}", safeUsername);

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    Logger.LogWarning(
                        "Autenticacion rechazada por credenciales incompletas para {Username}",
                        username);
                    ViewBag.Error = "Credenciales invalidas";
                    return View();
                }

                var normalizedUsername = username.Trim();
                var user = _db.Users.FirstOrDefault(u => u.Username == normalizedUsername);
                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    Logger.LogWarning(
                        "Autenticacion fallida para {Username}",
                        normalizedUsername);
                    ViewBag.Error = "Credenciales invalidas";
                    return View();
                }

                HttpContext.Session.SetString("User", user.Username);
                HttpContext.Session.SetInt32("UserId", user.Id);
                Logger.LogInformation(
                    "Autenticacion exitosa para {Username} con identificador {UserId}",
                    user.Username,
                    user.Id);
                return RedirectToAction(nameof(Dashboard));
            });
        }

        public IActionResult Dashboard()
        {
            return ExecuteLogged(nameof(Dashboard), safeParameters: null, () =>
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    Logger.LogWarning("Acceso no autenticado al panel");
                    return RedirectToAction(nameof(Login));
                }

                var user = _db.Users.Find(userId.Value);
                if (user == null)
                {
                    Logger.LogWarning(
                        "No se encontro el usuario autenticado {UserId}",
                        userId.Value);
                }

                return View(user);
            });
        }

        public IActionResult Logout()
        {
            return ExecuteLogged(nameof(Logout), safeParameters: null, () =>
            {
                var username = HttpContext.Session.GetString("User") ?? "Anonimo";
                HttpContext.Session.Clear();
                Logger.LogInformation("Cierre de sesion para {Username}", username);
                return RedirectToAction("Index", "Home");
            });
        }
    }
}
