# PLAN.md — Access Request Hub (Phase 1)

## 1. Problem Understanding & Business Rules

### 1.1 Scope & Demo Users

Membangun MVP Access Request Hub (ARH) sebagai *single source of truth* untuk permohonan akses internal, approval berbasis risiko, integritas data, dan audit trail.

**Demo Users:**
- `alice@example.local`: Requester (Reports to Bob).
- `bob@example.local`: Manager Alice.
- `carol@example.local`: System Owner CRM.
- `dana@example.local`: System Owner Finance Portal.
- `erin@example.local`: Admin/Auditor (view-only semua request dan audit, bukan approver).

**Master Data:**
- Applications: `CRM` (Owner: Carol), `Finance Portal` (Owner: Dana).
- Environments: `NonProduction`, `Production`.
- Access Levels: `Read`, `Admin`.
- PolicyVersion: `v1`.

### 1.2 State Machine & Approval Routing

Setiap request baru masuk ke `PendingManagerApproval`.

`CurrentApproverId` diisi dengan Manager dari Requester.

**Risk Formula:**

`IsHighRisk = (Environment == Production) OR (AccessLevel == Admin)`

**Approval Flow:**

- **Non-High-Risk:** Manager Approve → `Approved` (Terminal).
- **High-Risk:** Manager Approve → `PendingSystemOwnerApproval` → System Owner Approve → `Approved` (Terminal).
- **Rejection:** Manager atau System Owner Reject → `Rejected` (Terminal). Rejection wajib memiliki reason.

**CurrentApproverId:**

| Status | CurrentApproverId |
|---|---|
| PendingManagerApproval | Requester's Manager |
| PendingSystemOwnerApproval | Application's System Owner |
| Approved | NULL |
| Rejected | NULL |

Request dengan status `Approved` atau `Rejected` tidak dapat diproses kembali.

### 1.3 Core Invariants

1. **Anti Self-Approval**
   - Requester tidak boleh approve atau reject request miliknya sendiri.
   - `RequesterId == ActorId` → `403 Forbidden`.

2. **Server-Side Authorization**
   - Manager hanya boleh memproses request direct report.
   - System Owner hanya boleh memproses request aplikasi yang menjadi tanggung jawabnya.
   - Authorization memvalidasi status dan relasi master data di backend.
   - Erin hanya dapat melihat request dan audit, bukan melakukan approval.
   - ActorId diperoleh dari authenticated principal, bukan dari payload client.

3. **Idempotency**
   - Menggunakan `UNIQUE(RequesterId, ClientRequestId)`.
   - Request dengan key dan payload sama → kembalikan request lama (`200 OK`).
   - Request dengan key sama tetapi payload berbeda → `409 Conflict`.
   - Menggunakan payload hash untuk mendeteksi perbedaan payload.
   - Database unique constraint menjadi pengaman utama terhadap concurrent duplicate submission.

4. **Optimistic Concurrency**
   - Menggunakan integer `Version` yang bertambah setiap transisi.
   - Update dilakukan dengan conditional SQL berdasarkan Id, Status, dan ExpectedVersion.
   - Hanya satu transition untuk version yang sama boleh berhasil.
   - Stale atau concurrent action → `409 Conflict`.

5. **Atomic Audit Trail**
   - Setiap business-critical transition mencatat satu audit event.
   - Update status, increment version, dan insert audit harus berada dalam satu database transaction.
   - Jika audit gagal, perubahan status harus rollback.
   - Audit bersifat append-only pada application layer.

6. **Policy Snapshot**
   - Setiap request menyimpan `PolicyVersion` saat dibuat.
   - Phase 1 menggunakan policy tetap `v1`.
   - Perubahan policy di masa depan tidak mengubah interpretasi request lama.

7. **Terminal State**
   - `Approved` dan `Rejected` tidak memiliki outgoing transition.
   - Semua action setelah terminal state ditolak dengan `409 Conflict`.

---

## 2. Architecture & Project Structure

### 2.1 Tech Stack

- Framework: .NET 10 LTS.
- Frontend: Blazor Web App (Interactive Server).
- Backend: ASP.NET Core Minimal APIs.
- Database: MySQL.
- ORM: Entity Framework Core.
- MySQL Provider: Pomelo.EntityFrameworkCore.MySql (versi yang kompatibel dengan EF Core 10).
- Testing: xUnit, WebApplicationFactory, FluentAssertions, dan Testcontainers MySQL.
- Source Control: Git.

### 2.2 Project Structure

Satu project dengan struktur sederhana dan pragmatis.

```text
ARH/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   ├── NavMenu.razor
│   │   └── UserSwitcher.razor
│   │
│   ├── Pages/
│   │   ├── CreateRequest.razor
│   │   ├── MyRequests.razor
│   │   ├── ApprovalInbox.razor
│   │   └── RequestDetail.razor
│   │
│   └── Shared/
│       ├── AuditTimeline.razor
│       └── StatusBadge.razor
│
├── Domain/
│   ├── Entities/
│   │   ├── User.cs
│   │   ├── Application.cs
│   │   ├── AccessRequest.cs
│   │   └── AuditLog.cs
│   │
│   └── Enums/
│       ├── UserRole.cs
│       ├── RequestStatus.cs
│       ├── RiskLevel.cs
│       ├── EnvironmentType.cs
│       └── AccessLevel.cs
│
├── Data/
│   ├── Configurations/
│   │   ├── UserConfiguration.cs
│   │   ├── ApplicationConfiguration.cs
│   │   ├── AccessRequestConfiguration.cs
│   │   └── AuditLogConfiguration.cs
│   │
│   ├── Migrations/
│   ├── ArhDbContext.cs
│   └── ArhDbSeeder.cs
│
├── DTOs/
│   ├── CreateRequestDto.cs
│   ├── ApprovalActionDto.cs
│   ├── RequestResponseDto.cs
│   └── AuditLogDto.cs
│
├── Endpoints/
│   ├── RequestEndpoints.cs
│   ├── ApprovalEndpoints.cs
│   └── AuditEndpoints.cs
│
├── Services/
│   ├── AccessRequestService.cs
│   ├── WorkflowService.cs
│   ├── AuthorizationService.cs
│   └── AuditService.cs
│
├── Authentication/
│   └── DemoAuthenticationHandler.cs
│
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
│
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── ARH.csproj
├── README.md
├── PLAN.md
├── AI_USAGE.md
├── REVIEW.md
└── INTEGRITY.md
```

**Architecture Principles:**
- Single project, modular monolith.
- No Generic Repository Pattern.
- No MediatR or CQRS framework.
- No microservices.
- Business logic berada di Services.
- Database access menggunakan EF Core DbContext.
- Blazor UI tidak mengakses database secara langsung.
- Minimal APIs dan Blazor menggunakan authenticated user identity yang konsisten.
- User Switcher hanya tersedia di Development environment.

---

## 3. Data Model & Database Schema

### 3.1 Database Tables

| Table | Key Fields | Constraints |
|---|---|---|
| Users | Id, Email, FullName, Role, ManagerId | Email Unique, ManagerId FK |
| Applications | Id, Name, SystemOwnerId | Name Unique, SystemOwnerId FK |
| AccessRequests | Id, RequesterId, ClientRequestId, RequestPayloadHash, ApplicationId, Environment, AccessLevel, RiskLevel, Justification, Status, CurrentApproverId, RejectReason, PolicyVersion, Version, CreatedAtUtc, UpdatedAtUtc | RequesterId FK, ApplicationId FK, CurrentApproverId FK, Unique(RequesterId, ClientRequestId) |
| AuditLogs | Id, AccessRequestId, ActorId, ActorEmailSnapshot, Action, FromStatus, ToStatus, PreviousVersion, NewVersion, PolicyVersion, Reason, TimestampUtc | AccessRequestId FK, ActorId FK, Unique(AccessRequestId, NewVersion) |

### 3.2 Database Rules

- Primary key menggunakan GUID.
- `Version` menggunakan BIGINT dengan initial value `1`.
- `PolicyVersion` menyimpan versi policy ketika request dibuat.
- `CurrentApproverId` nullable untuk terminal states.
- `RejectReason` nullable, tetapi wajib diisi ketika status menjadi Rejected.
- `RequestPayloadHash` digunakan untuk validasi idempotency.
- AuditLogs menggunakan `ActorId` sebagai FK dan `ActorEmailSnapshot` untuk mempertahankan identitas historis.
- Semua timestamp menggunakan UTC dan MySQL `DATETIME(6)`.
- Foreign key, unique constraints, dan indexes dikonfigurasi melalui EF Core Fluent API.

### 3.3 Important Indexes

- `Users.Email` (Unique).
- `Applications.Name` (Unique).
- `AccessRequests(RequesterId, ClientRequestId)` (Unique).
- `AccessRequests(CurrentApproverId, Status, CreatedAtUtc)`.
- `AccessRequests(ApplicationId, Status, CreatedAtUtc)`.
- `AuditLogs(AccessRequestId, NewVersion)` (Unique).

---

## 4. Implementation Steps & Test Strategy

### 4.1 Implementation Order

1. **Project Setup**
   - Create .NET 10 Blazor Web App.
   - Configure Interactive Server.
   - Install EF Core dan Pomelo.
   - Setup MySQL connection.
   - Initialize Git repository.
   - Verify build dan run.

2. **Database Design**
   - Finalize four-table schema.
   - Define primary keys dan foreign keys.
   - Define unique constraints dan indexes.
   - Define relationships dan nullability.

3. **Domain**
   - Create User, Application, AccessRequest, AuditLog entities.
   - Create required enums.
   - Define initial status dan default Version.

4. **EF Core & MySQL**
   - Create ArhDbContext.
   - Configure entity relationships.
   - Configure indexes dan constraints.
   - Create initial migration.
   - Update MySQL database.
   - Seed 5 demo users dan 2 applications.

5. **Authentication & Authorization**
   - Implement development-only DemoAuthenticationHandler.
   - Implement current authenticated user identification.
   - Implement role validation.
   - Implement direct report validation.
   - Implement System Owner validation.
   - Implement anti-self-approval.
   - Protect API endpoints.

6. **Core Business Services**
   - Implement risk classification.
   - Implement Create Request.
   - Implement Manager Approval.
   - Implement System Owner Approval.
   - Implement Reject.
   - Validate status transitions.
   - Validate terminal states.

7. **Data Integrity**
   - Implement ClientRequestId idempotency.
   - Implement payload hash validation.
   - Implement unique constraint handling.
   - Implement Version-based conditional update.
   - Implement atomic status update dan audit insertion.
   - Implement transaction rollback handling.

8. **Minimal APIs**
   - Implement Create Request.
   - Implement My Requests.
   - Implement Approval Inbox.
   - Implement Request Detail.
   - Implement Approve dan Reject.
   - Implement Audit History.
   - Implement HTTP exception mapping.

9. **Automated Integration Tests**
   - Create xUnit test project.
   - Configure WebApplicationFactory.
   - Run tests against MySQL using Testcontainers.
   - Test business rules, authorization, idempotency, concurrency, dan audit integrity.

10. **Blazor UI**
    - Implement User Switcher.
    - Implement Create Request Form.
    - Implement My Requests.
    - Implement Approval Inbox.
    - Implement Request Detail.
    - Implement Audit Timeline.
    - Implement Approve/Reject dialog.
    - Implement conflict feedback.

11. **Final Verification & Documentation**
    - Run full integration test suite.
    - Verify security dan database integrity.
    - Verify migration from empty database.
    - Create README.md.
    - Create REVIEW.md.
    - Create INTEGRITY.md.
    - Create AI_USAGE.md.
    - Build Release.
    - Commit dan tag `phase-1-complete`.

### 4.2 API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/requests` | Create request |
| GET | `/api/requests/my` | My requests |
| GET | `/api/requests/inbox` | Approval inbox sesuai actor |
| GET | `/api/requests/{id}` | Request detail |
| POST | `/api/requests/{id}/approve` | Approve request |
| POST | `/api/requests/{id}/reject` | Reject request |
| GET | `/api/requests/{id}/history` | Request audit history |
| GET | `/api/audit-events` | Audit listing untuk Admin/Auditor |

**HTTP Response:**

- `200 OK`: Successful action atau idempotent replay.
- `201 Created`: New request created.
- `400 Bad Request`: Invalid input.
- `401 Unauthorized`: Not authenticated.
- `403 Forbidden`: Actor tidak memiliki authorization.
- `404 Not Found`: Resource tidak ditemukan.
- `409 Conflict`: Duplicate payload mismatch, stale version, atau invalid terminal transition.

ActorId dan ActorRole selalu diperoleh dari authenticated principal, bukan dari request body.

### 4.3 Integration Test Scenarios

| Scenario | Action / Condition | Expected Result |
|---|---|---|
| Standard Request | Alice meminta NonProduction + Read, Bob approve | Approved |
| Production Request | Alice meminta Production + Read, Bob approve, Carol approve | PendingSystemOwnerApproval → Approved |
| Admin Access | Alice meminta NonProduction + Admin, Bob approve, Dana approve | PendingSystemOwnerApproval → Approved |
| Self-Approval | Requester mencoba approve request sendiri | 403 Forbidden |
| Wrong Manager | User yang bukan direct manager mencoba approve | 403 Forbidden |
| Wrong Owner | Carol mencoba approve Finance Portal | 403 Forbidden |
| Auditor Approval | Erin mencoba approve atau reject | 403 Forbidden |
| Reject Without Reason | Approver reject tanpa reason | 400 Bad Request |
| Terminal Action | Approve/reject request yang sudah terminal | 409 Conflict |
| Idempotency | ClientRequestId dan payload sama dikirim ulang | Existing request dikembalikan, tanpa duplicate row |
| Payload Mismatch | ClientRequestId sama tetapi payload berbeda | 409 Conflict |
| Concurrent Create | Duplicate create dikirim secara bersamaan | Hanya satu business row |
| Concurrent Action | Approve dan reject menggunakan Version yang sama | Tepat satu berhasil, satu 409 Conflict |
| Audit Rollback | Audit insertion gagal | Seluruh transaksi rollback |
| Policy Snapshot | Policy berubah setelah request dibuat | Request lama tetap menyimpan PolicyVersion v1 |

Concurrent action test harus menggunakan actor yang authorized agar benar-benar menguji concurrency, bukan sekadar authorization failure.

---

## 5. Scope & Trade-offs

**Included in Phase 1:**
- Internal access request.
- Manager approval.
- System Owner approval.
- Risk-based routing.
- Demo authentication.
- Server-side authorization.
- Idempotency.
- Optimistic concurrency.
- Atomic audit logging.
- Basic admin/auditor read-only access.
- Blazor dashboard.

**Out of Scope:**
- External OAuth / SSO integration.
- Email notification service.
- Dynamic approval policy engine.
- Delegated approval.
- Multiple System Owners.
- Multi-level approval beyond Manager and System Owner.
- Microservices.
- Mobile application.
- Production identity provider integration.

**Technical Trade-offs:**

1. **Blazor Interactive Server:** Satu project memudahkan development dan deployment MVP.
2. **MySQL:** Database relasional dengan transaksi, foreign keys, dan unique constraints.
3. **Optimistic Concurrency:** Version token digunakan untuk mencegah stale action tanpa membutuhkan pessimistic locking pada setiap request.
4. **EF Core Migrations:** Database schema dikelola melalui C# entity configuration dan migrations.
5. **Simple Services:** Business logic ditempatkan di Services tanpa menambahkan abstraction yang tidak diperlukan.

---

## 6. Final Acceptance Criteria

Phase 1 dinyatakan selesai apabila:

1. Seluruh request mengikuti approval workflow yang ditentukan.
2. High-risk request wajib melalui System Owner.
3. Server-side authorization berjalan pada setiap business action.
4. Self-approval tidak dapat dilakukan.
5. Rejection wajib memiliki reason.
6. Approved dan Rejected bersifat terminal.
7. Duplicate request tidak menghasilkan duplicate business row.
8. Concurrent transition hanya menghasilkan satu pemenang.
9. Audit event selalu konsisten dengan perubahan status.
10. PolicyVersion tersimpan pada setiap request.
11. Seluruh integration tests berhasil menggunakan MySQL.
12. Blazor UI berjalan sesuai role dan permission.
13. Demo authentication tidak aktif di Production.
14. README dan dokumentasi integrity tersedia.
15. Project berhasil di-build dalam Release configuration.

**Final Deliverable:** Access Request Hub MVP (Phase 1), berjalan menggunakan .NET 10, Blazor Interactive Server, ASP.NET Core Minimal APIs, EF Core, dan MySQL.

**Development Principle:** Keep the implementation simple. Do not compromise business rules, security, idempotency, concurrency, and audit integrity.