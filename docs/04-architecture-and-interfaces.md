# Architecture and Interfaces

<!-- AI AGENT: To fill this document:
1. Start with the tech stack — be specific about versions
2. Design the project structure following clean architecture principles
3. Define dependency rules clearly (who can depend on whom)
4. Describe the request lifecycle end-to-end
5. API outline should list endpoints per module (can be refined later)
6. Deployment section should match the target environment
-->

## 1. Technical stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Language | [e.g., C#] | [e.g., .NET 10] |
| Web framework | [e.g., ASP.NET Core / Next.js / Django] | [version] |
| UI framework | [e.g., Blazor / React / Vue] | [version] |
| Database | [e.g., PostgreSQL / SQL Server] | [version] |
| ORM | [e.g., EF Core / Prisma / SQLAlchemy] | [version] |
| Auth | [e.g., ASP.NET Identity / Auth0 / Keycloak] | [version] |
| Testing | [e.g., NUnit + Playwright / Jest + Cypress] | [version] |
| CI/CD | [e.g., Azure DevOps / GitHub Actions] | — |

## 2. Application structure

### Project layout

```
src/
├── [Project].Domain/          # Entities, interfaces, enums — no dependencies
├── [Project].Service/         # Business logic — depends on Domain only
├── [Project].Data.[Provider]/ # Data access — implements Domain interfaces
├── [Project].Api/             # REST API — depends on Domain, Service
├── [Project].Web/             # UI — depends on Domain, ApiClient
├── [Project].ApiClient/       # Typed HTTP client for the API
└── [Project].Worker/          # Background jobs (optional)

tests/
├── [Project].Tests/               # Unit tests
├── [Project].IntegrationTests/    # Integration tests (DB, API)
└── [Project].E2ETests/            # End-to-end tests (Playwright etc.)
```

### Dependency rules

<!-- CRITICAL: These rules enforce clean architecture. The data layer must be replaceable. -->

```
Api, Web, Worker  ──►  Domain (interfaces)  ◄──  Data.[Provider] (implements)
```

| Project | Can depend on | CANNOT depend on |
|---------|---------------|------------------|
| **Domain** | nothing | anything else |
| **Service** | Domain | Data.*, Api, Web |
| **Data.[Provider]** | Domain | Service, Api, Web |
| **Api** | Domain, Service | — |
| **Web** | Domain, ApiClient | Service, Data.* |
| **ApiClient** | Domain | Service, Data.* |
| **Worker** | Domain, Service | — |

**Key rules**:
1. Never use DbContext or any Data.* types outside of the Data project (except DI registration in `Program.cs`)
2. All data access goes through repository interfaces defined in Domain
3. Service layer depends only on interfaces, never on concrete implementations
4. This allows swapping Data.[Provider] without changing any other code

## 3. Internal interfaces and flows

### Request lifecycle

<!-- Describe how a typical request flows through the system. -->

```
[Client] → [API Gateway/Reverse Proxy]
         → [Middleware: Auth, Tenant Resolution, Logging]
         → [Controller] → [Service] → [Repository Interface]
                                     → [Repository Implementation] → [Database]
         ← [Response with status code and body]
```

### Key interfaces

<!-- List the main interfaces that define the contract between layers. -->

```
// Example repository interface
public interface I[Entity]Repository
{
    Task<[Entity]?> GetByIdAsync(int id);
    Task<IReadOnlyList<[Entity]>> GetAllAsync();
    Task<[Entity]> CreateAsync([Entity] entity);
    Task UpdateAsync([Entity] entity);
    Task DeleteAsync(int id);
}

// Example service interface
public interface I[Entity]Service
{
    Task<[EntityDto]?> GetByIdAsync(int id);
    Task<PagedResult<[EntityDto]>> GetListAsync([FilterDto] filter);
    Task<[EntityDto]> CreateAsync([CreateDto] dto);
    Task<[EntityDto]> UpdateAsync(int id, [UpdateDto] dto);
    Task DeleteAsync(int id);
}
```

## 4. External integrations

| System | Protocol | Direction | Auth | Purpose |
|--------|----------|-----------|------|---------|
| [System name] | [REST / SOAP / gRPC / File] | [In / Out / Both] | [API key / OAuth / mTLS] | [What it does] |

## 5. API outline

<!-- List endpoints per module. This will be refined as implementation progresses. -->

### [Module 1]

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/[entities]` | List with filtering and pagination |
| GET | `/api/[entities]/{id}` | Get by ID |
| POST | `/api/[entities]` | Create |
| PUT | `/api/[entities]/{id}` | Update |
| DELETE | `/api/[entities]/{id}` | Delete |

### [Module 2]

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/[entities]` | List |
| POST | `/api/[entities]` | Create |

<!-- Add more modules as needed. -->

## 6. Deployment and runtime

### Hosting

- **Environment**: [e.g., "IIS on Windows Server / Kubernetes / Vercel"]
- **Topology**: [e.g., "Single deployment, multi-tenant via URL routing"]
- **Database hosting**: [e.g., "Managed PostgreSQL on Azure"]

### CI/CD pipeline

```
[Trigger: push to main/PR]
  → Build
  → Run unit tests
  → Run integration tests
  → Run E2E smoke test
  → Deploy to staging
  → [Manual approval]
  → Deploy to production
```

### Environments

| Environment | URL | Purpose |
|-------------|-----|---------|
| Development | [localhost:PORT] | Local development |
| Staging | [url] | Pre-production testing |
| Production | [url] | Live |
