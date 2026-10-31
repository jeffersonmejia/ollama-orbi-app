using OrbiApp.Application.Features.Shared.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Models;
using OrbiApp.Models.Commerce;

namespace OrbiApp.Controllers;

[Authorize(Roles = "Administrador")]
public class InventoryController : Controller
{
    private readonly IApplicationDataSession _context;

    public InventoryController(IApplicationDataSession context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var query = _context.Set<Product>()
            .OrderBy(f => f.Name);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * 5).Take(5).ToListAsync();

        var paginated = new PaginatedList<Product>(items, total, page, 5);
        ViewData["PaginatedList"] = paginated;
        ViewData["TotalRegistros"] = total;

        return View(paginated);
    }

    [HttpPost]
    public async Task<IActionResult> AumentarStock(int id, int cantidad)
    {
        var item = await _context.Set<Product>().FindAsync(id);
        if (item == null) return NotFound();

        item.Stock += cantidad;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
