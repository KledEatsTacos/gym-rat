using System.ComponentModel.DataAnnotations;

namespace gym_rat.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        public DateTime AppointmentDate { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled

        public required string MemberId { get; set; }
        public ApplicationUser? Member { get; set; }

        public int TrainerId { get; set; }
        public Trainer? Trainer { get; set; }

        public int ServiceId { get; set; }
        public Service? Service { get; set; }
    }
}
