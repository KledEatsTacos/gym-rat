using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using gym_rat.Data;
using gym_rat.Models;

namespace gym_rat.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrainersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TrainersApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/trainersapi
        // Returns all trainers with their gym info
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetTrainers()
        {
            var trainers = await _context.Trainers
                .Include(t => t.Gym)
                .Select(t => new
                {
                    t.Id,
                    t.FirstName,
                    t.LastName,
                    t.Specialization,
                    StartTime = t.StartTime.ToString(@"hh\:mm"),
                    EndTime = t.EndTime.ToString(@"hh\:mm"),
                    GymName = t.Gym != null ? t.Gym.Name : null
                })
                .ToListAsync();

            return Ok(trainers);
        }

        // GET: api/trainersapi/available?date=2024-12-20
        // Returns trainers available on a specific date (not fully booked)
        [HttpGet("available")]
        public async Task<ActionResult<IEnumerable<object>>> GetAvailableTrainers([FromQuery] DateTime date)
        {
            // Get all trainers
            var allTrainers = await _context.Trainers
                .Include(t => t.Gym)
                .ToListAsync();

            // Get appointments for the specified date
            var bookedTrainerIds = await _context.Appointments
                .Where(a => a.AppointmentDate.Date == date.Date)
                .Where(a => a.Status != "Cancelled")
                .Select(a => a.TrainerId)
                .Distinct()
                .ToListAsync();

            // Filter out fully booked trainers (simple version: if they have any appointment that day)
            // In a more complex version, you'd check time slots
            var availableTrainers = allTrainers
                .Where(t => !bookedTrainerIds.Contains(t.Id))
                .Select(t => new
                {
                    t.Id,
                    t.FirstName,
                    t.LastName,
                    t.Specialization,
                    StartTime = t.StartTime.ToString(@"hh\:mm"),
                    EndTime = t.EndTime.ToString(@"hh\:mm"),
                    GymName = t.Gym?.Name
                })
                .ToList();

            return Ok(new
            {
                Date = date.ToString("yyyy-MM-dd"),
                AvailableCount = availableTrainers.Count,
                Trainers = availableTrainers
            });
        }

        // GET: api/trainersapi/{id}
        // Returns a specific trainer by ID
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetTrainer(int id)
        {
            var trainer = await _context.Trainers
                .Include(t => t.Gym)
                .Where(t => t.Id == id)
                .Select(t => new
                {
                    t.Id,
                    t.FirstName,
                    t.LastName,
                    t.Specialization,
                    StartTime = t.StartTime.ToString(@"hh\:mm"),
                    EndTime = t.EndTime.ToString(@"hh\:mm"),
                    GymName = t.Gym != null ? t.Gym.Name : null
                })
                .FirstOrDefaultAsync();

            if (trainer == null)
            {
                return NotFound(new { Message = "Trainer not found" });
            }

            return Ok(trainer);
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AppointmentsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/appointmentsapi?memberId=xxx
        // Returns appointments for a specific member
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAppointments([FromQuery] string? memberId)
        {
            var query = _context.Appointments
                .Include(a => a.Trainer)
                .Include(a => a.Service)
                .Include(a => a.Member)
                .AsQueryable();

            // Filter by memberId if provided
            if (!string.IsNullOrEmpty(memberId))
            {
                query = query.Where(a => a.MemberId == memberId);
            }

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .Select(a => new
                {
                    a.Id,
                    AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd HH:mm"),
                    a.Status,
                    TrainerName = a.Trainer != null ? $"{a.Trainer.FirstName} {a.Trainer.LastName}" : null,
                    ServiceName = a.Service != null ? a.Service.Name : null,
                    ServicePrice = a.Service != null ? a.Service.Price : 0,
                    ServiceDuration = a.Service != null ? a.Service.DurationMinutes : 0,
                    MemberEmail = a.Member != null ? a.Member.Email : null
                })
                .ToListAsync();

            return Ok(new
            {
                Count = appointments.Count,
                Appointments = appointments
            });
        }
    }
}
