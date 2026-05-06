using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using McpServiceHub.Data;

namespace McpServiceHub.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }
    public async Task<IActionResult> Index()
    {
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Landing()
    {
        var recentServices = await _context.McpServices
            .Include(s => s.Owner)
            .OrderByDescending(s => s.CreatedAt)
            .Take(10)
            .ToListAsync();

        return View(recentServices);
    }

    public IActionResult Privacy()
    {
        return View();
    }
}