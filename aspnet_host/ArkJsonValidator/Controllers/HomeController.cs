using Microsoft.AspNetCore.Mvc;

namespace ArkJsonValidator.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
