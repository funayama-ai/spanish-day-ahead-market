# Spanish Day-Ahead Market Application

A .NET 10 application for downloading, parsing, storing, serving, and visualizing official Spanish Day-Ahead electricity-market prices published by OMIE.

The application retrieves OMIE `MARGINALPDBC` files, converts the market data into structured Day-Ahead price records, stores the records in SQLite, exposes them through a REST API, and displays them in a Blazor Server dashboard.

---

## Repository

Public GitHub repository:

```text
https://github.com/funayama-ai/spanish-day-ahead-market
```

---

## 1. Project Objective

The objective of this project is to build a reproducible end-to-end data pipeline for Spanish Day-Ahead electricity-auction prices.

The complete workflow is:

```text
OMIE publication
    ↓
HTTP file download
    ↓
MARGINALPDBC validation and parsing
    ↓
Day-Ahead import and revision handling
    ↓
SQLite persistence
    ↓
REST API
    ↓
Blazor Server dashboard
```

The project focuses on:

- reliable market-data ingestion;
- correct 15-minute interval handling;
- CET/CEST and UTC timestamp conversion;
- duplicate prevention;
- OMIE file-revision handling;
- scheduled automatic imports;
- API-based data access;
- clear visualization for market analysis;
- automated unit and API integration testing.

---

## 2. Research Context

OMIE publishes Day-Ahead auction results for the Iberian electricity market.

This application uses the OMIE `MARGINALPDBC` file format. Each market-data record contains information such as:

- delivery year;
- delivery month;
- delivery day;
- delivery period;
- Portuguese market price;
- Spanish market price.

The application imports the Spanish bidding-zone price using:

```text
ES
```

The implemented system supports 15-minute Market Time Units.

Depending on daylight-saving-time transitions, a delivery day can contain:

| Delivery-day type | Expected periods |
|---|---:|
| Normal 24-hour day | 96 |
| Spring DST transition, 23 hours | 92 |
| Autumn DST transition, 25 hours | 100 |

The application stores both:

- Spanish local delivery time using CET or CEST;
- the corresponding UTC delivery time.

This allows the stored data to be compared consistently with other European electricity-market datasets.

---

## 3. Main Features

- .NET 10 solution
- Clean layered project structure
- Official OMIE Day-Ahead file downloader
- OMIE `MARGINALPDBC` parser
- 15-minute Day-Ahead price support
- 92, 96, and 100-period delivery-day support
- Spanish CET/CEST timestamp handling
- UTC timestamp conversion
- SQLite persistence
- Entity Framework Core
- Database migrations
- Automatic EF Core migration at API startup
- Duplicate prevention
- OMIE source-file revision handling
- Quartz.NET scheduled imports
- JSON REST API response
- Semicolon-separated `text/plain` response
- Blazor Server dashboard
- Delivery-date selector
- Record-count KPI
- Average-price KPI
- Minimum-price KPI
- Maximum-price KPI
- Day-Ahead price-profile chart
- Complete 15-minute price table
- Unit tests
- REST API integration tests
- Public GitHub repository

---

## 4. Solution Structure

```text
spanish-day-ahead-market/
├── src/
│   ├── SpanishDayAhead.Api/
│   ├── SpanishDayAhead.Application/
│   ├── SpanishDayAhead.Domain/
│   ├── SpanishDayAhead.Infrastructure/
│   └── SpanishDayAhead.Web/
├── tests/
│   └── SpanishDayAhead.Tests/
├── .gitignore
├── dotnet-tools.json
├── README.md
└── SpanishDayAhead.slnx
```

### 4.1 SpanishDayAhead.Domain

Contains the central Day-Ahead price domain model.

The domain model represents:

- unique record ID;
- bidding zone;
- delivery date;
- delivery period;
- resolution;
- UTC delivery start;
- price in EUR/MWh;
- source filename;
- source version;
- UTC import timestamp.

### 4.2 SpanishDayAhead.Application

Contains:

- application abstractions;
- downloader interface;
- parser interface;
- repository interface;
- import-service interface;
- downloaded-file model;
- import-result model;
- complete Day-Ahead import workflow.

The Application project is independent of the HTTP, database, and user-interface implementations.

### 4.3 SpanishDayAhead.Infrastructure

Contains:

- OMIE HTTP downloader;
- OMIE file parser;
- SQLite DbContext;
- Entity Framework Core entity configuration;
- database migrations;
- repository implementation.

### 4.4 SpanishDayAhead.Api

Contains:

- Day-Ahead REST API controller;
- response contracts;
- Quartz.NET import job;
- Quartz schedule configuration;
- dependency-injection registration;
- SQLite configuration;
- automatic EF Core migration execution before Quartz.NET starts;
- OMIE downloader configuration;
- application logging;
- OpenAPI support.

### 4.5 SpanishDayAhead.Web

Contains:

- Blazor Server dashboard;
- REST API client;
- Day-Ahead data-transfer model;
- KPI calculations;
- price-profile chart;
- price table;
- project-specific navigation;
- application branding.

### 4.6 SpanishDayAhead.Tests

Contains tests for:

- OMIE file parsing;
- DST period counts;
- negative prices;
- invalid headers;
- missing final marker;
- filename and delivery-date validation;
- missing or duplicated periods;
- OMIE file downloading;
- Day-Ahead import workflow;
- revision handling;
- duplicate handling;
- JSON REST API response;
- semicolon-separated response;
- missing-date `404` response.

---

## 5. Application Architecture

The project follows a layered design:

```text
SpanishDayAhead.Web
        ↓ HTTP
SpanishDayAhead.Api
        ↓
SpanishDayAhead.Application
        ↓
SpanishDayAhead.Domain
        ↑
SpanishDayAhead.Infrastructure
```

### Domain layer

Defines the core market-data model.

### Application layer

Defines the use cases, workflow, and interfaces.

### Infrastructure layer

Implements OMIE communication, parsing, SQLite persistence, and repositories.

### API layer

Exposes stored data through HTTP and operates the scheduled import workflow.

### Web layer

Consumes the REST API and presents the market data to the user.

---

## 6. OMIE File Download

The application downloads files with names such as:

```text
marginalpdbc_20260711.1
```

The filename structure is:

```text
marginalpdbc_YYYYMMDD.VERSION
```

For example:

```text
marginalpdbc_20260711.1
```

represents:

- delivery date: `2026-07-11`;
- source version: `1`.

The downloader:

- creates the OMIE request;
- sends the HTTP request;
- handles successful responses;
- handles `404 Not Found`;
- records the source filename;
- returns the downloaded content as a stream;
- logs the download result.

An OMIE `404` response is treated as a normal “file not yet published” result rather than as an unexpected application failure.

---

## 7. OMIE File Parser

The parser validates the complete file before accepting market records.

Validation includes:

- expected `MARGINALPDBC` header;
- valid year, month, and day;
- valid delivery-period number;
- valid decimal prices;
- required final marker;
- delivery date matching the filename;
- continuous period sequence;
- correct number of periods for the delivery date;
- supported source-version format.

Supported period counts are:

```text
92
96
100
```

Supported prices include:

- positive prices;
- zero prices;
- negative prices.

Every accepted Spanish price is converted into a structured Day-Ahead domain record.

---

## 8. Time-Zone Handling

OMIE delivery periods use Spanish local market time.

The application converts the local delivery periods into UTC using the Spanish market time zone.

On Windows, the application uses:

```text
Romance Standard Time
```

On systems using IANA time-zone identifiers, the corresponding identifier is:

```text
Europe/Madrid
```

Example for a summer delivery day:

```text
Spanish local start: 2026-07-11 00:00 +02:00
UTC start:           2026-07-10 22:00 UTC
```

The implementation handles:

- Central European Time;
- Central European Summer Time;
- spring daylight-saving transition;
- autumn daylight-saving transition.

---

## 9. Database Persistence

The application uses SQLite through Entity Framework Core.

The development database is located at:

```text
src/SpanishDayAhead.Api/Data/spanish-day-ahead.db
```

The `DayAheadPrices` table stores:

- `Id`
- `BiddingZone`
- `DeliveryDate`
- `DeliveryStartUtc`
- `ImportedAtUtc`
- `Period`
- `PriceEurPerMWh`
- `ResolutionMinutes`
- `SourceFileName`
- `SourceVersion`

The database schema is managed with Entity Framework Core migrations.

### 9.1 Automatic database migration at API startup

The API applies all pending Entity Framework Core migrations before Quartz.NET and the HTTP application begin using the database.

On a clean clone or after the local SQLite database has been deleted, the first API startup automatically:

1. creates the SQLite database file;
2. creates the Entity Framework Core migration-history table;
3. applies the `20260710165436_InitialCreate` migration;
4. creates the `DayAheadPrices` table and indexes; and
5. starts the scheduled OMIE import only after migration completion.

Expected startup log:

```text
Applying pending database migrations.
Applying migration '20260710165436_InitialCreate'.
Database migrations applied successfully.
```

A manual database update is not required for normal setup:

```cmd
dotnet ef database update
```

The command above remains useful for diagnostics, but another user should be able to clone the repository and create the database simply by starting the API.

The database remains available after the API process is stopped and restarted.

Generated SQLite files are excluded from the public Git repository through `.gitignore`:

```text
*.db
*.db-shm
*.db-wal
```

The migration source files remain in Git so the schema can be reproduced on another computer.

---

## 10. Duplicate Prevention

Repeated imports must not create duplicate price records.

The import workflow checks incoming data against existing database records.

When the same source file is imported again, unchanged records are ignored.

A verified repeated import produced:

```text
Parsed:   96
Inserted: 0
Updated:  0
Ignored:  96
```

This confirms that all 96 existing records were recognized and were not inserted again.

---

## 11. OMIE Revision Handling

OMIE may publish later versions of the same delivery-day file.

Examples:

```text
marginalpdbc_20260711.1
marginalpdbc_20260711.2
```

The application stores the OMIE source version and applies revision logic.

The workflow distinguishes between:

- inserted records;
- updated records;
- unchanged records;
- ignored duplicate records.

A later OMIE source version can update the existing economic result without duplicating the same delivery intervals.

---

## 12. Quartz.NET Scheduling

Quartz.NET runs inside the API application.

The scheduled workflow:

1. determines the target delivery date;
2. starts with the configured source version;
3. requests the OMIE file;
4. parses the file if available;
5. imports the records into SQLite;
6. checks the next source version;
7. stops revision checking when no later version is available.

The configured schedule checks OMIE every:

```text
15 minutes
```

Quartz starts automatically when the API starts.

Quartz also stops gracefully when the API application shuts down.

An active import job is allowed to complete during graceful shutdown.

---

## 13. REST API

The main endpoint is:

```text
GET /api/day-ahead-prices
```

Required query parameter:

```text
deliveryDate=YYYY-MM-DD
```

### 13.1 JSON response

Request:

```http
GET https://localhost:7086/api/day-ahead-prices?deliveryDate=2026-07-11
Accept: application/json
```

Successful result:

```text
HTTP/1.1 200 OK
Content-Type: application/json
```

Example response record:

```json
{
  "biddingZone": "ES",
  "deliveryDate": "2026-07-11",
  "period": 1,
  "resolutionMinutes": 15,
  "deliveryStartLocal": "2026-07-11T00:00:00+02:00",
  "deliveryStartUtc": "2026-07-10T22:00:00+00:00",
  "priceEurPerMWh": 160.31,
  "sourceFileName": "marginalpdbc_20260711.1",
  "sourceVersion": 1,
  "importedAtUtc": "2026-07-10T18:31:18.8906775+00:00"
}
```

### 13.2 Semicolon-separated response

Request:

```http
GET https://localhost:7086/api/day-ahead-prices?deliveryDate=2026-07-11
Accept: text/plain
```

Successful result:

```text
HTTP/1.1 200 OK
Content-Type: text/plain
```

Header:

```text
bidding_zone;delivery_date;period;resolution_minutes;delivery_start_local;delivery_start_utc;price_eur_per_mwh;source_file_name;source_version;imported_at_utc
```

Example data record:

```text
ES;2026-07-11;1;15;2026-07-11T00:00:00+02:00;2026-07-10T22:00:00Z;160.31;marginalpdbc_20260711.1;1;2026-07-10T18:31:18Z
```

### 13.3 Missing delivery date

Request:

```http
GET https://localhost:7086/api/day-ahead-prices?deliveryDate=2026-07-12
Accept: application/json
```

Result:

```text
HTTP/1.1 404 Not Found
```

Example response:

```json
{
  "title": "Day-Ahead prices were not found.",
  "status": 404,
  "detail": "No stored prices exist for delivery date 2026-07-12."
}
```

---

## 14. Blazor Dashboard

The Blazor dashboard is available at:

```text
https://localhost:7289
```

The named route is:

```text
https://localhost:7289/day-ahead-prices
```

The dashboard contains:

- delivery-date selector;
- Load Data button;
- number-of-records KPI;
- average-price KPI;
- minimum-price KPI;
- maximum-price KPI;
- Day-Ahead price-profile chart;
- complete price table;
- Spanish local time;
- UTC time;
- source version.

The sample Home, Counter, Weather, and Weather Forecast elements were removed.

The Day-Ahead Prices dashboard is now the application landing page.

---

## 15. Configuration

### API configuration

The API configuration is stored in:

```text
src/SpanishDayAhead.Api/appsettings.json
```

It includes settings for:

- SQLite connection string;
- OMIE base URL;
- OMIE HTTP timeout;
- Quartz import interval;
- maximum source version;
- delivery-day offset;
- logging.

### Web configuration

The Web configuration is stored in:

```text
src/SpanishDayAhead.Web/appsettings.json
```

During local development, the Web application uses:

```text
https://localhost:7086/
```

as the REST API base address.

No password, API token, or private key is required for the public OMIE download workflow.

---

## 16. Prerequisites

Required software:

- .NET 10 SDK
- Git
- Visual Studio Code or Visual Studio
- Internet access for OMIE downloads
- trusted local ASP.NET Core HTTPS certificate

Check the installed .NET version:

```cmd
dotnet --version
```

Check the installed Git version:

```cmd
git --version
```

Trust the local development certificate when required:

```cmd
dotnet dev-certs https --trust
```

---

## 17. Clone the Repository

Clone the public GitHub repository:

```cmd
git clone https://github.com/funayama-ai/spanish-day-ahead-market.git
```

Enter the project folder:

```cmd
cd spanish-day-ahead-market
```

---

## 18. Restore and Build

Run all commands from the solution root.

Restore dependencies:

```cmd
dotnet restore
```

Build the full solution:

```cmd
dotnet build
```

Expected result:

```text
Build succeeded
```

Run the complete test suite:

```cmd
dotnet test
```

Expected verified result:

```text
Total:     22
Succeeded: 22
Failed:    0
Skipped:   0
```

### 18.1 Clean-clone reproduction check

A clean-clone check should be performed from a newly cloned repository with no generated SQLite database file.

From the repository root:

```cmd
dotnet restore
dotnet build
dotnet test
```

Confirm that the following file does not exist before the first API startup:

```text
src/SpanishDayAhead.Api/Data/spanish-day-ahead.db
```

No manual `dotnet ef database update` command should be required.

---

## 19. Run the API and Quartz Scheduler

Open the first terminal and run:

```cmd
dotnet run --project src\SpanishDayAhead.Api\SpanishDayAhead.Api.csproj --launch-profile https
```

The API is available at:

```text
https://localhost:7086
```

Keep this terminal running.

At startup, the API first applies pending EF Core migrations. Quartz.NET starts only after database migration has completed successfully.

Expected clean-start sequence:

```text
Applying pending database migrations.
Applying migration '20260710165436_InitialCreate'.
Database migrations applied successfully.
Scheduler QuartzScheduler_$_NON_CLUSTERED started.
```

On the first successful import into an empty database, the result should be approximately:

```text
parsed: 96
inserted: 96
updated: 0
ignored: 0
```

On a later import of the unchanged revision, duplicate prevention should produce:

```text
parsed: 96
inserted: 0
updated: 0
ignored: 96
```

A `404 Not Found` response for a later OMIE revision is expected when that revision has not yet been published. In that case, revision checking stops normally.

The following error must not appear after a successful clean startup:

```text
SQLite Error 1: 'no such table: DayAheadPrices'
```

The API process also starts the Quartz.NET scheduler.

---

## 20. Run the Blazor Web Application

Open a second terminal and run:

```cmd
dotnet run --project src\SpanishDayAhead.Web\SpanishDayAhead.Web.csproj --launch-profile https
```

The Web application is available at:

```text
https://localhost:7289
```

Open this address in a browser:

```text
https://localhost:7289
```

Both applications must be running for the dashboard to retrieve data through the REST API.

---

## 21. Stop the Applications

In the Web terminal, press:

```text
Ctrl + C
```

In the API and Quartz terminal, press:

```text
Ctrl + C
```

A successful Quartz shutdown includes:

```text
Scheduler QuartzScheduler_$_NON_CLUSTERED Shutdown complete.
```

---

## 22. Manual API Testing

Manual API requests are stored in:

```text
src/SpanishDayAhead.Api/SpanishDayAhead.Api.http
```

The file contains:

- JSON request;
- semicolon-separated text request;
- missing-delivery-date request.

The VS Code REST Client extension displays a:

```text
Send Request
```

link above each HTTP request.

---

## 23. Automated Tests

Run the complete test suite with:

```cmd
dotnet test
```

Current verified result:

```text
Total:     22
Succeeded: 22
Failed:    0
Skipped:   0
```

The test suite contains:

```text
19 unit tests
3 REST API integration tests
```

### Unit-test coverage

The unit tests cover:

- standard 96-period files;
- 92-period spring DST days;
- 100-period autumn DST days;
- negative Spanish prices;
- invalid file header;
- missing final marker;
- delivery-date and filename mismatch;
- missing periods;
- duplicated periods;
- downloader response handling;
- import-service behavior;
- inserted-record counting;
- updated-record counting;
- ignored-record counting.

### REST API integration-test coverage

The integration tests automatically verify:

1. A JSON request returns `200 OK` and 96 records.
2. A `text/plain` request returns `200 OK`, one header, and 96 records.
3. A missing delivery date returns `404 Not Found`.

The integration tests use:

- `WebApplicationFactory<Program>`;
- an isolated temporary SQLite database;
- the real API controller;
- the real repository;
- the ASP.NET Core request pipeline.

The integration tests do not require ports `7086` or `7289` to be running.

---

## 24. Verified Demonstration

The application was live-tested using:

```text
marginalpdbc_20260711.1
```

Verified results:

| Item | Result |
|---|---:|
| Delivery date | 2026-07-11 |
| Bidding zone | ES |
| Resolution | 15 minutes |
| Records | 96 |
| Average price | 87.32 EUR/MWh |
| Minimum price | -0.01 EUR/MWh |
| Maximum price | 160.31 EUR/MWh |
| Source version | 1 |

The real OMIE data was successfully:

- downloaded;
- parsed;
- converted to UTC;
- stored in SQLite;
- protected against duplicate insertion;
- retrieved as JSON;
- retrieved as semicolon-separated text;
- displayed in the Blazor dashboard.

---

## 25. Observed Price Profile

The verified delivery-day profile shows:

- high prices during the early morning;
- a strong decline during the morning;
- prices close to zero around midday and the afternoon;
- a strong increase during the evening;
- a small negative minimum price;
- an evening peak close to the daily maximum.

The 15-minute resolution reveals short-term market-price movements that would be less visible after hourly aggregation.

---

## 26. Logging

The application produces logs for:

- HTTP requests;
- OMIE downloads;
- unavailable OMIE files;
- file parsing;
- import results;
- database commands;
- REST API queries;
- Quartz job execution;
- graceful shutdown.

Example repeated-import result:

```text
Parsed: 96
Inserted: 0
Updated: 0
Ignored: 96
```

Example API result:

```text
Returning 96 Day-Ahead prices for 07/11/2026.
```

---

## 27. Restart Behaviour

The SQLite database persists when the API application stops.

After the API restarts:

- stored Day-Ahead records remain available;
- the REST API can return the saved data;
- pending EF Core migrations are checked automatically;
- Quartz.NET starts again automatically.

Historical missed-run backfilling is not currently implemented.

---

## 28. Git and Repository Hygiene

The repository contains a root `.gitignore`.

The following generated or local files are excluded:

- `bin`;
- `obj`;
- `.vs`;
- SQLite database files;
- test-result files;
- local secret files;
- logs;
- ZIP archives;
- temporary submission folders.

The public repository contains source code, EF Core migration source files, and documentation, but does not contain the locally generated SQLite database.

Repository URL:

```text
https://github.com/funayama-ai/spanish-day-ahead-market
```

---

## 29. Assumptions

The current implementation assumes that:

- the OMIE file structure remains compatible with the implemented parser;
- the Spanish price remains in the expected source column;
- OMIE source versions are positive integers;
- delivery periods are published in continuous order;
- local development uses HTTPS;
- API and Web applications run as separate processes;
- the primary demonstration bidding zone is Spain.

---

## 30. Current Limitations

- Historical missed-run backfilling is not implemented.
- Public-cloud deployment is outside the current scope.
- Authentication and user accounts are not implemented.
- The dashboard focuses on one delivery date at a time.
- The project currently focuses on the Spanish bidding zone.
- Long-term historical analytics are not implemented.
- Automated browser-interface tests are not included.

---

## 31. Possible Future Improvements

Potential future work includes:

- historical backfill by delivery-date range;
- multi-day and monthly price analysis;
- CSV download from the dashboard;
- database health endpoint;
- Quartz scheduler-status endpoint;
- support for additional bidding zones;
- chart zoom and interactive tooltips;
- price-duration curve;
- volatility KPIs;
- hourly aggregation;
- automated deployment;
- Docker support;
- GitHub Actions build and test workflow;
- public cloud hosting.

---

## 32. Technology Stack

- .NET 10
- C#
- ASP.NET Core
- REST API
- Blazor Server
- Entity Framework Core
- SQLite
- Quartz.NET
- `HttpClient`
- xUnit
- `Microsoft.AspNetCore.Mvc.Testing`
- Git
- GitHub
- Visual Studio Code
- VS Code REST Client

---

## 33. AI-Assisted Development Disclosure

AI assistance was used during the development process for:

- explaining .NET and ASP.NET Core concepts;
- proposing project structure;
- generating initial code drafts;
- reviewing build and test errors;
- suggesting debugging steps;
- preparing documentation.

All generated code was manually copied, executed, reviewed, tested, and corrected in the local development environment.

The final verified application result is based on:

- successful local builds;
- 22 passing automated tests;
- live OMIE data retrieval;
- SQLite database verification;
- successful automatic migration from an empty database;
- manual REST API testing;
- manual Blazor dashboard testing;
- successful publication to GitHub.

---

## 34. Current Project Status

The core application functionality is complete.

Completed:

- clean solution structure;
- OMIE file downloader;
- OMIE file parser;
- 15-minute interval support;
- CET/CEST handling;
- UTC conversion;
- Day-Ahead import service;
- SQLite persistence;
- automatic EF Core migration at API startup;
- clean empty-database startup verification;
- duplicate prevention;
- OMIE revision handling;
- Quartz.NET scheduling;
- JSON REST API;
- semicolon-separated response;
- Blazor dashboard;
- application branding cleanup;
- sample template removal;
- unit tests;
- REST API integration tests;
- complete README documentation;
- clean source-code submission package;
- Git repository initialization;
- public GitHub repository publication.

Current verified test result:

```text
22 passed
0 failed
```

Current verified clean-start result:

```text
Automatic migration: succeeded
Initial OMIE import: 96 inserted
Duplicate re-import: 96 ignored
```

Public repository:

```text
https://github.com/funayama-ai/spanish-day-ahead-market
```

Remaining optional work:

- production deployment;
- historical missed-run backfill;
- extended historical analytics.