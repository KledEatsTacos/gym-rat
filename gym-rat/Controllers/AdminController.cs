using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using gym_rat.Data;
using Microsoft.AspNetCore.Identity;
using gym_rat.Models;

namespace gym_rat.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // Dashboard Statistics
            var viewModel = new AdminDashboardViewModel
            {
                TotalMembers = (await _userManager.GetUsersInRoleAsync("Member")).Count,
                // Fallback if GetUsersInRoleAsync is slow or complex, simple count:
                // TotalMembers = await _context.Users.CountAsync(), // This counts admins too, but maybe close enough or filter by role logic if needed
                
                TotalTrainers = await _context.Trainers.CountAsync(),
                TotalServices = await _context.Services.CountAsync(),
                PendingAppointments = await _context.Appointments.CountAsync(a => a.Status == "Pending"),
                RecentAppointments = await _context.Appointments
                    .Include(a => a.Member)
                    .Include(a => a.Trainer)
                    .Include(a => a.Service)
                    .OrderByDescending(a => a.AppointmentDate)
                    .Take(5)
                    .ToListAsync()
            };

            return View(viewModel);
        }
    }

    public class AdminDashboardViewModel
    {
        public int TotalMembers { get; set; }
        public int TotalTrainers { get; set; }
        public int TotalServices { get; set; }
        public int PendingAppointments { get; set; }
        public List<Appointment> RecentAppointments { get; set; } = new();
    }
}
