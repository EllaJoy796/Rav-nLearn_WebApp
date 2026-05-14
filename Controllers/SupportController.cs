using Microsoft.AspNetCore.Mvc;

namespace RavnLearnWeb.Controllers
{
    public class SupportController : Controller
    {
        public IActionResult HowToUse()
        {
            return View();
        }
    }
}