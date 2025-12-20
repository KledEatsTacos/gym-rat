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

        // GET: api/trainersapi/{id}/timeslots?date=2024-12-20
        // Returns available time slots for a trainer on a specific date
        [HttpGet("{id}/timeslots")]
        public async Task<ActionResult<object>> GetAvailableTimeSlots(int id, [FromQuery] string date)
        {
            try
            {
                var trainer = await _context.Trainers.FindAsync(id);
                if (trainer == null)
                {
                    return NotFound(new { Message = "Trainer not found" });
                }

                // Parse the date
                if (!DateTime.TryParse(date, out DateTime parsedDate))
                {
                    return BadRequest(new { Message = "Invalid date format" });
                }

                // Date range for PostgreSQL compatibility
                var startOfDay = parsedDate.Date;
                var endOfDay = parsedDate.Date.AddDays(1);

                // Get booked appointment times for this trainer on this date
                var bookedTimes = await _context.Appointments
                    .Include(a => a.Service)
                    .Where(a => a.TrainerId == id)
                    .Where(a => a.AppointmentDate >= startOfDay && a.AppointmentDate < endOfDay)
                    .Where(a => a.Status != "Cancelled")
                    .Select(a => new { 
                        Start = a.AppointmentDate.TimeOfDay, 
                        Duration = a.Service != null ? a.Service.DurationMinutes : 60 
                    })
                    .ToListAsync();

                // Generate hourly slots within trainer's working hours
                // Fallback to default hours (08:00-20:00) if trainer has no hours set
                var availableSlots = new List<object>();
                var startTime = trainer.StartTime == TimeSpan.Zero ? new TimeSpan(8, 0, 0) : trainer.StartTime;
                var endTime = trainer.EndTime == TimeSpan.Zero ? new TimeSpan(20, 0, 0) : trainer.EndTime;
                var currentTime = startTime;
                
                while (currentTime < endTime)
                {
                    var slotStart = currentTime;
                    var slotEnd = currentTime.Add(TimeSpan.FromHours(1));
                    
                    // Check if this slot overlaps with any booked appointment
                    var isBooked = bookedTimes.Any(bt => 
                        slotStart < bt.Start.Add(TimeSpan.FromMinutes(bt.Duration)) && 
                        slotEnd > bt.Start);
                    
                    availableSlots.Add(new
                    {
                        Time = slotStart.ToString(@"hh\:mm"),
                        Available = !isBooked
                    });
                    
                    currentTime = currentTime.Add(TimeSpan.FromHours(1));
                }

                return Ok(new
                {
                    TrainerId = id,
                    Date = parsedDate.ToString("yyyy-MM-dd"),
                    TrainerName = $"{trainer.FirstName} {trainer.LastName}",
                    WorkingHours = $"{trainer.StartTime:hh\\:mm} - {trainer.EndTime:hh\\:mm}",
                    TimeSlots = availableSlots
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error loading time slots", Error = ex.Message });
            }
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
