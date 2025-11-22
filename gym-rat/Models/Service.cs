using System.ComponentModel.DataAnnotations;

namespace gym_rat.Models
{
    public class Service
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } // e.g., Yoga, Pilates, Fitness

        public string Description { get; set; }

        public int DurationMinutes { get; set; }

        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        public int GymId { get; set; }
        public Gym Gym { get; set; }

        public ICollection<Appointment> Appointments { get; set; }
    }
}
