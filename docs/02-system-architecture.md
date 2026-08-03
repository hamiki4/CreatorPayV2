# System Architecture

CreatorPay uses Clean Architecture with dependencies directed inward:

- **Domain** contains the Milestone 2 core entities/status behavior and will contain later business rules. It has no project dependencies.
- **Application** currently provides dependency-injection seams and will contain use cases and interfaces. It references Domain.
- **Infrastructure** contains the EF Core/Npgsql context, mappings and initial migration and will implement external integrations. It references Application and Domain.
- **API** is the HTTP entry point and composition root. It references Application and Infrastructure.
- **Web** is an independent React and TypeScript client.

PostgreSQL and Entity Framework Core are the persistence technologies. Milestone 2 introduced the initial data model, database context and migration; the financial, transaction, notification and payout models described in the Milestone 3 blueprint remain future work.

Tests mirror the backend layers. Architecture references remain acyclic, and the API currently exposes only `GET /health` plus development OpenAPI metadata.
