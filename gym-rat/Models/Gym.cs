using System.ComponentModel.DataAnnotations;

namespace gym_rat.Models
{
    public class Gym
    {
        public int Id { get; set; }

        [Required]
        public required string Name { get; set; }

        public string? Address { get; set; }

        public string? PhoneNumber { get; set; }

        public TimeSpan OpeningTime { get; set; }
        public TimeSpan ClosingTime { get; set; }

        // Navigation properties
        public ICollection<Trainer> Trainers { get; set; } = new List<Trainer>();
        public ICollection<Service> Services { get; set; } = new List<Service>();
    }
}
