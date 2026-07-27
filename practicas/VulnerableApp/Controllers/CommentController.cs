using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VulnerableApp.Security;

namespace VulnerableApp.Controllers
{
    public class CommentController : Controller
    {
        private static List<string> _comments = new();
        private readonly ILogger<CommentController> _logger;

        public CommentController(ILogger<CommentController> logger)
        {
            _logger = logger;
        }

        private string? CurrentUser => HttpContext.Session.GetString("User");
        private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

        public IActionResult Index()
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation(
                "Inicio Comment.Index. Usuario:{User} IP:{IP} TotalComentarios:{Total}",
                CurrentUser, ClientIp, _comments.Count);

            var result = View(_comments);

            sw.Stop();
            _logger.LogInformation("Fin Comment.Index. DuracionMs:{DuracionMs}", sw.ElapsedMilliseconds);
            return result;
        }

        [HttpPost]
        public IActionResult AddComment(string comment)
        {
            var sw = Stopwatch.StartNew();
            var longitud = comment?.Length ?? 0;

            _logger.LogInformation(
                "Inicio Comment.AddComment. Usuario:{User} IP:{IP} LongitudComentario:{Longitud}",
                CurrentUser, ClientIp, longitud);

            if (SecurityPatternDetector.LooksLikeXss(comment))
            {
                _logger.LogWarning(
                    "Posible intento de XSS detectado en Comment. Usuario:{User} IP:{IP} Fragmento:{Fragmento}",
                    CurrentUser, ClientIp, SecurityPatternDetector.SafeSnippet(comment));
            }

            try
            {
                if (string.IsNullOrEmpty(comment))
                {
                    _logger.LogWarning(
                        "Comment.AddComment recibió un comentario vacío. Usuario:{User} IP:{IP}",
                        CurrentUser, ClientIp);
                }
                else
                {
                    _comments.Add(comment);
                    _logger.LogInformation(
                        "Comentario agregado. Usuario:{User} IP:{IP} LongitudComentario:{Longitud}",
                        CurrentUser, ClientIp, longitud);
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Comment.AddComment. Usuario:{User} IP:{IP}", CurrentUser, ClientIp);
                throw;
            }
            finally
            {
                sw.Stop();
                _logger.LogInformation("Fin Comment.AddComment. DuracionMs:{DuracionMs}", sw.ElapsedMilliseconds);
            }
        }
    }
}
