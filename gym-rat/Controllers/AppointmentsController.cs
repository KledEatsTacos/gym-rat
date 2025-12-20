using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using gym_rat.Data;
using gym_rat.Models;

namespace gym_rat.Controllers
{
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AppointmentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Appointments (Member sees their own, Admin sees all)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            IQueryable<Appointment> appointments = _context.Appointments
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member);

            // If not admin, filter to only show user's appointments
            if (!User.IsInRole("Admin"))
            {
                appointments = appointments.Where(a => a.MemberId == user.Id);
            }

            return View(await appointments.OrderByDescending(a => a.AppointmentDate).ToListAsync());
        }

        // GET: Appointments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null) return NotFound();

            // Check access: only owner or admin can view
            var user = await _userManager.GetUserAsync(User);
            if (appointment.MemberId != user?.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(appointment);
        }

        // GET: Appointments/Create
        public IActionResult Create()
        {
            ViewData["TrainerId"] = new SelectList(_context.Trainers, "Id", "FirstName");
            ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name");
            return View();
        }

        // POST: Appointments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int TrainerId, int ServiceId, DateTime AppointmentDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Get trainer and service for validation
            var trainer = await _context.Trainers.FindAsync(TrainerId);
            var service = await _context.Services.FindAsync(ServiceId);

            if (trainer == null || service == null)
            {
                ModelState.AddModelError("", "Invalid trainer or service selected.");
                ViewData["TrainerId"] = new SelectList(_context.Trainers, "Id", "FirstName", TrainerId);
                ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", ServiceId);
                return View();
            }

            // Check if appointment time is within trainer's working hours
            var appointmentTime = AppointmentDate.TimeOfDay;
            if (appointmentTime < trainer.StartTime || appointmentTime >= trainer.EndTime)
            {
                ModelState.AddModelError("AppointmentDate", 
                    $"Trainer is only available between {trainer.StartTime:hh\\:mm} and {trainer.EndTime:hh\\:mm}");
                ViewData["TrainerId"] = new SelectList(_context.Trainers, "Id", "FirstName", TrainerId);
                ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", ServiceId);
                return View();
            }

            // Convert to UTC for PostgreSQL
            var utcAppointmentDate = DateTime.SpecifyKind(AppointmentDate, DateTimeKind.Utc);

            // Check for overlapping appointments for this trainer
            var appointmentEnd = utcAppointmentDate.AddMinutes(service.DurationMinutes);
            var hasOverlap = await _context.Appointments
                .Where(a => a.TrainerId == TrainerId && a.Status != "Cancelled")
                .Where(a => a.AppointmentDate < appointmentEnd && 
                           a.AppointmentDate.AddMinutes(a.Service!.DurationMinutes) > utcAppointmentDate)
                .AnyAsync();

            if (hasOverlap)
            {
                ModelState.AddModelError("AppointmentDate", "This time slot is already booked. Please choose another time.");
                ViewData["TrainerId"] = new SelectList(_context.Trainers, "Id", "FirstName", TrainerId);
                ViewData["ServiceId"] = new SelectList(_context.Services, "Id", "Name", ServiceId);
                return View();
            }

            var appointment = new Appointment
            {
                MemberId = user.Id,
                TrainerId = TrainerId,
                ServiceId = ServiceId,
                AppointmentDate = utcAppointmentDate,
                Status = "Pending"
            };

            _context.Add(appointment);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Appointment booked successfully! Waiting for admin approval.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Appointments/Cancel/5 (Member can cancel their own)
        public async Task<IActionResult> Cancel(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (appointment.MemberId != user?.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(appointment);
        }

        // POST: Appointments/Cancel/5
        [HttpPost, ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (appointment.MemberId != user?.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            appointment.Status = "Cancelled";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Appointment cancelled successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ========== ADMIN ONLY ACTIONS ==========

        // GET: Appointments/Manage (Admin view all pending)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var pendingAppointments = await _context.Appointments
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member)
                .Where(a => a.Status == "Pending")
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync();

            return View(pendingAppointments);
        }

        // POST: Appointments/Approve/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            appointment.Status = "Confirmed";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Appointment approved!";
            return RedirectToAction(nameof(Manage));
        }

        // POST: Appointments/Reject/5
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            appointment.Status = "Cancelled";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Appointment rejected.";
            return RedirectToAction(nameof(Manage));
        }
    }
}
