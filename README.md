# CreatorPay V2

CreatorPay is a creator-to-merchant affiliate commission platform. This repository currently contains Foundation Milestone 1: a buildable clean-architecture backend, test projects, a minimal React frontend, local PostgreSQL configuration, and initial documentation.

## Prerequisites

- .NET 10 SDK
- Node.js and npm
- Docker Desktop (optional for the first build)

## Build and test

```powershell
dotnet restore CreatorPay.slnx
dotnet build CreatorPay.slnx --no-restore
dotnet test CreatorPay.slnx --no-build
npm install --prefix src/CreatorPay.Web
npm run build --prefix src/CreatorPay.Web
```

Run the API with `dotnet run --project src/CreatorPay.Api` and request `GET /health` using the URL printed by ASP.NET Core.

## Local PostgreSQL

Copy `.env.example` to `.env`, replace its development password, and run `docker compose up -d`. Docker is not required to build this milestone.

See [the implementation roadmap](docs/implementation-roadmap.md) for planned future phases.
