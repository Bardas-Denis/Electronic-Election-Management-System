using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using Electronic_Election_Management_System.Services;

namespace Electronic_Election_Management_System.Data
{
    // Seeds a default Admin account when the database is empty, so the team
    // can log into the admin panel immediately without a manual setup step.
    public static class SeedData
    {
        public static async Task EnsureAdminUserAsync(ElectionDbContext db)
        {
          
            bool anyAdmin = await db.Users.AnyAsync(u => u.Role == UserRole.Admin);
            if (anyAdmin)
            {
                return;
            }

            const string adminEmail = "admin@election.local";

            
            bool emailTaken = await db.Users.AnyAsync(u => u.Email == adminEmail);
            if (emailTaken)
            {
                throw new System.InvalidOperationException($"Cannot seed default admin because '{adminEmail}' already exists.");
            }

            var admin = new User
            {
                Email = adminEmail,
                PasswordHash = PasswordHasher.Hash("Admin123!"),
                Role = UserRole.Admin
            };

            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        // Seeds 100 test voters and 10 elections (with a mix of past, active,
        // and future ones) so the team has realistic data to test/demo against.
        // Runs only once - skips if elections already exist.
        public static async Task EnsureTestDataAsync(ElectionDbContext db)
        {
            
            bool alreadySeeded = await db.Elections.AnyAsync();
            if (alreadySeeded)
            {
                return;
            }

            bool anyTestUsers = await db.Users.AnyAsync(u => u.Email.EndsWith("@test.com"));
            if (!anyTestUsers)
            {
                var testPasswordHash = PasswordHasher.Hash("Test123!");
                var voters = new List<User>();

                for (int i = 1; i <= 100; i++)
                {
                    voters.Add(new User
                    {
                        Email = $"user{i}@test.com",
                        PasswordHash = testPasswordHash,
                        Role = UserRole.Voter
                    });
                }

                db.Users.AddRange(voters);
                await db.SaveChangesAsync();
            }

            var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin)
                ?? throw new System.InvalidOperationException("Cannot seed test elections because no admin user exists. EnsureAdminUserAsync must run successfully first.");

            var now = DateTime.UtcNow;
            var elections = new List<Election>
            {
                new Election
                {
                    Title = "Alegeri Consiliul Studentesc 2025",
                    Description = "Editia din anul universitar precedent.",
                    Type = ElectionType.Politic,
                    IsAnonymous = true,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(-60),
                    EndsAt = now.AddDays(-53),
                    Options = new List<Option>
                    {
                        new Option { Label = "Ana Popescu" },
                        new Option { Label = "Mihai Ionescu" },
                        new Option { Label = "Diana Stan" }
                    }
                },
                new Election
                {
                    Title = "Cel mai bun produs Q4 2025",
                    Description = "Sondaj comercial intern, editie incheiata.",
                    Type = ElectionType.Comercial,
                    IsAnonymous = false,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(-30),
                    EndsAt = now.AddDays(-23),
                    Options = new List<Option>
                    {
                        new Option { Label = "Produs A" },
                        new Option { Label = "Produs B" }
                    }
                },
                new Election
                {
                    Title = "Alegeri Sef de Grupa",
                    Description = "Vot pentru reprezentantul grupei in acest semestru.",
                    Type = ElectionType.Politic,
                    IsAnonymous = true,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(-2),
                    EndsAt = now.AddDays(5),
                    Options = new List<Option>
                    {
                        new Option { Label = "Andrei Marin" },
                        new Option { Label = "Elena Radu" }
                    }
                },
                new Election
                {
                    Title = "Preferinta locatie team building",
                    Description = "Unde mergem anul acesta?",
                    Type = ElectionType.Comercial,
                    IsAnonymous = false,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(-1),
                    EndsAt = now.AddDays(6),
                    Options = new List<Option>
                    {
                        new Option { Label = "Munte" },
                        new Option { Label = "Mare" },
                        new Option { Label = "Delta Dunarii" }
                    }
                },
                new Election
                {
                    Title = "Cel mai bun proiect al lunii",
                    Description = "Vot intern echipa dezvoltare.",
                    Type = ElectionType.Comercial,
                    IsAnonymous = false,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(-3),
                    EndsAt = now.AddDays(4),
                    Options = new List<Option>
                    {
                        new Option { Label = "Proiect X" },
                        new Option { Label = "Proiect Y" },
                        new Option { Label = "Proiect Z" }
                    }
                },
                new Election
                {
                    Title = "Alegeri Senat Studentesc 2026",
                    Description = "Editie viitoare, votarea nu a inceput.",
                    Type = ElectionType.Politic,
                    IsAnonymous = true,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(10),
                    EndsAt = now.AddDays(17),
                    Options = new List<Option>
                    {
                        new Option { Label = "Radu Constantin" },
                        new Option { Label = "Ioana Vasile" }
                    }
                },
                new Election
                {
                    Title = "Sondaj satisfactie angajati 2026",
                    Description = "Va incepe saptamana viitoare.",
                    Type = ElectionType.Comercial,
                    IsAnonymous = true,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(7),
                    EndsAt = now.AddDays(14),
                    Options = new List<Option>
                    {
                        new Option { Label = "Foarte multumit" },
                        new Option { Label = "Multumit" },
                        new Option { Label = "Nemultumit" }
                    }
                },
                new Election
                {
                    Title = "Cea mai buna idee de imbunatatire",
                    Description = "Propuneri interne pentru optimizarea proceselor.",
                    Type = ElectionType.Comercial,
                    IsAnonymous = false,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(-5),
                    EndsAt = now.AddDays(2),
                    Options = new List<Option>
                    {
                        new Option { Label = "Automatizare rapoarte" },
                        new Option { Label = "Program flexibil" },
                        new Option { Label = "Training suplimentar" }
                    }
                },
                new Election
                {
                    Title = "Alegeri Comitet Studentesc 2024",
                    Description = "Arhiva - editie mai veche, complet incheiata.",
                    Type = ElectionType.Politic,
                    IsAnonymous = true,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(-400),
                    EndsAt = now.AddDays(-393),
                    Options = new List<Option>
                    {
                        new Option { Label = "George Tudor" },
                        new Option { Label = "Maria Enache" }
                    }
                },
                new Election
                {
                    Title = "Vot logo nou echipa",
                    Description = "Alege designul preferat pentru rebranding.",
                    Type = ElectionType.Comercial,
                    IsAnonymous = true,
                    CreatedByUserId = admin.Id,
                    StartsAt = now.AddDays(-1),
                    EndsAt = now.AddDays(3),
                    Options = new List<Option>
                    {
                        new Option { Label = "Varianta A" },
                        new Option { Label = "Varianta B" },
                        new Option { Label = "Varianta C" }
                    }
                }
            };

            db.Elections.AddRange(elections);
            await db.SaveChangesAsync();
        }
    }
}