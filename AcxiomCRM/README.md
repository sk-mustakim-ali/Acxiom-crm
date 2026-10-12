# AcxiomCRM

AcxiomCRM is a role-aware customer relationship management application for organizing customer accounts, tracking prospective business, managing sales opportunities and follow-ups, and reviewing team performance. It is built with ASP.NET Core 8 MVC, ASP.NET Core Identity, Entity Framework Core, and SQLite.

> **Local demo is running:** [http://localhost:5088](http://localhost:5088). Sign in on the running development instance with the administrator account supplied to you. Do not assume this local account or its database exists in a fresh clone.

## Table of contents

- [What the application does](#what-the-application-does)
- [Screenshots](#screenshots)
- [Roles and access](#roles-and-access)
- [Typical CRM workflow](#typical-crm-workflow)
- [Run locally](#run-locally)
- [First sign-in and user setup](#first-sign-in-and-user-setup)
- [HTTP API](#http-api)
- [Validation and security](#validation-and-security)
- [Project structure](#project-structure)
- [Assignment feature checklist](#assignment-feature-checklist)
- [Limitations and implementation notes](#limitations-and-implementation-notes)

## What the application does

| Area | Purpose |
|---|---|
| Dashboard | Summarizes customers, leads, opportunities, pipeline value, and pending follow-ups. Choose a reporting period or a custom date range. Charts show lead status, opportunity stages, and closed-won sales by month. |
| Customers | Create, search, filter, inspect, and update customer accounts, including contact/company details, status, and owner. Deactivation retains the record as inactive rather than deleting it. |
| Leads | Track prospects, source, priority, expected value, status, notes, and assigned Sales Executive. Search/filter the list, inspect lead details, and convert a qualified lead into a customer. |
| Opportunities | Link a potential sale to a customer (and optionally a lead), manage its amount, probability, stage, owner, and expected close date, and record Won or Lost outcomes. |
| Follow-ups | Schedule calls, meetings, emails, and tasks, link them to a customer, lead, or opportunity, assign them, and track planned/completed/missed/cancelled status. |
| Activities | Record calls, meetings, emails, or tasks against a customer or lead. Filter by date, status, type, and assignee; update or remove activities that you can access. |
| User management | Admins can create users, assign one of the application roles, activate/deactivate accounts, unlock accounts, and generate password-reset tokens. |
| Reports | View scoped customer, lead, follow-up, opportunity, pipeline, conversion, user-activity, and audit reports. Management reports support date or assignee filters where relevant. |
| Audit log | Review recorded authentication events and changes to CRM records and user accounts. Access to sensitive values and network information is restricted to Admins. |
| HTTP API | Use authenticated JSON endpoints for login/logout, customers, leads, opportunities, follow-ups, and pipeline data. |

All of these screens are part of the same application. The main navigation groups record-management links under **CRM**, report links under **Insights**, and provides a visible **Logout** button alongside the signed-in account.

On narrow screens, tap the menu button beside the AcxiomCRM brand to expand the navigation. The signed-in account and high-contrast **Logout** button are at the bottom of the expanded menu.

![Expanded application navigation showing the Logout button](docs/screenshots/navigation-logout.png)

## Screenshots

These full-page captures show the running application. The local database used for the captures is not distributed with the repository; CRM dashboard figures are zero where that database has no matching CRM records.

### Dashboard

KPIs and charts respond to the selected reporting period and the signed-in user's data scope.

![AcxiomCRM dashboard](docs/screenshots/dashboard.png)

### Customer and sales workflow

The customer and lead lists provide the entry points to create and manage records. Leads can be progressed through qualification and converted when eligible; opportunities represent the sales pipeline.

| Customers | Leads |
|---|---|
| ![Customer list](docs/screenshots/customers.png) | ![Lead list](docs/screenshots/leads.png) |

| Opportunities | Follow-ups |
|---|---|
| ![Opportunity list](docs/screenshots/opportunities.png) | ![Follow-up list](docs/screenshots/follow-ups.png) |

### Activities and reports

| Activities | Report catalogue |
|---|---|
| ![Activity list](docs/screenshots/activities.png) | ![Reports](docs/screenshots/reports.png) |

The pipeline report groups open amount and weighted amount by sales stage.

![Opportunity pipeline report](docs/screenshots/pipeline-report.png)

### Administration and audit

The user-management page is restricted to Admins. Audit entries include sign-in/sign-out events and tracked application changes.

| User management | Audit log |
|---|---|
| ![User management](docs/screenshots/user-management.png) | ![Audit log](docs/screenshots/audit-log.png) |

## Roles and access

| Role | Access |
|---|---|
| **Admin** | Organization-wide CRM records and reports; user creation and administration; full audit-log details. |
| **Manager** | Organization-wide CRM records, assignment controls, management reports, and audit-log access. Cannot administer accounts. |
| **SalesExecutive** | Records assigned to them or associated with customers/leads they own. Dashboard, API results, activities, follow-ups, and reports are scoped accordingly. |

Public self-registration always creates a **SalesExecutive** account. A registrant cannot select a privileged role. Admins can create or change user roles using the User Management page. In this implementation there is no separate team entity, so Manager access is organization-wide.

## Typical CRM workflow

1. **Add a customer or lead.** Admins and Managers can assign records to an active Sales Executive. Sales Executives create records in their own scope.
2. **Qualify a lead.** Track the status, priority, notes, and expected value. A qualified lead can be converted into a customer; the conversion is recorded in the audit log.
3. **Create an opportunity.** Associate it with a customer and optionally a lead. Move it through Qualification, Proposal, and Negotiation, then close it as Won or Lost. Closed opportunities record a close date.
4. **Plan follow-ups and activities.** Link follow-ups to a customer, lead, or opportunity; record activities against a customer or lead. The application checks that linked records are in the current user's permitted scope.
5. **Review performance.** Filter the dashboard by time period and use Reports for pipeline, conversion, and activity summaries.

Workflow transitions and status/stage combinations are validated on the server; the application does not rely only on browser-side validation.

## Run locally

### Prerequisites

- Windows, macOS, or Linux
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PowerShell for the commands shown here (or use equivalent environment-variable commands in another shell)

### Start the application

Open PowerShell in the `AcxiomCRM` project directory:

```powershell
cd "C:\Users\ASUS\Desktop\AcxiomCRM Project\AcxiomCRM"
dotnet restore
dotnet build
```

Configure the first Admin credentials using .NET user-secrets. Replace the example values with a unique email and a strong password:

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "you@example.com"
dotnet user-secrets set "BootstrapAdmin:Password" "Use-A-Unique-Strong-Password!"
```

Start the web application:

```powershell
dotnet run
```

With the included `http` launch profile, browse to **http://localhost:5222**. To explicitly run on the port used for the currently running local demo, use:

```powershell
dotnet run --no-launch-profile --urls http://localhost:5088
```

The first startup applies pending Entity Framework Core migrations and creates the built-in roles. The configured bootstrap Admin is created if it does not yet exist. Keep the terminal running while using the site; stop the application with **Ctrl+C**.

### Database

By default, SQLite uses `DataSource=app.db;Cache=Shared` in the project directory. The database is created and migrated automatically. It is local application data and is intentionally not committed to Git. A fresh checkout does not include the local development database or its users.

To use a different SQLite file, set the connection string before starting:

```powershell
$env:ConnectionStrings__DefaultConnection = "Data Source=C:\data\acxiomcrm.db"
dotnet run
```

### First sign-in and user setup

- On a **fresh checkout**, configure both `BootstrapAdmin:Email` and `BootstrapAdmin:Password` before the first run. There is deliberately no universal default administrator password in source control.
- If both bootstrap settings are omitted, public registration can create a Sales Executive account, but registration does not create an Admin.
- The already-running local demonstration at `http://localhost:5088` uses a pre-existing local development account. Use the sign-in details provided with that running instance; they are not credentials for a clean checkout.
- Use **User Management** to create Manager/Sales Executive accounts, change roles, activate/deactivate users, unlock accounts, or generate password-reset tokens.

For production, supply secrets from an appropriate secret manager or protected environment variables. Do not commit credentials, share development passwords, or reuse a demo password for real data.

## HTTP API

API routes use the same Identity cookie session and role-based record scoping as the MVC application. An unauthenticated API request returns `401`; an authenticated but unauthorized request returns `403`, rather than an MVC login-page redirect.

### Authentication and antiforgery

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/auth/csrf` | Issues an antiforgery request token and cookie. |
| `POST` | `/api/auth/login` | Signs in with email and password; returns the user ID and roles. |
| `POST` | `/api/auth/logout` | Signs out the current session. |

For state-changing requests, send the antiforgery cookie and include its request token in the `RequestVerificationToken` header. Authentication API requests are limited to five per minute per remote IP address.

### CRM and reporting routes

| Resource | Methods and routes |
|---|---|
| Customers | `GET /api/customers`, `GET /api/customers/{id}`, `POST /api/customers`, `PUT /api/customers/{id}`, `DELETE /api/customers/{id}` (soft-deactivates). |
| Leads | `GET /api/leads`, `GET /api/leads/{id}`, `POST /api/leads`, `PUT /api/leads/{id}`, `DELETE /api/leads/{id}`. |
| Opportunities | `GET /api/opportunities`, `GET /api/opportunities/{id}`, `POST /api/opportunities`, `PUT /api/opportunities/{id}`, `DELETE /api/opportunities/{id}`. |
| Follow-ups | `GET /api/followups`, `POST /api/followups`. |
| Pipeline | `GET /api/reports/pipeline`. |

The API validates request DTOs, workflow states, assignments, and references to related records. Activity CRUD is currently available through the MVC interface, not a dedicated activity API endpoint.

## Validation and security

- ASP.NET Core Identity enforces a password policy (minimum length 8; uppercase, lowercase, digit, and non-alphanumeric character required) and locks accounts after five failed sign-in attempts for five minutes.
- Inactive accounts cannot sign in. API sign-in attempts are rate limited.
- Forms use antiforgery validation. API write operations also require antiforgery tokens because authentication uses cookies.
- User/role administration and audit screens use role authorization. Business record queries and related-record selectors apply server-side ownership/access scoping.
- Controllers validate record ownership, active assignees, required related records, allowed statuses, and stage transitions before saving.
- Audit events are written for authentication and tracked CRM/user-management changes.
- Set up a unique bootstrap Admin secret per environment; use HTTPS and protect production database files and backups.

## Project structure

```text
AcxiomCRM/
|-- Areas/Identity/Pages/Account/   Identity registration and account pages
|-- Controllers/                   MVC modules, reports, audit, and API
|-- Data/                          EF Core context, user model, and role/bootstrap setup
|-- Migrations/                    Versioned SQLite schema migrations
|-- Models/                        Domain entities, form models, and API DTOs
|-- Services/                      Access scoping, workflow rules, and sign-in auditing
|-- Views/                         Razor pages for CRM, admin, reports, and shared UI
|-- wwwroot/                       CSS, JavaScript, and static assets
`-- docs/screenshots/              README screenshots of the running UI
```

## Assignment feature checklist

- [x] Identity sign-in, sign-out, public registration, and the Admin/Manager/SalesExecutive role model.
- [x] Role-aware customer, lead, opportunity, and follow-up management.
- [x] Server-side record scoping, relationship checks, form validation, and workflow transition rules.
- [x] Lead qualification and conversion into a customer.
- [x] Dashboard KPIs, reporting-period filtering, and lead/opportunity/sales charts.
- [x] Follow-up scheduling and activity management.
- [x] Administrator user/role management and account activation/lockout operations.
- [x] Audit history and business/pipeline/conversion/activity reports.
- [x] Authenticated, DTO-based API routes with antiforgery and authentication rate limiting.
- [x] SQLite migrations applied automatically at application startup.
- [x] Setup, role, API, and security guidance documented here.

## Limitations and implementation notes

- A distinct team hierarchy is not represented in the current data model. Managers therefore have organization-wide business-record visibility.
- Activity management is provided by MVC screens; it does not currently have separate REST API routes.
- Password-reset token generation is an administrative operation. Delivering reset links to end users requires an email provider, which is not configured in this project.
- Charts and reports reflect the records in the current database and the signed-in user's authorized scope. New databases are empty apart from Identity roles and configured users.
- Screenshots document visible screens; they are not seeded sample data and do not imply a populated database on a fresh install.
