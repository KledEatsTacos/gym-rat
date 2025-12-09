using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using gym_rat.Data;

namespace gym_rat.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Public - anyone can view services
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            return View(await _context.Services.ToListAsync());
        }
    }
}
