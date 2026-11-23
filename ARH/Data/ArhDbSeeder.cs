using ARH.Domain.Entities;
using ARH.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ARH.Data
{

    public static class ArhDbSeeder
    {
        private static readonly Guid BobId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid AliceId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid CarolId =
            Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid DanaId =
            Guid.Parse("44444444-4444-4444-4444-444444444444");

        private static readonly Guid ErinId =
            Guid.Parse("55555555-5555-5555-5555-555555555555");

        private static readonly Guid CrmId =
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        private static readonly Guid FinanceId =
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        public static async Task SeedAsync(
            ArhDbContext db,
            CancellationToken cancellationToken = default)
        {
            await db.Database.MigrateAsync(cancellationToken);

            var users = new[]
            {
            new User
            {
                Id = AliceId,
                Email = "alice@example.local",
                FullName = "Alice",
                Role = UserRole.Requester,
                ManagerId = BobId,
                IsActive = true
            },
            new User
            {
                Id = BobId,
                Email = "bob@example.local",
                FullName = "Bob",
                Role = UserRole.Manager,
                IsActive = true
            },
            new User
            {
                Id = CarolId,
                Email = "carol@example.local",
                FullName = "Carol",
                Role = UserRole.SystemOwner,
                IsActive = true
            },
            new User
            {
                Id = DanaId,
                Email = "dana@example.local",
                FullName = "Dana",
                Role = UserRole.SystemOwner,
                IsActive = true
            },
            new User
            {
                Id = ErinId,
                Email = "erin@example.local",
                FullName = "Erin",
                Role = UserRole.AdminAuditor,
                IsActive = true
            }
        };

            foreach (var user in users)
            {
                var exists = await db.Users.AnyAsync(
                    x => x.Email == user.Email,
                    cancellationToken);

                if (!exists)
                {
                    user.CreatedAtUtc = DateTime.UtcNow;
                    db.Users.Add(user);
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            var applications = new[]
            {
            new Application
            {
                Id = CrmId,
                Name = "CRM",
                SystemOwnerId = CarolId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            },
            new Application
            {
                Id = FinanceId,
                Name = "Finance Portal",
                SystemOwnerId = DanaId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            }
        };

            foreach (var application in applications)
            {
                var exists = await db.Applications.AnyAsync(
                    x => x.Name == application.Name,
                    cancellationToken);

                if (!exists)
                {
                    db.Applications.Add(application);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}