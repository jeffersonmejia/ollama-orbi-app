using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SakilaApp.Models.Identity;

namespace SakilaApp.Data;

public static class IdentitySeeder
{
    private static readonly string[] ApplicationRoles =
        { "Administrador", "Vendedor", "Repartidor", "Usuario" };

    private static readonly SeedUser[] SeedUsers =
    {
        new("jefferson.mejia@orbi.com", "Admin123*", "Administrador", "Jefferson", "Mejía", "0912345675", "Av. Principal 101", "Calle 9 de Octubre", "09", "0901", "Frente al parque"),
        new("maria.lopez@orbi.com", "Vendedor123*", "Vendedor", "María", "López", "1712345671", "Av. Amazonas", "Calle Naciones Unidas", "17", "1701", null),
        new("carlos.perez@orbi.com", "Reparto123*", "Repartidor", "Carlos", "Pérez", "0923456784", "Av. Nicolás Lapentti", "Calle Loja", "09", "0907", null),
        new("ana.torres@orbi.com", "Usuario123*", "Usuario", "Ana", "Torres", "0123456782", "Av. de las Américas", "Calle del Batán", "01", "0101", "Casa esquinera")
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

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

            var profile = await dbContext.UserProfiles
                .SingleOrDefaultAsync(item => item.IdentityUserId == user.Id);

            if (profile is null)
            {
                profile = new UserProfile { IdentityUserId = user.Id };
                dbContext.UserProfiles.Add(profile);
            }

            profile.FirstName = userSeed.FirstName;
            profile.LastName = userSeed.LastName;
            profile.Cedula = userSeed.Cedula;
            profile.AddressLine1 = userSeed.AddressLine1;
            profile.AddressLine2 = userSeed.AddressLine2;
            profile.ProvinceCode = userSeed.ProvinceCode;
            profile.CityCode = userSeed.CityCode;
            profile.Reference = userSeed.Reference;
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed record SeedUser(
        string Email,
        string Password,
        string Role,
        string FirstName,
        string LastName,
        string Cedula,
        string AddressLine1,
        string AddressLine2,
        string ProvinceCode,
        string CityCode,
        string? Reference);
}
