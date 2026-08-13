Database Migrations


Switching Provider

Set in appsettings.Development.json (do not commit this change):

    { "Database": { "Provider": "Postgres" } }

- Sqlite  = no Docker needed, this is the default
- Postgres = requires Docker running (docker compose up -d)


---


Adding a Migration

When you change the model in ElectionDbContext, run both commands:

    dotnet ef migrations add <Name> --context SqliteAppDbContext   --output-dir Migrations
    dotnet ef migrations add <Name> --context PostgresAppDbContext --output-dir Migrations/Postgres

Always commit both migration files and their updated snapshots together.


---


Applying Migrations

Automatic: db.Database.Migrate() runs on every app startup.

Manual / CI:

    dotnet ef database update --context SqliteAppDbContext
    dotnet ef database update --context PostgresAppDbContext


---


CHECK Constraint SQL Rule

Always use double-quoted column names in ElectionDbContext.cs.
Postgres lowercases unquoted identifiers, which causes a column-not-found error.

    GOOD:  .HasCheckConstraint("CK_Name", "\"ColumnName\" IS NOT NULL")
    BAD:   .HasCheckConstraint("CK_Name", "ColumnName IS NOT NULL")


---


Reset Postgres (Wipe and Reseed)

    docker compose down -v    (deletes the volume)
    docker compose up -d      (fresh container)
    dotnet run                (auto-migrates and seeds)

For SQLite: delete election.db from the project folder, then dotnet run.


---


Notes

- Model snapshots (ElectionDbContextModelSnapshot.cs, PostgresAppDbContextModelSnapshot.cs)
  are auto-generated. Never edit them manually.

- Repositories inject ElectionDbContext and are unaffected by provider switches.
  Program.cs handles the DI wiring internally.
