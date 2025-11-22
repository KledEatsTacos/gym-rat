using System.ComponentModel.DataAnnotations;

namespace gym_rat.Models
{
    public class Trainer
    {
        public int Id { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public string Specialization { get; set; } // e.g., Muscle Building, Weight Loss

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public int GymId { get; set; }
        public Gym Gym { get; set; }

        public ICollection<Appointment> Appointments { get; set; }
    }
}
