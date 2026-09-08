Database Migrations


Switching Provider

Set in appsettings.Development.json (do not commit this change):

    { "Database": { "Provider": "Postgres" } }

- Sqlite  = no Docker needed, this is the default
- Postgres = requires Docker running (docker compose up -d)


---


Adding a Migration

Migrations live with the database provider that owns them, not in the API project. Each provider
is a plugin under Plugins/, carrying its own EF package and its own migration set.

From the solution folder, run both commands:

    dotnet ef migrations add <Name> \
        --project "Plugins/Eems.Providers.Sqlite/Eems.Providers.Sqlite.csproj" \
        --startup-project "Plugins/Eems.Providers.Sqlite/Eems.Providers.Sqlite.csproj" \
        --output-dir Migrations

    dotnet ef migrations add <Name> \
        --project "Plugins/Eems.Providers.Postgres/Eems.Providers.Postgres.csproj" \
        --startup-project "Plugins/Eems.Providers.Postgres/Eems.Providers.Postgres.csproj" \
        --output-dir Migrations

The model itself still lives in Electronic Election Management System.Data - that is what you
edit. The providers only carry the generated migrations.

Always commit both migration files and their updated snapshots together.


---


Removing a Migration

    dotnet ef migrations remove --project <provider csproj> --startup-project <same> [--force]

Removing checks whether the migration was already applied, so it opens a real connection. For
Postgres that means a reachable server: start it with docker compose up -d postgres, or point
the design-time factory elsewhere with EEMS_DESIGNTIME_POSTGRES. Adding a migration never
connects, so it needs neither.

Use --force to skip the check when the database is not available.


---


Applying Migrations

Automatic: db.Database.Migrate() runs on every app startup, using whichever provider plugin
data/dbconfig.json names.

Manual / CI:

    dotnet ef database update --project <provider csproj> --startup-project <same>


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

- Model snapshots are auto-generated and live beside their migrations, inside each provider
  plugin. Never edit them manually.

- A provider is only available if its assembly is in the plugin folder. Deleting it removes that
  database from the setup screen, and the application refuses to start if dbconfig.json names a
  provider that is not installed.

- Repositories inject ElectionDbContext and are unaffected by provider switches.
  Program.cs handles the DI wiring internally.
