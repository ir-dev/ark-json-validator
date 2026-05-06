using Microsoft.AspNetCore.Mvc;

namespace McpValidatorClient.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
