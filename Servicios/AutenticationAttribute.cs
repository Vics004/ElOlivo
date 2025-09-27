using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ElOlivo.Servicios
{
    public class AutenticationAttribute
    {
        public class AutenticacionAttribute : ActionFilterAttribute
        {
            public override void OnActionExecuting(ActionExecutingContext context)
            {
                var usuarioId = context.HttpContext.Session.GetInt32("usuarioId");

                if (usuarioId == null)
                {
                    context.Result = new RedirectToActionResult("Autenticar", "Login", null);
                }
                base.OnActionExecuting(context);
            }
        }

    }
}
