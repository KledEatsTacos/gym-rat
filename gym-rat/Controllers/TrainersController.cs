using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using gym_rat.Data;

namespace gym_rat.Controllers
{
    public class TrainersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TrainersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Trainers.ToListAsync());
        }
    }
}
