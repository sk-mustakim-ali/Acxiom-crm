# AcxiomCRM

AcxiomCRM is an ASP.NET Core customer relationship management application for managing customer records, sales leads, opportunities, follow-ups, activities, users, and reports. It was developed against the functional and technical requirements in [`AcxiomCRM_Complete_Project_Assignment.pdf`](../AcxiomCRM_Complete_Project_Assignment.pdf).

The repository contains the application source, EF Core migrations, and an xUnit test project. It does not provide a default administrator account or password.

## Contents

- [Features](#features)
- [Technology and structure](#technology-and-structure)
- [Roles and access](#roles-and-access)
- [Requirements](#requirements)
- [Install, build, and run](#install-build-and-run)
- [Configuration](#configuration)
- [Database and migrations](#database-and-migrations)
- [HTTP API](#http-api)
- [Security and validation](#security-and-validation)
- [Tests](#tests)
- [Assignment coverage and limitations](#assignment-coverage-and-limitations)

## Features

| Area | Implemented behavior |
| --- | --- |
| Authentication | ASP.NET Core Identity registration, sign-in and sign-out; password policy and lockout; account activation. Public registration assigns the SalesExecutive role. |
| Dashboard | Role-scoped customer, lead, opportunity, and follow-up summaries; date periods; pipeline totals and charts. |
| Customers | Create, search, view, edit, and deactivate customer records; assign to an active Sales Executive when authorized. Deactivation changes status rather than deleting the customer. |
| Leads | Create, search, view, edit, and delete leads; capture source, priority, expected value, status, and assignment; convert a qualified lead to a customer. Conversion and its audit entries are saved transactionally. |
| Opportunities | Associate opportunities with a customer and optionally a lead; track source, amount, probability, stage, status, owner, and expected close date. Pipeline reporting includes open and weighted values, including owner groupings. |
| Follow-ups | Schedule and manage calls, meetings, emails, and tasks linked to a customer, lead, or opportunity; filter by due state and track planned, completed, missed, or cancelled status. |
| Activities | Create, search, edit, and delete call, meeting, email, or task records associated with a customer or lead. |
| Users and roles | Admin-only user creation, role assignment, account activation/deactivation, lockout status, and unlock actions. Role and activation changes invalidate existing sign-in cookies. |
| Reports and audit | Customer, lead, follow-up, opportunity, pipeline, conversion, user-activity, and audit views. Visibility and some filters depend on role. |
| REST API | Cookie-authenticated JSON endpoints for authentication, customers, leads, opportunities, follow-ups, and pipeline reporting. |

## Technology and structure

- **Runtime:** .NET 8 / ASP.NET Core MVC and Razor Pages
- **Authentication and roles:** ASP.NET Core Identity with cookie authentication
- **Data access:** Entity Framework Core 8 and SQLite
- **User interface:** Razor views, Bootstrap, and client-side validation scripts
- **Tests:** xUnit, ASP.NET Core `WebApplicationFactory`, and SQLite

The application uses MVC controllers and Razor views for interactive pages, Identity Razor Pages for account flows, and API controllers with request/response DTOs for JSON endpoints. Domain models and workflow rules hold data constraints and transition/calculation logic. Controllers use the EF Core `ApplicationDbContext` for persistence; services handle access scoping, contact normalization, sign-in auditing, and lead conversion. EF Core migrations define the SQLite schema. This is a compact layered MVC application; it does not implement a separate repository layer.

```text
AcxiomCRM/
├── Areas/Identity/   Identity account pages
├── Controllers/      MVC, reporting, audit, and API controllers
├── Data/             EF Core context, Identity user, and role/bootstrap setup
├── Migrations/       Versioned EF Core schema migrations
├── Models/           Entities, view models, and API DTOs
├── Services/         Access scope, workflow, normalization, and conversion rules
├── Views/            Razor views
└── wwwroot/          Static assets
AcxiomCRM.Tests/      xUnit workflow and API integration tests
```

## Roles and access

| Role | Access in this implementation |
| --- | --- |
| **Admin** | Organization-wide CRM data, reports, audit details, and user administration. |
| **Manager** | Organization-wide CRM data, assignments, and management reports. Managers cannot administer users; audit visibility is limited to their own business events, without sensitive values. There is no team hierarchy, so this role is not team-scoped. |
| **SalesExecutive** | Records assigned to the user or associated with customers/leads the user owns. Assignment controls and organization-wide views are restricted. |

All three roles can use the protected CRM API. Record queries still apply the role's access scope. Public registration always creates a SalesExecutive; no role is chosen by the registrant. An Admin can create users and change an account's role through User Management. No demo or default account is seeded. A bootstrap Admin is created only when both bootstrap settings are supplied.

## Requirements

- .NET 8 SDK
- PowerShell for the commands below (the app itself also runs on other supported .NET platforms)

The commands below are run from the repository root, the directory containing `AcxiomCRM/` and `AcxiomCRM.Tests/`.

## Install, build, and run

Restore and build the application:

```powershell
dotnet restore .\AcxiomCRM\AcxiomCRM.csproj
dotnet build .\AcxiomCRM\AcxiomCRM.csproj --no-restore
```

> **Database safety:** `Program.cs` calls `Database.MigrateAsync()` during application startup. The checked-in configuration points SQLite at `app.db`. Therefore, starting the app with its default connection string can apply pending migrations to the existing `AcxiomCRM/app.db`. Do not use the default connection for a database whose contents must be preserved until you have made and checked a separate backup and deliberately approved migration.

To run against a **new, separate local database** instead, set a different connection string before starting. This example puts the database under the system temporary directory and leaves `AcxiomCRM/app.db` out of the run:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Logging__EventLog__LogLevel__Default = "None"
$freshDb = Join-Path $env:TEMP ("AcxiomCRM-fresh-" + [guid]::NewGuid().ToString("N") + ".db")
$env:ConnectionStrings__DefaultConnection = "Data Source=$freshDb"
dotnet run --project .\AcxiomCRM\AcxiomCRM.csproj --no-build --no-launch-profile --urls http://localhost:5222
```

The `Logging__EventLog__LogLevel__Default` setting suppresses the Windows Event Log provider for this local console run; console logging remains available. The app creates the new database, applies migrations, and creates the built-in roles at startup. It does not create an administrator unless bootstrap credentials are configured. Stop the app with **Ctrl+C**. To clear the overrides from the current PowerShell session afterward:

```powershell
Remove-Item Env:ConnectionStrings__DefaultConnection
Remove-Item Env:ASPNETCORE_ENVIRONMENT
Remove-Item Env:Logging__EventLog__LogLevel__Default
```

The project launch profile `http` uses `http://localhost:5222`; the `https` profile uses HTTPS on port 7123 and HTTP on port 5222. The explicit command above avoids launch-profile behavior and uses HTTP for local development.

## Configuration

### Bootstrap administrator

There is no built-in username or password. On a fresh database, configure both settings before the first run if an administrator account is needed. .NET user-secrets are scoped to the application project:

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "<your-admin-email>" --project .\AcxiomCRM\AcxiomCRM.csproj
dotnet user-secrets set "BootstrapAdmin:Password" "<your-unique-strong-password>" --project .\AcxiomCRM\AcxiomCRM.csproj
```

These angle-bracketed values are placeholders: replace them locally and do not commit secrets. The configured password must satisfy the Identity policy: at least 8 characters and include an uppercase letter, lowercase letter, digit, and non-alphanumeric character. If both bootstrap settings are omitted, public registration can create a SalesExecutive account, but no Admin is created. Supplying only one bootstrap setting causes startup to fail.

### Connection string

`AcxiomCRM/appsettings.json` currently specifies `DataSource=app.db;Cache=Shared`. ASP.NET Core configuration can override it with `ConnectionStrings__DefaultConnection`, as shown in the isolated-database run command above. Keep real database paths and credentials out of source control.

## Database and migrations

The repository contains migrations for Identity and CRM entities, account activation, opportunity close dates and opportunity-linked follow-ups, customer modification dates, opportunity source, and normalized contact fields. The normalized contact migration backfills existing customer and lead email/phone values before creating unique indexes. It can fail if existing records collide after normalization; review and resolve such duplicates before applying it. As with any schema/data migration, make and verify a backup first.

### Fresh database

Use a new database path as shown in [Install, build, and run](#install-build-and-run). Startup applies the migrations to that new file. This is the recommended way to evaluate the app without changing an existing local database.

### Existing database, including `AcxiomCRM/app.db`

The app currently migrates its configured database automatically at startup; there is no README command or application switch that disables this behavior. To preserve an existing database:

1. Do not start the app against that database until you intend to apply pending migrations.
2. Stop any running app that uses it. Make a separate backup copy and confirm that the copy opens and contains the expected records. Do not use the original as a migration test.
3. Review the pending migration files, especially the normalized-contact backfill and unique indexes. Check the backup copy for email/phone duplicates after case and punctuation normalization.
4. Test startup migration using a separate copy of the backup and a connection string explicitly pointing to that copy. Confirm the app and data behave as expected.
5. Only after review and a recoverable backup, deliberately start the app against the intended database; startup will apply the pending migrations.

The instructions and verification in this README do not require migration or reset of `AcxiomCRM/app.db`. Automated tests use isolated temporary SQLite databases.

## HTTP API

API routes use ASP.NET Core Identity cookie authentication and accept/return JSON. Protected CRM endpoints require one of `Admin`, `Manager`, or `SalesExecutive`; record access is scoped as described above. Unauthenticated requests to protected CRM routes return **401** (`/api/auth/login` is anonymous). Authenticated callers outside the allowed role receive **403**. A record outside the caller's data scope is generally reported as **404**; an out-of-scope related record submitted in a create/update request is rejected with **400**. The global antiforgery filter also protects state-changing API requests.

### Authentication and antiforgery flow

First request a request-verification token and retain the cookies returned by the server:

```http
GET /api/auth/csrf
```

Response body:

```json
{ "token": "<request-token>" }
```

Send that token in `RequestVerificationToken` and keep the antiforgery cookie when posting credentials:

```http
POST /api/auth/login
Content-Type: application/json
RequestVerificationToken: <request-token>
Cookie: <antiforgery-cookie>

{
  "email": "<account-email>",
  "password": "<account-password>",
  "rememberMe": false
}
```

A successful login returns **200** with a session cookie and a body similar to:

```json
{ "message": "Signed in.", "userId": "<identity-id>", "roles": ["SalesExecutive"] }
```

Invalid credentials or an inactive account return **401**; a locked account returns **423**. The login endpoint is limited to five attempts per minute per remote IP. After login, request `/api/auth/csrf` again and use the current session cookie plus the returned antiforgery token for each write request. `POST /api/auth/logout` requires an authenticated session and antiforgery token; success returns **204**.

### CRM and reporting endpoints

| Method | Route | Request / response |
| --- | --- | --- |
| `GET` | `/api/customers` | Returns the caller's customer DTOs (**200**). |
| `GET` | `/api/customers/{id}` | Returns one in-scope customer (**200**) or **404**. |
| `POST` | `/api/customers` | Accepts `CustomerCreateRequest`; creates and audits a customer (**201**). |
| `PUT` | `/api/customers/{id}` | Accepts `CustomerCreateRequest`; updates an in-scope customer (**200**) or **404**. |
| `DELETE` | `/api/customers/{id}` | Sets the customer status to Inactive and audits the change (**204**) or **404**. |
| `GET` | `/api/leads` | Returns the caller's lead DTOs (**200**). |
| `GET` | `/api/leads/{id}` | Returns one in-scope lead (**200**) or **404**. |
| `POST` | `/api/leads` | Accepts `LeadCreateRequest`; creates and audits a lead (**201**). |
| `PUT` | `/api/leads/{id}` | Accepts `LeadCreateRequest`; updates an in-scope lead (**200**) or **404**. |
| `DELETE` | `/api/leads/{id}` | Deletes an in-scope lead (**204**) or **404**. |
| `GET` | `/api/opportunities` | Returns the caller's opportunity DTOs (**200**). |
| `GET` | `/api/opportunities/{id}` | Returns one in-scope opportunity (**200**) or **404**. |
| `POST` | `/api/opportunities` | Accepts `OpportunityCreateRequest`; creates and audits an opportunity (**201**). |
| `PUT` | `/api/opportunities/{id}` | Accepts `OpportunityCreateRequest`; updates an in-scope opportunity (**200**) or **404**. |
| `DELETE` | `/api/opportunities/{id}` | Deletes an in-scope opportunity (**204**) or **404**. |
| `GET` | `/api/followups` | Returns the caller's follow-up DTOs (**200**). |
| `POST` | `/api/followups` | Accepts `FollowUpCreateRequest`; creates a follow-up (**201**). Exactly one in-scope related record is required. |
| `GET` | `/api/reports/pipeline` | Returns pipeline totals grouped by stage (**200**), scoped to the caller. |

Customer create/update request example (send the authenticated session cookie and a fresh antiforgery token):

```http
POST /api/customers
Content-Type: application/json
RequestVerificationToken: <request-token>
Cookie: <session-and-antiforgery-cookies>

{
  "customerName": "Example Customer",
  "email": "contact@example.test",
  "phone": "+1 (555) 123-4567",
  "companyName": "Example Company",
  "status": "Active",
  "address": "100 Example Street"
}
```

Customer requests require name, valid email, phone, company name, and status; `address` and `ownerId` are optional. A SalesExecutive is assigned as owner by default. Admins and Managers may assign an active SalesExecutive. Lead requests use `leadName`, `companyName`, `source`, and `priority`, with optional `email`, `phone`, `expectedValue`, `notes`, and `assignedToId`; a new lead starts in `New`. Opportunity requests require `opportunityName`, `customerId`, `amount`, `stage`, `probability`, `expectedCloseDate`, `status`, and `source`; `leadId`, `notes`, and `ownerId` are optional. Follow-up requests require `followUpDate`, `subject`, and `followUpType`, and exactly one of `customerId`, `leadId`, or `opportunityId`.

DTOs omit Identity password/security fields and internal normalized-contact values. Invalid model data, workflow values, references, or assignments return **400** (validation responses use Problem Details where generated by the framework); duplicate normalized customer/lead contacts return **409**. This API does not expose activity CRUD or lead conversion endpoints. The pipeline API is stage-grouped; owner grouping is available in the MVC report.

## Security and validation

- ASP.NET Core Identity manages password hashes, password verification, role membership, and sign-in cookies; plaintext passwords are not stored by the application.
- Passwords must be at least 8 characters and include uppercase, lowercase, numeric, and non-alphanumeric characters. Identity lockout is configured for five failed attempts with a five-minute lockout.
- Inactive users cannot sign in. Role or activation changes rotate the security stamp so an existing cookie is rejected on its next validation.
- MVC forms and API writes use antiforgery validation. API requests use cookies, not bearer tokens.
- API login has a fixed-window rate limit of five attempts per minute per remote IP. Identity lockout also applies to password sign-in.
- Server-side checks validate model fields, contact uniqueness, workflow transitions, active assignees, linked-record access, opportunity amount/probability/date rules, and follow-up dates.
- Access is scoped in database queries for MVC and API record reads and writes. Admin and Manager have organization-wide CRM access; SalesExecutive records are owner/assignment scoped.
- Audit records are created for authentication and many CRM and user-management operations. Sensitive audit values and IP addresses are shown only to Admins. Audit-log storage is not protected by an immutable/append-only database mechanism.
- Production deployment should use HTTPS and protected configuration/secrets. The local HTTP launch profile is for development only.

## Tests

Restore, build, and run the automated suite from the repository root:

```powershell
dotnet restore .\AcxiomCRM.Tests\AcxiomCRM.Tests.csproj
dotnet build .\AcxiomCRM\AcxiomCRM.csproj --no-restore
dotnet test .\AcxiomCRM.Tests\AcxiomCRM.Tests.csproj --no-restore
```

The tests cover customer/lead validation, lead and follow-up transitions, overdue-follow-up rules, pipeline calculations, opportunity validation, SalesExecutive record scope, duplicate customer email constraints, lead conversion and audit behavior, unauthenticated API rejection, antiforgery and invalid API payloads, duplicate API customer handling, data scoping, and cookie invalidation after role/account changes. API integration tests use a per-run temporary SQLite database; workflow tests use isolated SQLite databases. They do not target the configured development database.

## Assignment coverage and limitations

Implemented portions include Identity authentication and roles, scoped CRM CRUD, dashboard and reports, validation and business transitions, normalized duplicate contact checks, lead-to-customer conversion, role-aware APIs, audit events, and automated tests. This README documents the implementation, not a claim that every assignment item is complete.

The following assignment requirements are incomplete or only partly implemented:

- Managers see organization-wide CRM data because the data model has no manager/team hierarchy; team-specific scope is not implemented.
- User administration supports create, role assignment, activation/deactivation, and unlock, but does not edit profile fields or provide password reset delivery.
- Follow-up due-state display and filters exist, but no reminder/notification delivery is configured. Follow-up rescheduling/completion is not represented as a complete history stream.
- Some lists have search/filter controls, but consistent sorting and pagination are not implemented across list pages.
- Reports do not consistently provide the full set of filters, sorting, pagination, or export. Audit supports date/entity/action filters and Admin actor filtering, with a maximum of 500 displayed rows; Managers see only their own business audit events. The User Activity report supports date and assignee filters.
- Activity has MVC screens but no REST API. The API also has no lead-conversion route and no follow-up update/delete routes.
- No dedicated API schema/OpenAPI document is configured. The README provides endpoint and request guidance, but not an exhaustive schema catalog for every DTO or every error body.
- Audit logging covers important events, but not every possible status/security change, and the database does not enforce audit immutability.
- A qualified lead converts to a customer; conversion does not also create an opportunity.
- The automated suite provides focused workflow and API tests; it does not cover every role, controller, or assignment acceptance criterion.

Screenshots in [`docs/screenshots/`](docs/screenshots/) are illustrative captures of the dashboard, customer, lead, and opportunity pages. They are not seeded records or evidence that a fresh database contains sample CRM data.
