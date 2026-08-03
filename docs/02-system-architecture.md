# System Architecture

CreatorPay uses Clean Architecture with dependencies directed inward:

- **Domain** will contain core business rules and entities. It has no project dependencies.
- **Application** will contain use cases and interfaces. It references Domain.
- **Infrastructure** will implement persistence and external integrations. It references Application and Domain.
- **API** is the HTTP entry point and composition root. It references Application and Infrastructure.
- **Web** is an independent React and TypeScript client.

PostgreSQL and Entity Framework Core are the planned persistence technologies. Foundation Milestone 1 prepares local PostgreSQL through Docker but intentionally does not introduce a data model or database context.

Tests mirror the backend layers. Architecture references remain acyclic, and the API currently exposes only `GET /health` plus development OpenAPI metadata.
