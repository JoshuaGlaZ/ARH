# Access Request Hub (ARH)

**Enterprise Fullstack Engineering Assessment**

Access Request Hub is an internal application access request and approval management MVP built with .NET, Blazor, and MySQL.

The application provides centralized access requests, risk-based approval routing, server-side authorization, idempotency, optimistic concurrency, and audit history.

## 1. Technology Stack

| Component | Technology |
|---|---|
| Framework | .NET 9 |
| Frontend | Blazor Web App |
| Render Mode | Interactive Server |
| Backend | ASP.NET Core Minimal APIs |
| Database | MySQL |
| ORM | Entity Framework Core 9 |
| MySQL Provider | Pomelo.EntityFrameworkCore.MySql |
| Authentication | Development Cookie Authentication |
| Testing | xUnit |

## 2. Project Structure

```text
ARH/
├── Components/
│   ├── Layout/
│   ├── Pages/
│   └── Shared/
├── Domain/
│   ├── Entities/
│   └── Enums/
├── Data/
│   ├── Configurations/
│   └── Migrations/
├── DTOs/
├── Endpoints/
├── Services/
├── Authentication/
├── Middleware/
├── Program.cs
├── appsettings.json
└── ARH.csproj

ARH.Tests/
├── Unit/
└── Integration/
```

## 3. Prerequisites

Install:

- .NET 9 SDK.
- MySQL 8.x.
- Laragon (recommended for local MySQL).
- Visual Studio 2022 or Visual Studio Code.
- Git.

Install the EF Core CLI:

```powershell
dotnet tool install --global dotnet-ef --version 9.0.0
```

If already installed:

```powershell
dotnet tool update --global dotnet-ef --version 9.0.0
```

## 4. Database Setup

Start MySQL through Laragon.

Create the development database:

```sql
CREATE DATABASE arh_db
CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;
```

Configure the connection string in `appsettings.Development.json` or .NET User Secrets.

Example for a default Laragon installation without a MySQL password:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3306;Database=arh_db;User=root;Password=;"
  }
}
```

Adjust the username, password, and port to match the local MySQL installation.

**Never commit real database credentials to Git.**

## 5. Database Migration and Seeding

From the application project directory:

```powershell
cd ARH
```

To create a new migration when the model changes:

```powershell
dotnet ef migrations add MigrationName --output-dir Data/Migrations
```

To apply existing migrations:

```powershell
dotnet ef database update
```

The application database seeder is executed during application startup.

It creates the five demo users and two application records if they do not already exist.

## 6. Demo Users

| Email | Role | Responsibility |
|---|---|---|
| alice@example.local | Requester | Creates and tracks requests |
| bob@example.local | Manager | Approves Alice's requests |
| carol@example.local | SystemOwner | Owns CRM |
| dana@example.local | SystemOwner | Owns Finance Portal |
| erin@example.local | AdminAuditor | Views all requests and audit logs |

No real passwords are required for the development demo login.

Demo authentication is intended for local development only.

## 7. Run the Application

From the repository root:

```powershell
dotnet build ARH/ARH.csproj
```

Run the application:

```powershell
dotnet run --project ARH/ARH.csproj --launch-profile https
```

Default development URLs:

- HTTPS: https://localhost:7142
- HTTP: http://localhost:5149

Open the login page:

`https://localhost:7142/login`

Select a demo user to simulate authentication.

## 8. Run Automated Tests

Integration tests use a separate MySQL database named `arh_test`.

Create it in Laragon:

```sql
CREATE DATABASE arh_test
CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;
```

Ensure the test connection string in `MySqlFixture.cs` matches the local MySQL configuration.

Run all tests:

```powershell
dotnet test ARH.Tests/ARH.Tests.csproj
```

Run workflow unit tests:

```powershell
dotnet test ARH.Tests/ARH.Tests.csproj --filter FullyQualifiedName~WorkflowServiceTests
```

Run MySQL smoke tests:

```powershell
dotnet test ARH.Tests/ARH.Tests.csproj --filter FullyQualifiedName~MySqlSmokeTests
```

Run AccessRequestService integration tests:

```powershell
dotnet test ARH.Tests/ARH.Tests.csproj --filter FullyQualifiedName~AccessRequestServiceTests
```

**Important:** Integration tests reset data in `arh_test`. Never configure the test fixture to use the development database `arh_db`.

## 9. Business Rules

### Risk Classification

A request is high-risk when:

`Environment == Production OR AccessLevel == Admin`

### Approval Workflow

**Non-High-Risk**

```text
Requester
    |
    v
Manager Approval
    |
    v
Approved
```

**High-Risk**

```text
Requester
    |
    v
Manager Approval
    |
    v
System Owner Approval
    |
    v
Approved
```

**Rejection**

```text
Manager / System Owner
    |
    v
Rejected
```

Rejection requires a reason.

Approved and Rejected are terminal states.

## 10. Demo Scenarios

### Scenario 1 — Standard Request

1. Login as Alice.
2. Create CRM / NonProduction / Read.
3. Login as Bob.
4. Open Approval Inbox.
5. Approve the request.
6. Verify status becomes Approved.

### Scenario 2 — Production Request

1. Login as Alice.
2. Create CRM / Production / Read.
3. Login as Bob.
4. Approve the request.
5. Login as Carol.
6. Approve the request.
7. Verify status becomes Approved.

### Scenario 3 — Admin Access

1. Login as Alice.
2. Create Finance Portal / NonProduction / Admin.
3. Login as Bob.
4. Approve the request.
5. Login as Dana.
6. Approve the request.
7. Verify status becomes Approved.

### Scenario 4 — Rejection

1. Login as Alice.
2. Create an access request.
3. Login as the assigned approver.
4. Reject with a reason.
5. Verify status and rejection reason.

### Scenario 5 — Audit Verification

1. Login as Erin.
2. Open Audit Logs.
3. Review request creation and approval/rejection events.
4. Verify status transitions and version history.

## 11. API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/requests/` | Create request |
| GET | `/api/requests/my` | My requests |
| GET | `/api/requests/inbox` | Approval inbox |
| GET | `/api/requests/{id}` | Request details |
| POST | `/api/requests/{id}/approve` | Approve |
| POST | `/api/requests/{id}/reject` | Reject |
| GET | `/api/requests/{id}/history` | Audit history |
| GET | `/api/audit-events` | Global audit listing |

API mutation endpoints use authenticated identity and server-side authorization. Cookie-authenticated HTTP mutations must also pass antiforgery validation.

## 12. Security and Data Integrity

The application implements the following controls:

- Server-side authorization.
- Direct-report Manager validation.
- Application System Owner validation.
- Anti-self-approval.
- ClientRequestId idempotency.
- Payload hash validation.
- Version-based optimistic concurrency.
- Atomic state transition and audit insertion.
- Terminal state protection.
- PolicyVersion snapshot.

## 13. Scope Limitations

The following are outside Phase 1:

- External OAuth/OIDC integration.
- Real email notifications.
- Automated access provisioning.
- Dynamic approval policy administration.
- Multiple System Owners per application.
- Microservices and cloud deployment.

## 14. Project Documentation

| File | Description |
|---|---|
| PLAN.md | Project scope and implementation plan |
| REVIEW.md | Engineering and production-readiness review |
| AI_USAGE.md | AI-assisted development record |
| INTEGRITY.md | Engineering integrity declaration |
| README.md | Setup, execution, testing, and demo guide |

## 15. Final Submission

Before submission:

- Verify the application runs from a clean setup.
- Verify migrations and seeding.
- Run all automated tests.
- Complete the critical browser demo flows.
- Review security and known limitations.
- Ensure no credentials or secrets are committed.
- Verify Git history and required assessment tags.

**Project:** Access Request Hub (ARH)

**Purpose:** Demonstrate an end-to-end access request, approval/rejection, and audit workflow.