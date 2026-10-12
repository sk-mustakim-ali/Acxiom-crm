# AcxiomCRM

AcxiomCRM is an ASP.NET Core 8 MVC customer relationship management application backed by SQLite and ASP.NET Core Identity.

## Requirements

- .NET 8 SDK
- PowerShell (for the commands below on Windows)

## Run locally

From this directory:

```powershell
dotnet restore
dotnet build
```

Set up the first administrator before starting the application. Use .NET user-secrets for local development:

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com"
dotnet user-secrets set "BootstrapAdmin:Password" "replace-with-a-unique-strong-password"
dotnet run
```

Alternatively, set the `BootstrapAdmin__Email` and `BootstrapAdmin__Password` environment variables. Configure both values or neither. Do not commit administrator credentials or put production secrets in `appsettings.json`.

On startup, EF Core applies pending SQLite migrations and the application ensures the `Admin`, `Manager`, and `SalesExecutive` roles exist. A configured bootstrap administrator is created if needed, activated, and assigned the Admin role. Without bootstrap settings, users can register as Sales Executives, but no administrator is created automatically.

The default database connection is `DataSource=app.db;Cache=Shared`. The local database is not part of the source repository; a new database is created and migrated at startup. Override `ConnectionStrings__DefaultConnection` to use another SQLite path.

## Roles and record access

- **Admin** can administer accounts and roles, view organization-wide CRM records, reports, and audit details.
- **Manager** can manage and view organization-wide CRM records and management reports, but cannot administer user accounts.
- **SalesExecutive** can access records assigned to them or linked to customers/leads they own. Dashboard and report data follow the same scope.

The application has no separate team entity, so Manager access currently means organization-wide business scope. Public registration always assigns the SalesExecutive role; a registrant cannot select a privileged role.

## Main modules

- Dashboard with date filtering, scoped KPIs, lead/opportunity charts, and pending follow-ups.
- Customer and lead management, including lead qualification and conversion.
- Opportunity pipeline and follow-up scheduling.
- Activity tracking.
- Administrator-only user creation, role management, activation, unlock, and password-reset-token generation.
- Scoped customer, lead, follow-up, opportunity, conversion, pipeline, user-activity, and audit reports.
- Audit history for authentication and CRM/admin changes.

## HTTP API

API routes use cookie authentication and return `401`/`403` rather than redirecting unauthenticated or unauthorized API requests.

- `GET /api/auth/csrf` obtains an antiforgery request token and sets its cookie.
- `POST /api/auth/login` and `POST /api/auth/logout` manage the Identity cookie session.
- Customers: `GET /api/customers`, `GET /api/customers/{id}`, `POST /api/customers`, `PUT /api/customers/{id}`, `DELETE /api/customers/{id}` (soft deactivation).
- Leads: `GET /api/leads`, `GET /api/leads/{id}`, `POST /api/leads`, `PUT /api/leads/{id}`, `DELETE /api/leads/{id}`.
- Opportunities: `GET /api/opportunities`, `GET /api/opportunities/{id}`, `POST /api/opportunities`, `PUT /api/opportunities/{id}`, `DELETE /api/opportunities/{id}`.
- Follow-ups: `GET /api/followups` and `POST /api/followups`.
- Pipeline: `GET /api/reports/pipeline`.

Send the antiforgery token in the `RequestVerificationToken` header for state-changing requests, along with the antiforgery cookie. Authentication API requests are rate-limited to five per minute per remote IP address.

## Security notes

- Use HTTPS and unique, strong credentials outside local development.
- Keep bootstrap secrets in a secret manager or environment variables and remove them when no longer needed.
- The initial GitHub history may contain old demo credentials from an earlier project state. Rotate any credentials that were ever used; removing them from the current source does not erase them from prior commits.
- Audit records can include operational details. Restrict access to administrators and protect database backups.
