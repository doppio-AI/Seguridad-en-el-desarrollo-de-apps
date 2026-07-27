using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VulnerableApp.Data;

namespace VulnerableApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext db, ILogger<AuthController> logger)
        {
            _db = db;
            _logger = logger;
        }

        private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

        public IActionResult Login()
        {
            _logger.LogInformation("Inicio Auth.Login (GET). IP:{IP}", ClientIp);
            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var sw = Stopwatch.StartNew();
            // IMPORTANTE: nunca se registra 'password' en los logs, solo el username.
            _logger.LogInformation("Inicio Auth.Login (POST). Usuario:{Usuario} IP:{IP}", username, ClientIp);

            try
            {
                var user = _db.Users.FirstOrDefault(u => u.Username == username);
                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    _logger.LogWarning(
                        "Evento de autenticación: LOGIN FALLIDO. Usuario:{Usuario} IP:{IP}",
                        username, ClientIp);

                    ViewBag.Error = "Credenciales inválidas";
                    return View();
                }

                HttpContext.Session.SetString("User", user.Username);
                HttpContext.Session.SetInt32("UserId", user.Id);

                _logger.LogInformation(
                    "Evento de autenticación: LOGIN EXITOSO. Usuario:{Usuario} UserId:{UserId} IP:{IP}",
                    user.Username, user.Id, ClientIp);

                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Auth.Login. Usuario:{Usuario} IP:{IP}", username, ClientIp);
                throw;
            }
            finally
            {
                sw.Stop();
                _logger.LogInformation("Fin Auth.Login (POST). DuracionMs:{DuracionMs}", sw.ElapsedMilliseconds);
            }
        }

        public IActionResult Dashboard()
        {
            var sw = Stopwatch.StartNew();
            var userId = HttpContext.Session.GetInt32("UserId");

            _logger.LogInformation("Inicio Auth.Dashboard. UserId:{UserId} IP:{IP}", userId, ClientIp);

            if (!userId.HasValue)
            {
                _logger.LogWarning("Acceso no autenticado a Auth.Dashboard. IP:{IP}", ClientIp);
                return RedirectToAction("Login");
            }

            var user = _db.Users.Find(userId.Value);

            sw.Stop();
            _logger.LogInformation(
                "Fin Auth.Dashboard. UserId:{UserId} DuracionMs:{DuracionMs}",
                userId, sw.ElapsedMilliseconds);

            return View(user);
        }

        public IActionResult Logout()
        {
            var username = HttpContext.Session.GetString("User");
            HttpContext.Session.Clear();

            _logger.LogInformation(
                "Evento de autenticación: LOGOUT. Usuario:{Usuario} IP:{IP}",
                username, ClientIp);

            return RedirectToAction("Index", "Home");
        }
    }
}
