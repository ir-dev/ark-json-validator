using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using McpServiceHub.Data;
using McpServiceHub.Models.Entities;
using McpServiceHub.Models.ViewModels;

namespace McpServiceHub.Controllers;

[Authorize]
public class McpServicesController : Controller
{
    private readonly AppDbContext _context;

    public McpServicesController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
    }

    // My Services
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var services = await _context.McpServices
            .Include(s => s.Owner)
            .Where(s => s.OwnerUserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View(services);
    }

    // All public services (for discovery)
    public async Task<IActionResult> Browse()
    {
        var services = await _context.McpServices
            .Include(s => s.Owner)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View(services);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(McpServiceViewModel model)
    {
        if (ModelState.IsValid)
        {
            var service = new McpService
            {
                Name = model.Name,
                Description = model.Description,
                EndpointUrl = model.EndpointUrl,
                Domain = model.Domain,
                AuthenticationType = model.AuthenticationType,
                ApiKeyHeader = model.ApiKeyHeader,
                OwnerUserId = GetCurrentUserId(),
                CreatedAt = DateTime.UtcNow
            };

            _context.McpServices.Add(service);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "MCP Service registered successfully!";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var service = await _context.McpServices
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
        {
            return NotFound();
        }

        if (service.OwnerUserId != GetCurrentUserId())
        {
            TempData["ErrorMessage"] = "You don't have permission to edit this service.";
            return RedirectToAction(nameof(Index));
        }

        var model = new McpServiceViewModel
        {
            Id = service.Id,
            Name = service.Name,
            Description = service.Description,
            EndpointUrl = service.EndpointUrl,
            Domain = service.Domain,
            AuthenticationType = service.AuthenticationType,
            ApiKeyHeader = service.ApiKeyHeader,
            OwnerEmail = service.Owner.Email
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, McpServiceViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            var service = await _context.McpServices.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            if (service.OwnerUserId != GetCurrentUserId())
            {
                TempData["ErrorMessage"] = "You don't have permission to edit this service.";
                return RedirectToAction(nameof(Index));
            }

            service.Name = model.Name;
            service.Description = model.Description;
            service.EndpointUrl = model.EndpointUrl;
            service.Domain = model.Domain;
            service.AuthenticationType = model.AuthenticationType;
            service.ApiKeyHeader = model.ApiKeyHeader;
            service.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "MCP Service updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await _context.McpServices
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
        {
            return NotFound();
        }

        if (service.OwnerUserId != GetCurrentUserId())
        {
            TempData["ErrorMessage"] = "You don't have permission to delete this service.";
            return RedirectToAction(nameof(Index));
        }

        return View(service);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var service = await _context.McpServices.FindAsync(id);
        if (service != null && service.OwnerUserId == GetCurrentUserId())
        {
            _context.McpServices.Remove(service);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "MCP Service deleted successfully!";
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var service = await _context.McpServices
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
        {
            return NotFound();
        }

        return View(service);
    }
}