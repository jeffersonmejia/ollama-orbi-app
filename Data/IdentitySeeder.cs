using Microsoft.AspNetCore.Identity;

namespace SakilaApp.Data;

public static class IdentitySeeder
{
    private static readonly string[] ApplicationRoles =
        { "Administrador", "Vendedor", "Repartidor", "Usuario" };

    private static readonly string[] LegacyTestEmails =
    {
        "admin1@orbi.app",
        "admin2@orbi.app",
        "usuario@orbi.app",
        "repartidor@orbi.app"
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var role in ApplicationRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        foreach (var email in LegacyTestEmails)
        {
            var legacyUser = await userManager.FindByEmailAsync(email);
            if (legacyUser != null)
            {
                await userManager.DeleteAsync(legacyUser);
            }
        }

        var obsoleteRoles = roleManager.Roles
            .Where(role => role.Name != null && !ApplicationRoles.Contains(role.Name))
            .ToList();

        foreach (var role in obsoleteRoles)
        {
            var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
            foreach (var user in usersInRole)
            {
                await userManager.RemoveFromRoleAsync(user, role.Name!);
            }

            await roleManager.DeleteAsync(role);
        }

        var users = new[]
        {
            new { Email = "jefferson.mejia@orbi.com", Password = "Admin123*", Role = "Administrador" },
            new { Email = "maria.lopez@orbi.com", Password = "Vendedor123*", Role = "Vendedor" },
            new { Email = "carlos.perez@orbi.com", Password = "Reparto123*", Role = "Repartidor" },
            new { Email = "ana.torres@orbi.com", Password = "Usuario123*", Role = "Usuario" }
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

            var currentRoles = await userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Where(role => role != userSeed.Role).ToArray();
            if (rolesToRemove.Length > 0)
            {
                await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }

            if (!await userManager.IsInRoleAsync(user, userSeed.Role))
            {
                await userManager.AddToRoleAsync(user, userSeed.Role);
            }
        }
    }
}
