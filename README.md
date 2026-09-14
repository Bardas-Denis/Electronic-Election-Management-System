## Getting Started

### Prerequisites

- [Git](https://git-scm.com/)
- [.NET SDK 10.0](https://dotnet.microsoft.com/download)
- [Node.js 20.19+](https://nodejs.org/) (LTS recommended) with npm
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) *(optional — only needed for PostgreSQL)*

### 1. Clone the repo

```bash
git clone https://github.com/Bardas-Denis/Electronic-Election-Management-System.git
cd Electronic-Election-Management-System
```

### 2. Set the JWT signing key (required)

From the backend project directory:

```bash
cd "Electronic Election Management System/Electronic Election Management System"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 64)"
```

On Windows (PowerShell), see [`JWT-CONFIGURATION.md`](./JWT-CONFIGURATION.md) for the equivalent script.

### 3. Run the backend

```bash
dotnet restore
dotnet build
dotnet run
```

Runs on `http://localhost:7104`. Uses **SQLite** by default — no extra setup needed. For PostgreSQL instead, run `docker compose up -d postgres` first and see [`MIGRATIONS.md`](./MIGRATIONS.md).

### 4. Run the frontend

In a separate terminal, from the repo root:

```bash
cd electronic-election-management-system
npm install
npm start
```

Open `http://localhost:4200` — the app will walk you through a first-run setup wizard (database + admin account) automatically.

### 5. (Optional) Run everything with Docker

```bash
docker compose up -d
```

Starts the backend + PostgreSQL together (frontend still run separately with `npm start`).
