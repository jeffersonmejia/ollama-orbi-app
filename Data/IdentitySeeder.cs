using Microsoft.AspNetCore.Identity;

namespace SakilaApp.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Administrador", "Usuario", "Repartidor" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var users = new[]
        {
            new { Email = "admin1@orbi.app", Password = "Admin123*", Role = "Administrador" },
            new { Email = "admin2@orbi.app", Password = "Admin123*", Role = "Administrador" },
            new { Email = "usuario@orbi.app", Password = "Usuario123*", Role = "Usuario" },
            new { Email = "repartidor@orbi.app", Password = "Reparto123*", Role = "Repartidor" }
        };

        foreach (var userSeed in users)
        {
            var user = await userManager.FindByEmailAsync(userSeed.Email);

            if (user == null)
            {
                user = new IdentityUser
                {
                    UserName = userSeed.Email,
                    Email = userSeed.Email,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, userSeed.Password);
                if (!result.Succeeded) continue;
            }

            if (!await userManager.IsInRoleAsync(user, userSeed.Role))
                await userManager.AddToRoleAsync(user, userSeed.Role);
        }
    }
}
