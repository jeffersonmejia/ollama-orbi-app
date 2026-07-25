using Microsoft.AspNetCore.Identity;

namespace SakilaApp.Data;

public static class IdentitySeeder
{
    private static readonly string[] ApplicationRoles =
        { "Administrador", "Vendedor", "Repartidor", "Usuario" };

    private static readonly SeedUser[] SeedUsers =
    {
        new("jefferson.mejia@orbi.com", "Admin123*", "Administrador"),
        new("maria.lopez@orbi.com", "Vendedor123*", "Vendedor"),
        new("carlos.perez@orbi.com", "Reparto123*", "Repartidor"),
        new("ana.torres@orbi.com", "Usuario123*", "Usuario")
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

        foreach (var userSeed in SeedUsers)
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

    private sealed record SeedUser(string Email, string Password, string Role);
}
