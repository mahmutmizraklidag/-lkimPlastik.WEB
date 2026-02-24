using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ilkimPlastik.WEB.Infrastructure
{
    public class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var isAdmin = context.HttpContext.Session.GetInt32("ADMIN_ID");
            if (isAdmin == null)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", new { area = "Admin" });
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}
