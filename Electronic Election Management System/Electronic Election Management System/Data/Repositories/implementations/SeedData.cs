using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using Electronic_Election_Management_System.Services;

namespace Electronic_Election_Management_System.Data
{
    // Seeds predefined scoring schemes and initial test data when the database is empty.
    // Administrator accounts are provisioned exclusively through the setup form (POST /api/setup/save).
    public static class SeedData
    {

        public static async Task EnsureScoringSchemesAsync(ElectionDbContext db)
        {
            if (await db.ScoringSchemes.AnyAsync(s => s.IsPredefined))
            {
                return;
            }

            db.ScoringSchemes.AddRange(
                new ScoringScheme
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Name = "Eurovision",
                    Points = new List<int> { 12, 10, 8, 7, 6, 5, 4, 3, 2, 1 },
                    IsLinear = false,
                    IsPredefined = true
                },
                new ScoringScheme
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    Name = "Formula 1",
                    Points = new List<int> { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 },
                    IsLinear = false,
                    IsPredefined = true
                },
                new ScoringScheme
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                    Name = "Linear",
                    Points = new List<int>(),
                    IsLinear = true,
                    IsPredefined = true
                }
            );

            await db.SaveChangesAsync();
        }

        // Seeds 100 test voters (with UserDetails profiles), a handful of
        // non-role labels, 20 elections (5 expired / 5 upcoming / 10 active,
        // including one closed election and one multi-question survey), and
        // realistic voting activity limited to voters aged 60-90, plus the
        // supporting VoterChangeRecord and AuditLog rows.
        // Runs only once - skips if elections already exist.
        public static async Task EnsureTestDataAsync(ElectionDbContext db)
        {
            bool alreadySeeded = await db.Elections.AnyAsync();
            if (alreadySeeded)
            {
                return;
            }

            var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);
            if (admin is null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var rng = new Random(20260728);

            // ------------------------------------------------------------
            // 1. Voters + UserDetails
            // ------------------------------------------------------------
            string[] counties = { "Bucuresti", "Cluj", "Timis", "Iasi", "Brasov" };
            string[] cities = { "Bucuresti", "Cluj-Napoca", "Timisoara", "Iasi", "Brasov" };
            string[] departments = { "IT", "HR", "Finance", "Sales", "Marketing" };
            string[] jobTitles = { "Specialist", "Analyst", "Coordonator", "Consultant", "Referent" };
            string[] firstNamesM = { "Andrei", "Mihai", "Radu", "George", "Stefan", "Cristian", "Alexandru", "Ionut", "Bogdan", "Florin" };
            string[] firstNamesF = { "Ana", "Elena", "Maria", "Diana", "Ioana", "Andreea", "Cristina", "Gabriela", "Larisa", "Simona" };
            string[] lastNames = { "Popescu", "Ionescu", "Stan", "Marin", "Radu", "Constantin", "Vasile", "Tudor", "Enache", "Dumitru" };

            bool anyTestUsers = await db.Users.AnyAsync(u => u.Email.EndsWith("@test.com"));

            var voters = new List<User>();
            if (!anyTestUsers)
            {
                var testPasswordHash = PasswordHasher.Hash("Test123!");

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
                await db.SaveChangesAsync(); // need Ids before creating UserDetails

                var userDetailsList = new List<UserDetails>();
                for (int i = 0; i < voters.Count; i++)
                {
                    int idx = i + 1;

                    // Ages cycle across 18-90 (73 distinct values) so we get a
                    // solid pool of voters in the 60-90 range for voting.
                    int age = 18 + ((idx - 1) % 73);
                    var birthDate = DateOnly.FromDateTime(now.AddYears(-age).AddDays(rng.Next(-180, 180)));

                    bool isMale = idx % 2 == 0;
                    int countyIdx = idx % counties.Length;
                    int deptIdx = idx % departments.Length;

                    string firstName = isMale
                        ? firstNamesM[idx % firstNamesM.Length]
                        : firstNamesF[idx % firstNamesF.Length];
                    string lastName = lastNames[idx % lastNames.Length];

                    string genderDigit = birthDate.Year >= 2000
                        ? (isMale ? "5" : "6")
                        : (isMale ? "1" : "2");
                    string fakeCnp = $"{genderDigit}{birthDate.Year % 100:D2}{birthDate.Month:D2}{birthDate.Day:D2}{(countyIdx + 1):D2}{idx:D3}";

                    userDetailsList.Add(new UserDetails
                    {
                        UserId = voters[i].Id,
                        Cnp = fakeCnp,
                        BirthDate = birthDate,
                        FullName = $"{firstName} {lastName}",
                        ResidenceCounty = counties[countyIdx],
                        ResidenceCity = cities[countyIdx],
                        ResidenceAddress = $"Str. Exemplu nr. {idx}",
                        Citizenship = "Romana",
                        Gender = isMale ? "M" : "F",
                        WorkEmail = $"user{idx}@testcorp.com",
                        EmployeeId = $"EMP{idx:D4}",
                        Department = departments[deptIdx],
                        JobTitle = jobTitles[deptIdx],
                        Company = "Test Corp",
                        UpdatedAt = now
                    });
                }

                db.UserDetails.AddRange(userDetailsList);
                await db.SaveChangesAsync();
            }
            else
            {
                voters = await db.Users.Where(u => u.Role == UserRole.Voter).ToListAsync();
            }

            var userDetailsByUserId = await db.UserDetails
                .Where(ud => voters.Select(v => v.Id).Contains(ud.UserId))
                .ToDictionaryAsync(ud => ud.UserId);

            // Voters whose email index falls in [60, 90] - i.e. user60@test.com
            // through user90@test.com - since test users are created incrementally.
            int? EmailIndexOf(string email)
            {
                var atIdx = email.IndexOf('@');
                if (atIdx < 0 || !email.StartsWith("user")) return null;
                var numPart = email.Substring(4, atIdx - 4);
                return int.TryParse(numPart, out var n) ? n : null;
            }

            var seniorVoters = voters
                .Where(v => EmailIndexOf(v.Email) is >= 60 and <= 90)
                .OrderBy(v => EmailIndexOf(v.Email))
                .ToList();

            // ------------------------------------------------------------
            // 2. Labels (no role-type labels - department/geography only) + UserLabels
            // ------------------------------------------------------------
            bool anyLabels = await db.Labels.AnyAsync();
            List<Label> labels;
            if (!anyLabels)
            {
                labels = new List<Label>
                {
                    new Label { Name = "IT", Category = "Department", CreatedAt = now },
                    new Label { Name = "HR", Category = "Department", CreatedAt = now },
                    new Label { Name = "Finance", Category = "Department", CreatedAt = now },
                    new Label { Name = "Bucuresti", Category = "Geography", CreatedAt = now },
                    new Label { Name = "Cluj-Napoca", Category = "Geography", CreatedAt = now }
                };
                db.Labels.AddRange(labels);
                await db.SaveChangesAsync();

                var userLabels = new List<UserLabel>();
                for (int i = 0; i < voters.Count; i++)
                {
                    var voter = voters[i];
                    int labelCount = rng.Next(1, 3); // 1 or 2 labels
                    var chosen = labels.OrderBy(_ => rng.Next()).Take(labelCount);
                    foreach (var label in chosen)
                    {
                        userLabels.Add(new UserLabel
                        {
                            UserId = voter.Id,
                            LabelId = label.Id,
                            AssignedBy = admin.Id,
                            AssignedAt = now
                        });
                    }
                }
                db.UserLabels.AddRange(userLabels);
                await db.SaveChangesAsync();
            }

            // ------------------------------------------------------------
            // 3. Elections: 5 expired, 5 starting in ~2 months, 10 active
            //    (running for roughly the next 5 months)
            // ------------------------------------------------------------
            var elections = new List<Election>();

            // -- 5 expired elections --
            elections.Add(new Election
            {
                Title = "Alegeri Consiliul Studentesc 2025",
                Description = "Editia din anul universitar precedent.",
                Type = ElectionType.Politic,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-60),
                EndsAt = now.AddDays(-53),
                Options = new List<Option>
                {
                    new Option { Label = "Ana Popescu" },
                    new Option { Label = "Mihai Ionescu" },
                    new Option { Label = "Diana Stan" }
                }
            });
            elections.Add(new Election
            {
                Title = "Alegeri Comitet Studentesc 2024",
                Description = "Arhiva - editie mai veche, complet incheiata.",
                Type = ElectionType.Politic,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-45),
                EndsAt = now.AddDays(-38),
                Options = new List<Option>
                {
                    new Option { Label = "George Tudor" },
                    new Option { Label = "Maria Enache" }
                }
            });
            elections.Add(new Election
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
            });
            elections.Add(new Election
            {
                Title = "Cea mai buna idee de imbunatatire T3",
                Description = "Propuneri interne pentru optimizarea proceselor, editie incheiata.",
                Type = ElectionType.Comercial,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-20),
                EndsAt = now.AddDays(-13),
                Options = new List<Option>
                {
                    new Option { Label = "Automatizare rapoarte" },
                    new Option { Label = "Program flexibil" },
                    new Option { Label = "Training suplimentar" }
                }
            });
            elections.Add(new Election
            {
                Title = "Vot logo nou echipa - runda 1",
                Description = "Alege designul preferat pentru rebranding, editie incheiata.",
                Type = ElectionType.Comercial,
                IsAnonymous = true,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-10),
                EndsAt = now.AddDays(-3),
                Options = new List<Option>
                {
                    new Option { Label = "Varianta A" },
                    new Option { Label = "Varianta B" },
                    new Option { Label = "Varianta C" }
                }
            });

            // -- 5 elections starting in ~2 months --
            elections.Add(new Election
            {
                Title = "Alegeri Senat Studentesc 2026",
                Description = "Editie viitoare, votarea nu a inceput.",
                Type = ElectionType.Politic,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(55),
                EndsAt = now.AddDays(62),
                Options = new List<Option>
                {
                    new Option { Label = "Radu Constantin" },
                    new Option { Label = "Ioana Vasile" }
                }
            });
            elections.Add(new Election
            {
                Title = "Sondaj satisfactie angajati - val 2",
                Description = "Va incepe peste aproximativ doua luni.",
                Type = ElectionType.Comercial,
                IsAnonymous = true,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(58),
                EndsAt = now.AddDays(65),
                Options = new List<Option>
                {
                    new Option { Label = "Foarte multumit" },
                    new Option { Label = "Multumit" },
                    new Option { Label = "Nemultumit" }
                }
            });
            elections.Add(new Election
            {
                Title = "Alegeri Sef de Grupa - semestrul urmator",
                Description = "Vot pentru reprezentantul grupei, editie viitoare.",
                Type = ElectionType.Politic,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(60),
                EndsAt = now.AddDays(67),
                Options = new List<Option>
                {
                    new Option { Label = "Andrei Marin" },
                    new Option { Label = "Elena Radu" }
                }
            });
            elections.Add(new Election
            {
                Title = "Preferinta locatie team building 2027",
                Description = "Unde mergem anul viitor? Editie viitoare.",
                Type = ElectionType.Comercial,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(63),
                EndsAt = now.AddDays(70),
                Options = new List<Option>
                {
                    new Option { Label = "Munte" },
                    new Option { Label = "Mare" },
                    new Option { Label = "Delta Dunarii" }
                }
            });
            elections.Add(new Election
            {
                Title = "Vot logo nou echipa - runda 2",
                Description = "A doua runda de rebranding, editie viitoare.",
                Type = ElectionType.Comercial,
                IsAnonymous = true,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(65),
                EndsAt = now.AddDays(72),
                Options = new List<Option>
                {
                    new Option { Label = "Varianta D" },
                    new Option { Label = "Varianta E" }
                }
            });

            // -- 10 active elections (already started, running ~1-5 months) --
            elections.Add(new Election
            {
                Title = "Cel mai bun proiect al lunii",
                Description = "Vot intern echipa dezvoltare.",
                Type = ElectionType.Comercial,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-1),
                EndsAt = now.AddDays(150),
                Options = new List<Option>
                {
                    new Option { Label = "Proiect X" },
                    new Option { Label = "Proiect Y" },
                    new Option { Label = "Proiect Z" }
                }
            });
            elections.Add(new Election
            {
                Title = "Cea mai buna idee de imbunatatire T4",
                Description = "Propuneri interne pentru optimizarea proceselor.",
                Type = ElectionType.Comercial,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-2),
                EndsAt = now.AddDays(140),
                Options = new List<Option>
                {
                    new Option { Label = "Automatizare rapoarte" },
                    new Option { Label = "Program flexibil" },
                    new Option { Label = "Training suplimentar" }
                }
            });
            elections.Add(new Election
            {
                Title = "Alegeri Consiliul Studentesc 2026",
                Description = "Editie curenta, votarea este deschisa.",
                Type = ElectionType.Politic,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-3),
                EndsAt = now.AddDays(130),
                Options = new List<Option>
                {
                    new Option { Label = "Ana Popescu" },
                    new Option { Label = "Mihai Ionescu" }
                }
            });
            elections.Add(new Election
            {
                Title = "Preferinta locatie team building 2026",
                Description = "Unde mergem anul acesta?",
                Type = ElectionType.Comercial,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-4),
                EndsAt = now.AddDays(120),
                Options = new List<Option>
                {
                    new Option { Label = "Munte" },
                    new Option { Label = "Mare" },
                    new Option { Label = "Delta Dunarii" }
                }
            });
            elections.Add(new Election
            {
                Title = "Alegeri Sef de Grupa",
                Description = "Vot pentru reprezentantul grupei in acest semestru.",
                Type = ElectionType.Politic,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-5),
                EndsAt = now.AddDays(110),
                Options = new List<Option>
                {
                    new Option { Label = "Andrei Marin" },
                    new Option { Label = "Elena Radu" }
                }
            });
            elections.Add(new Election
            {
                Title = "Vot logo nou echipa",
                Description = "Alege designul preferat pentru rebranding.",
                Type = ElectionType.Comercial,
                IsAnonymous = true,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-6),
                EndsAt = now.AddDays(100),
                Options = new List<Option>
                {
                    new Option { Label = "Varianta A" },
                    new Option { Label = "Varianta B" },
                    new Option { Label = "Varianta C" }
                }
            });
            elections.Add(new Election
            {
                Title = "Alegeri Senat Studentesc - mandat curent",
                Description = "Editie curenta, votarea este deschisa.",
                Type = ElectionType.Politic,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-7),
                EndsAt = now.AddDays(90),
                Options = new List<Option>
                {
                    new Option { Label = "Radu Constantin" },
                    new Option { Label = "Ioana Vasile" }
                }
            });

            // Closed election with an explicit invitee list drawn from existing users.
            var closedElection = new Election
            {
                Title = "Comitet de conducere - vot restrans",
                Description = "Editie inchisa, deschisa doar persoanelor invitate.",
                Type = ElectionType.Politic,
                IsAnonymous = false,
                IsClosed = true,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-8),
                EndsAt = now.AddDays(75),
                Options = new List<Option>
                {
                    new Option { Label = "Continuitate" },
                    new Option { Label = "Schimbare" }
                }
            };
            elections.Add(closedElection);

            elections.Add(new Election
            {
                Title = "Sondaj satisfactie angajati 2026",
                Description = "Editie curenta, votarea este deschisa.",
                Type = ElectionType.Comercial,
                IsAnonymous = true,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-9),
                EndsAt = now.AddDays(60),
                Options = new List<Option>
                {
                    new Option { Label = "Foarte multumit" },
                    new Option { Label = "Multumit" },
                    new Option { Label = "Nemultumit" }
                }
            });

            // Multi-question survey (uses ElectionQuestion instead of direct Options).
            var multiQuestionElection = new Election
            {
                Title = "Sondaj cultura organizationala 2026",
                Description = "Sondaj cu mai multe intrebari despre cultura companiei.",
                Type = ElectionType.Comercial,
                IsAnonymous = false,
                CreatedByUserId = admin.Id,
                StartsAt = now.AddDays(-10),
                EndsAt = now.AddDays(45),
                Options = new List<Option>(),
                Questions = new List<ElectionQuestion>
                {
                    new ElectionQuestion
                    {
                        Text = "Cat de multumit esti de comunicarea interna?",
                        DisplayOrder = 1,
                        Options = new List<Option>
                        {
                            new Option { Label = "Foarte multumit" },
                            new Option { Label = "Multumit" },
                            new Option { Label = "Nemultumit" }
                        }
                    },
                    new ElectionQuestion
                    {
                        Text = "Ce beneficiu ti-ar placea sa vezi adaugat?",
                        DisplayOrder = 2,
                        Options = new List<Option>
                        {
                            new Option { Label = "Zile libere suplimentare" },
                            new Option { Label = "Abonament sala" },
                            new Option { Label = "Buget dezvoltare personala" }
                        }
                    }
                }
            };
            elections.Add(multiQuestionElection);

            // Every direct Option needs its parent ElectionId; every Option that
            // belongs to a question also needs ElectionId set alongside QuestionId.
            foreach (var question in multiQuestionElection.Questions)
            {
                foreach (var option in question.Options)
                {
                    option.ElectionId = multiQuestionElection.Id;
                }
            }

            db.Elections.AddRange(elections);
            await db.SaveChangesAsync(); // need Ids for Options/Questions before voting

            // ------------------------------------------------------------
            // 4. Votes: only for voters aged 60-90, only on elections that
            //    have already started (5 expired + 10 active = 15).
            // ------------------------------------------------------------
            var startedElections = elections.Where(e => e.StartsAt <= now).ToList();

            var closedInvitees = seniorVoters
                .OrderBy(_ => rng.Next())
                .Take(Math.Min(15, seniorVoters.Count))
                .ToList();

            var voteTokens = new List<VoteToken>();
            var votes = new List<Vote>();
            var declarations = new List<VoterDeclaration>();
            var auditLogs = new List<AuditLog>();

            // One audit log per election creation.
            foreach (var election in elections)
            {
                auditLogs.Add(new AuditLog
                {
                    UserId = admin.Id,
                    ElectionId = election.Id,
                    Action = "ElectionCreated",
                    Timestamp = election.CreatedAt
                });
            }

            foreach (var election in startedElections)
            {
                // The closed election only allows its invited senior voters;
                // every other started election draws from the full senior pool.
                var eligibleVoters = election.Id == closedElection.Id
                    ? closedInvitees
                    : seniorVoters.OrderBy(_ => rng.Next()).Take(Math.Min(20, seniorVoters.Count)).ToList();

                // Resolve which option "buckets" to vote in: either the
                // election's direct options, or - for the multi-question
                // survey - one option per question.
                List<List<Option>> optionGroups = election.Id == multiQuestionElection.Id
                    ? multiQuestionElection.Questions.OrderBy(q => q.DisplayOrder).Select(q => q.Options.ToList()).ToList()
                    : new List<List<Option>> { election.Options.ToList() };

                if (optionGroups.All(g => g.Count == 0))
                {
                    continue; // nothing to vote on (shouldn't happen, but stay safe)
                }

                foreach (var voter in eligibleVoters)
                {
                    var token = new VoteToken
                    {
                        UserId = voter.Id,
                        ElectionId = election.Id,
                        IsUsed = true,
                        IssuedAt = election.StartsAt
                    };
                    voteTokens.Add(token);

                    var castAt = election.StartsAt.AddHours(rng.Next(1, 48));
                    User? declarationSourceUser = election.IsAnonymous ? null : voter;
                    userDetailsByUserId.TryGetValue(voter.Id, out var voterDetails);

                    foreach (var optionGroup in optionGroups)
                    {
                        if (optionGroup.Count == 0) continue;
                        var chosenOption = optionGroup[rng.Next(optionGroup.Count)];

                        var vote = new Vote
                        {
                            OptionId = chosenOption.Id,
                            VoteTokenId = election.IsAnonymous ? token.Id : null,
                            UserId = election.IsAnonymous ? null : voter.Id,
                            CastAt = castAt
                        };
                        votes.Add(vote);

                        if (!election.IsAnonymous && voterDetails is not null)
                        {
                            var declaration = new VoterDeclaration
                            {
                                Vote = vote,
                                CreatedAt = castAt
                            };

                            if (election.Type == ElectionType.Politic)
                            {
                                declaration.Cnp = voterDetails.Cnp;
                                declaration.FullName = voterDetails.FullName;
                                declaration.ResidenceCounty = voterDetails.ResidenceCounty;
                                declaration.ResidenceAddress = voterDetails.ResidenceAddress;
                                declaration.ResidenceCity = voterDetails.ResidenceCity;
                                declaration.Citizenship = voterDetails.Citizenship;
                                declaration.BirthDate = voterDetails.BirthDate;
                                declaration.Gender = voterDetails.Gender;
                            }
                            else
                            {
                                declaration.EmployeeId = voterDetails.EmployeeId;
                                declaration.Department = voterDetails.Department;
                                declaration.WorkEmail = voterDetails.WorkEmail;
                                declaration.JobTitle = voterDetails.JobTitle;
                                declaration.Company = voterDetails.Company;
                            }

                            declarations.Add(declaration);
                        }

                        auditLogs.Add(new AuditLog
                        {
                            UserId = voter.Id,
                            ElectionId = election.Id,
                            Action = "Voted",
                            Timestamp = castAt
                        });
                    }
                }
            }

            db.VoteTokens.AddRange(voteTokens);
            db.Votes.AddRange(votes);
            db.VoterDeclarations.AddRange(declarations);

            // ------------------------------------------------------------
            // 5. Closed-election invitations, drawn from existing users.
            // ------------------------------------------------------------
            var invitations = closedInvitees.Select(v => new ElectionInvitation
            {
                ElectionId = closedElection.Id,
                UserId = v.Id,
                Email = v.Email,
                Method = ElectionInvitationMethod.Manual,
                CreatedAt = now
            }).ToList();
            db.ElectionInvitations.AddRange(invitations);

            // ------------------------------------------------------------
            // 6. VoterChangeRecords for 2 senior voters who voted in the
            //    same election (1 edit each, within the allowed budget).
            // ------------------------------------------------------------
            var changeRecords = new List<VoterChangeRecord>();
            if (closedInvitees.Count >= 2)
            {
                // closedInvitees are guaranteed to have voted in closedElection above.
                foreach (var voter in closedInvitees.Take(2))
                {
                    changeRecords.Add(new VoterChangeRecord
                    {
                        UserId = voter.Id,
                        ElectionId = closedElection.Id,
                        ChangeCount = 1
                    });
                }
            }
            db.VoterChangeRecords.AddRange(changeRecords);

            db.AuditLogs.AddRange(auditLogs);

            await db.SaveChangesAsync();
        }
    }
}