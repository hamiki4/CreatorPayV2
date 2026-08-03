# CreatorPay V2

CreatorPay is a creator-to-merchant affiliate commission platform. Milestone 2 provides the core domain model and PostgreSQL persistence foundation while preserving the clean-architecture solution established in Milestone 1.

## Prerequisites

- .NET 10 SDK and `dotnet-ef`
- Node.js and npm
- Docker Desktop for local PostgreSQL (optional for metadata-only tests)

## Build and test

```powershell
dotnet restore CreatorPay.slnx
dotnet build CreatorPay.slnx
dotnet test CreatorPay.slnx
npm run build --prefix src/CreatorPay.Web
```

Infrastructure tests validate the Npgsql EF model metadata without substituting SQLite. Container-backed PostgreSQL integration tests are deferred to a later milestone.

## Local PostgreSQL and migrations

Copy `.env.example` to `.env`, replace the placeholder password, export `ConnectionStrings__CreatorPayDatabase` for the API/EF CLI, then start PostgreSQL:

```powershell
docker compose up -d
$env:ConnectionStrings__CreatorPayDatabase = "Host=localhost;Port=5432;Database=CreatorPayV2Db;Username=creatorpay;Password=<local-password>"
dotnet ef migrations list --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api
dotnet ef database update --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api
```

Migrations are deliberately not applied during API startup. Run the API with `dotnet run --project src/CreatorPay.Api`; the only business-neutral endpoint remains `GET /health`.

See [database design](docs/03-database-design.md), [domain model](docs/04-domain-model.md), and the [implementation roadmap](docs/implementation-roadmap.md).
