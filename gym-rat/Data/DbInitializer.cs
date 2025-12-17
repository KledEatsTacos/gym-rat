using gym_rat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace gym_rat.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // Ensure Roles Exist
            string[] roleNames = { "Admin", "Member" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Ensure Admin User Exists
            var adminEmail = "G221210580@sakarya.edu.tr";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "User",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                
                var createPowerUser = await userManager.CreateAsync(adminUser, "sau");
                
                if (createPowerUser.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Ensure the single Gym exists
            if (!await context.Gyms.AnyAsync())
            {
                var gym = new Gym
                {
                    Name = "GymRat Fitness Center",
                    Address = "Sakarya Üniversitesi, Esentepe Kampüsü",
                    PhoneNumber = "+90 264 295 5454",
                    OpeningTime = new TimeSpan(6, 0, 0),  // 06:00
                    ClosingTime = new TimeSpan(23, 0, 0)  // 23:00
                };
                context.Gyms.Add(gym);
                await context.SaveChangesAsync();

                // Add sample trainers
                var trainers = new[]
                {
                    new Trainer { FirstName = "Ahmet", LastName = "Yılmaz", Specialization = "Kas Geliştirme", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), GymId = gym.Id },
                    new Trainer { FirstName = "Elif", LastName = "Demir", Specialization = "Yoga & Pilates", StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(18, 0, 0), GymId = gym.Id },
                    new Trainer { FirstName = "Mehmet", LastName = "Kaya", Specialization = "Kilo Verme", StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(22, 0, 0), GymId = gym.Id }
                };
                context.Trainers.AddRange(trainers);

                // Add sample services
                var services = new[]
                {
                    new Service { Name = "Fitness", Description = "Genel fitness ve ağırlık antrenmanı", DurationMinutes = 60, Price = 150, GymId = gym.Id },
                    new Service { Name = "Yoga", Description = "Rahatlama ve esneklik çalışması", DurationMinutes = 45, Price = 120, GymId = gym.Id },
                    new Service { Name = "Pilates", Description = "Core güçlendirme egzersizleri", DurationMinutes = 45, Price = 130, GymId = gym.Id },
                    new Service { Name = "Kişisel Antrenman", Description = "Birebir eğitmen eşliğinde antrenman", DurationMinutes = 60, Price = 250, GymId = gym.Id }
                };
                context.Services.AddRange(services);

                await context.SaveChangesAsync();
            }
        }
    }
}

