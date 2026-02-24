using ilkimPlastik.WEB.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize]
    public abstract class AdminBaseController : Controller
    {
    }
}