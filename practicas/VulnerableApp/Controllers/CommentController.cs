using Microsoft.AspNetCore.Mvc;
using VulnerableApp.Security;
using VulnerableApp.Services;

namespace VulnerableApp.Controllers
{
    public class CommentController : InstrumentedController<CommentController>
    {
        private readonly ICommentStore _commentStore;

        public CommentController(
            ICommentStore commentStore,
            ILogger<CommentController> logger) : base(logger)
        {
            _commentStore = commentStore;
        }

        public IActionResult Index()
        {
            return ExecuteLogged(nameof(Index), safeParameters: null,
                () => View(_commentStore.GetAll()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddComment(string comment)
        {
            return ExecuteLogged(
                nameof(AddComment),
                new { CommentLength = comment?.Length ?? 0 },
                () =>
                {
                    if (string.IsNullOrWhiteSpace(comment))
                    {
                        Logger.LogWarning("Se rechazo un comentario vacio");
                    }
                    else
                    {
                        if (SecurityPatternDetector.LooksLikeXss(comment))
                        {
                            Logger.LogWarning(
                                "Posible intento de XSS detectado | Longitud: {CommentLength}",
                                comment.Length);
                        }

                        _commentStore.Add(comment);
                        Logger.LogInformation(
                            "Comentario almacenado con longitud {CommentLength}",
                            comment.Length);
                    }

                    return RedirectToAction(nameof(Index));
                });
        }
    }
}
