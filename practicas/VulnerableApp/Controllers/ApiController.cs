using Microsoft.AspNetCore.Mvc;
using VulnerableApp.Data;

namespace VulnerableApp.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : InstrumentedController<ApiController>
    {
        private readonly AppDbContext _db;

        public ApiController(AppDbContext db, ILogger<ApiController> logger) : base(logger)
        {
            _db = db;
        }

        [HttpGet("user/{id}")]
        public IActionResult GetUser(int id)
        {
            return ExecuteLogged(nameof(GetUser), new { Id = id }, () =>
            {
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (!currentUserId.HasValue)
                {
                    Logger.LogWarning(
                        "Solicitud no autenticada para consultar al usuario {RequestedUserId}",
                        id);
                    return Unauthorized();
                }

                if (id != currentUserId.Value)
                {
                    Logger.LogWarning(
                        "Acceso denegado: usuario {CurrentUserId} intento consultar a {RequestedUserId}",
                        currentUserId.Value,
                        id);
                    return StatusCode(StatusCodes.Status403Forbidden);
                }

                var user = _db.Users.Find(id);
                if (user == null)
                {
                    Logger.LogWarning("No se encontro al usuario {RequestedUserId}", id);
                    return NotFound();
                }

                return Ok(new
                {
                    user.Id,
                    user.Username,
                    user.Email
                });
            });
        }

        [HttpGet("users")]
        public IActionResult GetAllUsers()
        {
            return ExecuteLogged(nameof(GetAllUsers), safeParameters: null, () =>
            {
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (!currentUserId.HasValue)
                {
                    Logger.LogWarning("Solicitud no autenticada para listar usuarios");
                    return Unauthorized();
                }

                var users = _db.Users
                    .Select(user => new { user.Id, user.Username, user.Email })
                    .ToList();

                return Ok(users);
            });
        }
    }
}
