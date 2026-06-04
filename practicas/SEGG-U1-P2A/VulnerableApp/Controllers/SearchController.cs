using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VulnerableApp.Data;
using VulnerableApp.Models;

namespace VulnerableApp.Controllers
{
    public class SearchController : Controller
    {
        private readonly AppDbContext _db;

        public SearchController(AppDbContext db)
        {
            _db = db;
        }

        public IActionResult Index(string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return View(new List<User>());
            }

            string query = "SELECT * FROM Users WHERE Username LIKE '%" + search + "%'";

            var users = _db.Users.FromSqlRaw(query).ToList();

            return View(users);
        }
    }
}