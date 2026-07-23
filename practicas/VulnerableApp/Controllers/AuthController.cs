using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VulnerableApp.Data;

namespace VulnerableApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _db;

        public AuthController(AppDbContext db)
        {
            _db = db;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            if (username == "admin" && password == "admin")
            {
                HttpContext.Session.SetString("User", username);
                HttpContext.Session.SetInt32("UserId", 1);

                return RedirectToAction("Dashboard");
            }

            string query = "SELECT * FROM Users WHERE Username = '" + username + "' AND Password = '" + password + "'";

            var user = _db.Users.FromSqlRaw(query).FirstOrDefault();

            if (user != null)
            {
                HttpContext.Session.SetString("User", user.Username);
                HttpContext.Session.SetInt32("UserId", user.Id);

                return RedirectToAction("Dashboard");
            }

            ViewBag.Error = "Usuario/contraseña inválido";
            return View();
        }

        public IActionResult Dashboard()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
            {
                return RedirectToAction("Login");
            }

            var user = _db.Users.Find(userId.Value);

            return View(user);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }
    }
}