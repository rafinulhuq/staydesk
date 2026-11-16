# StayDesk — Software Requirements Specification (SRS)

**Revision 2** · 2026-11-16 · Author: Tazkir Hossain
Revision 2 applies `SRS-revision-plan.md`. The change log is in Appendix A.

---

## 0. Decisions in effect

Revision 2 assumes these decisions. Changing one affects every section listed.

| ID | Decision | Chosen | Affects |
|----|----------|--------|---------|
| D1 | Database | **PostgreSQL 16+** | §3, §5, BOOK-2, §9 |
| D2 | Runtime | **.NET 10 (LTS)**, **latest stable Angular major** at project start | §2, §3, §12 |
| D3 | Refresh token storage | **localStorage** (simple cross-origin deploy). XSS tradeoff documented in the README | §7, NFR-1 |
| D4 | Price rule priority | **Removed.** All matching rules stack multiplicatively | PRICE-3, §5 |
| D5 | Public demo protection | **Nightly re-seed** + demo accounts exempt from lockout | AUTH-4/5, OPS-1 |

---

## 1. Introduction

StayDesk is a full-stack accommodation booking platform with a public booking site and an internal back-office, built with ASP.NET Core and Angular. It is a portfolio project aimed at an Associate Software Engineer (.NET / Angular) role at a travel and accommodation company.

**Purpose.** This SRS defines everything needed to build StayDesk end to end. It is written so an AI coding assistant or a developer can implement it without further clarification. Where a choice affects the data model, API contract or concurrency, it is decided here.

**Scope.**
In scope: property and room management, availability search, booking with double-booking prevention, rule-based pricing, guest accounts, staff walk-in bookings, a staff back-office with dashboard, live occupancy calendar, audit log, English/German UI, tests, Docker, CI and cloud deployment.
Out of scope: real payment processing (simulated only), real OTA integrations, native mobile apps, and email delivery beyond a development SMTP catcher.

**Definitions.**

| Term | Meaning |
|------|---------|
| Property | A hotel, guesthouse or apartment building that contains rooms |
| Room | A bookable unit with a type, capacity and base price |
| Booking | A reservation of one room for a date range (check-in inclusive, check-out exclusive) |
| Night / room-night | One calendar night of one room between check-in and check-out |
| Check-in instant | `CheckIn` date + `Property.CheckInTime`, in `Property.TimeZone` |
| Active booking | Status Confirmed or CheckedIn, or status Pending with `ExpiresAt > now` |
| Price rule | A rule that adjusts the nightly price (season, weekend, length of stay, last minute) |
| Back-office | The staff and admin area of the app |
| Audit log | An append-only record of who changed what and when |
| Occupancy | Occupied room-nights ÷ available room-nights in a period, as a percentage (exact formula in DASH-1) |
| Revenue | Nightly prices of stayed or confirmed nights in the period + cancellation and no-show fees charged in the period (exact formula in DASH-1) |

---

## 2. Overall description

StayDesk has two faces on one backend: guests search and book rooms, while staff and admins run properties and bookings from the back-office.

**User roles.** Roles are hierarchical. An Admin user is assigned both the Staff and Admin roles, and authorization uses the policies `GuestOnly`, `StaffOrAdmin` and `AdminOnly`.

| Role | Can do |
|------|--------|
| Visitor (not logged in) | Search availability, view properties and rooms, see prices, register, log in |
| Guest | Everything a visitor can, plus create bookings, view and cancel own bookings, edit own profile |
| Staff | Back-office access: view and manage all bookings, create walk-in bookings, check guests in and out, view calendar, dashboard and per-booking history |
| Admin | Everything Staff can, plus manage properties, rooms, price rules, users and roles, and view the full audit log |

**Assumptions.**
- All prices are in EUR, stored as `numeric(10,2)` (`decimal` in C#).
- All dates are calendar dates (no time of day) in the property's local time zone, `Europe/Berlin` by default. "Today" always means today in the property's time zone.
- Check-in date is inclusive and check-out date is exclusive, so a booking 10–12 March holds the nights of 10 and 11 March.
- Payment is simulated. A guest booking moves to Confirmed when the guest clicks "Pay" on a mock payment step. A staff walk-in booking is created directly as Confirmed.

**Constraints.**
- Backend: **.NET 10 (LTS)**. Frontend: **latest stable Angular major** with standalone components. Record the exact versions in the README.
- Must run locally with one command (`docker compose up`).
- Must be deployable on a free or near-free cloud tier. The design must stay correct when the host sleeps (no correctness may depend on a background job running on time).
- Code, comments, commit messages and docs in English. UI available in English and German.

---

## 3. Tech stack and architecture

The backend follows Clean Architecture (Domain, Application, Infrastructure, Api), and the frontend is one Angular app with lazy-loaded public, account and admin areas.

The Angular app calls the API over REST and receives live booking events over SignalR. All four backend layers run in one API container, and Docker Compose starts the database and Mailpit beside it.

```
 Angular SPA ──REST /api/v1──▶ ┌──────────── API container ────────────┐
      ▲                        │ Api → Application → Domain            │
      └──SignalR /hubs/────────│ Infrastructure (EF Core, email, audit)│
                               └───────────────┬───────────────────────┘
                                               ▼
                                       PostgreSQL     Mailpit (dev SMTP)
```

| Layer | Technology |
|-------|------------|
| Backend framework | ASP.NET Core 10 Web API (controllers) |
| ORM and database | Entity Framework Core 10 + Npgsql provider + PostgreSQL 16+ (extension `btree_gist`), snake_case names via `EFCore.NamingConventions` |
| Auth | ASP.NET Core Identity + JWT access tokens (15 min) + rotating refresh tokens (7 days) |
| Validation | FluentValidation |
| Mapping | Manual mapping (no mapping library in business logic) |
| Real-time | SignalR hub for booking and calendar updates |
| Background jobs | `BackgroundService` (hosted service) with `PeriodicTimer` |
| Logging | Serilog to console and file, with request correlation IDs |
| API docs | OpenAPI document + Swagger UI with JWT support |
| Frontend | Angular (latest stable), standalone components, Angular Material, RxJS, Signals for local component state |
| Charts | ng2-charts (Chart.js) |
| i18n | ngx-translate with `en.json` and `de.json` |
| Backend testing | xUnit, Shouldly, Testcontainers (PostgreSQL) |
| Frontend testing | Vitest (the Angular CLI default since Angular 21; Karma is deprecated) |
| DevOps | Docker, Docker Compose, GitHub Actions |
| Dev email | Mailpit container |

> Version note (checked 2026-11-16): .NET 8 and 9 reached end of support on 10 Nov 2026, and .NET 10 is the current LTS. Angular 22 is the current major (June 2026). Angular is moving to a 12-month major cycle, so v23 is expected around June 2027. Use ngx-translate **v18** (v17 is EOL), ng2-charts **v10+**, and Shouldly (BSD-3-Clause). FluentAssertions v8+ is not used: it needs a paid licence for commercial use. To snake_case the database names, add `EFCore.NamingConventions` (community package; v10.x targets EF Core 10). Re-check versions and licences when the project starts.

**Solution structure.**

```
StayDesk/
├─ src/
│  ├─ StayDesk.Domain/          # entities, enums, domain exceptions, no dependencies
│  ├─ StayDesk.Application/     # services, DTOs, validators, interfaces, pricing engine, domain events
│  ├─ StayDesk.Infrastructure/  # EF Core DbContext, migrations, repositories, email, audit interceptor, jobs
│  └─ StayDesk.Api/             # controllers, SignalR hub, middleware, Program.cs
├─ tests/
│  ├─ StayDesk.UnitTests/
│  └─ StayDesk.IntegrationTests/
├─ client/staydesk-web/         # Angular app
│  └─ src/app/
│     ├─ core/      # auth service, interceptors, guards, SignalR service
│     ├─ shared/    # reusable components, pipes, models
│     ├─ public/    # home, search, property, room, booking flow
│     ├─ account/   # login, register, my bookings, profile
│     └─ admin/     # dashboard, bookings, calendar, properties, rooms, pricing, users, audit
├─ docs/SRS.md
├─ docker-compose.yml
├─ .env.example
├─ .github/workflows/ci.yml
├─ CLAUDE.md
└─ README.md
```

**Dependency rule.** Domain depends on nothing. Application depends on Domain. Infrastructure depends on Application. Api depends on Application, and references Infrastructure **only for DI registration in `Program.cs`**. Controllers stay thin: they validate input, call one application service, and return a DTO.

**Side effects rule.** SignalR events, audit entries and emails are triggered from the Application layer (or the EF interceptor), never from controllers, so background jobs and staff actions are covered the same way as guest actions.

---

## 4. Functional requirements

The system has 9 modules. Every requirement has an ID so tests and commits can reference it. "Must" items are the minimum to ship; "Should" items make the project stand out. Every item referenced by an acceptance criterion in §11 is a Must.

### 4.1 Authentication and users (AUTH)

- **AUTH-1 (Must)** Visitors register with first name, last name, email and password. Email is unique (case-insensitive). The password needs 8+ characters, one digit and one uppercase letter.
- **AUTH-2 (Must)** Login returns a JWT access token and a refresh token. Refresh rotates the token: the old token is revoked and `ReplacedByTokenHash` points to the new one. If a revoked token is presented again (reuse), every token in that chain is revoked. Logout revokes the current refresh token.
- **AUTH-3 (Must)** Roles: Guest, Staff, Admin (Admin users also hold Staff). New registrations get Guest. Only Admin can change roles. An Admin cannot remove their own Admin role.
- **AUTH-4 (Must)** A seed creates one Admin, one Staff and one Guest demo account. Their passwords come from configuration (`Seed__AdminPassword` etc.), and the README documents the demo values.
- **AUTH-5 (Should)** An account locks for 5 minutes after 5 failed logins. Demo accounts (flag `IsDemo`) are exempt, so the public demo can't be locked out.
- **AUTH-6 (Must)** Deactivated users (`IsActive = false`) get 401 on login and refresh, and their refresh tokens are revoked on deactivation.
- **AUTH-7 (Must)** A guest edits their own first name, last name and phone number. Email changes are out of scope.

### 4.2 Properties and rooms (PROP)

- **PROP-1 (Must)** Admin creates, edits, deactivates and reactivates properties: name, city, address, description, image URL, check-in time, check-out time, time zone.
- **PROP-2 (Must)** Admin creates, edits, deactivates and reactivates rooms: property, room number, type (Single, Double, Twin, Suite, Apartment), max guests (1–10), base price per night, amenities, image URLs.
- **PROP-3 (Must)** Deactivated properties and rooms are hidden from public search and listings but keep their history (soft delete). Bookings, calendar and audit views still show them. Admin lists can include them with `includeInactive=true`.
- **PROP-4 (Must)** Seed data: at least 3 properties in Berlin, Munich and Hamburg, each with 6–10 rooms, plus 8–12 amenities.
- **PROP-5 (Must)** Rooms that have active bookings can be deactivated, but the API returns a warning listing those bookings. Existing bookings stay valid.

### 4.3 Search and availability (SRCH)

- **SRCH-1 (Must)** Search by city, check-in, check-out and guests. Returns available active rooms in active properties, each with the total price for the stay.
- **SRCH-2 (Must)** Validation: check-in is today or later (property time zone), check-out is after check-in, stay is 1–30 nights, guests 1–10.
- **SRCH-3 (Must)** A room is available when no **active booking** overlaps the range. An active booking is one with status Confirmed or CheckedIn, or status Pending with `ExpiresAt > now`. Overlap rule: `existing.CheckIn < requested.CheckOut AND existing.CheckOut > requested.CheckIn`. Rooms with `MaxGuests < guests` are excluded.
- **SRCH-4 (Must)** Results support sorting (`price`, `capacity`, ascending or descending), filters (room type, `maxPrice` = maximum **total for the stay**, amenities = rooms having **all** selected amenities) and server-side pagination. Because totals depend on price rules, price sorting and `maxPrice` filtering happen in memory after the SQL availability filter. This is acceptable at the project's scale and documented as a known limit.
- **SRCH-5 (Should)** The room detail page shows a 60-day availability mini-calendar.

### 4.4 Pricing engine (PRICE)

- **PRICE-1 (Must)** Nightly price = `BasePrice × Π (1 + PercentChange / 100)` over all active rules that match that night, rounded to 2 decimals with `MidpointRounding.AwayFromZero`. A nightly price is never below 1.00 EUR. Total = sum of the rounded nightly prices, so the total always equals the breakdown.
- **PRICE-2 (Must)** Rule types. `PercentChange` is signed (+20 = 20% surcharge, −10 = 10% discount), allowed range −90 to +300.
  - **Seasonal:** matches a night whose date is within `StartDate`..`EndDate` (both inclusive).
  - **Weekend:** matches Friday and Saturday nights.
  - **LengthOfStay:** matches every night when the stay has at least `MinNights` nights.
  - **LastMinute:** matches every night when `CheckIn − today` (in days, property time zone) is ≤ `DaysBeforeCheckIn`.
- **PRICE-3 (Must)** A rule attaches to a property (applies to all its rooms) or to one room (`RoomId` set). Property and room rules both apply. There is no priority: multiplication is order-independent.
- **PRICE-4 (Must)** The quote endpoint returns a per-night breakdown: date, base price, each applied rule (name, type, percent), and the rounded nightly price, plus the total.
- **PRICE-5 (Must)** The price at booking time (`TotalPrice` + `PriceBreakdown` JSON) is stored on the booking and never recalculated afterwards.
- **PRICE-6 (Must)** The pricing engine is a pure, unit-tested class (Strategy pattern, one class per rule type). It takes `today` as an input and never reads the clock.
- **PRICE-7 (Must)** `POST /bookings` recomputes the price on the server. If the client sends `expectedTotal` and it differs from the new total, the API returns **409** `price_changed` with the new quote, and no booking is created.
- **PRICE-8 (Should)** Admin can preview an unsaved rule against a room and date range before saving it.

### 4.5 Booking (BOOK)

- **BOOK-1 (Must)** A logged-in Guest books an active room for dates and a guest count. Validation from SRCH-2 applies, plus `guests ≤ Room.MaxGuests`. The guest-details step captures `GuestFullName`, `GuestEmail`, `GuestPhone` (pre-filled from the profile) and optional `SpecialRequests` (max 500 chars). A booking reference like `SD-2026-000123` is generated from a database sequence (year = creation year in UTC).
- **BOOK-2 (Must)** Double-booking prevention:
  1. Start a transaction (READ COMMITTED).
  2. Lock the room row: `SELECT … FROM rooms WHERE id = @roomId FOR UPDATE`. This serialises all booking attempts for the same room.
  3. Mark Pending bookings of that room overlapping the range with `ExpiresAt <= now` as Cancelled (expired lazily; see BOOK-3).
  4. Re-check availability (SRCH-3). If taken, return **409** `room_unavailable`.
  5. Insert the booking and commit.

  As a database-level safety net, `bookings` has an exclusion constraint:
  `EXCLUDE USING gist (room_id WITH =, daterange(check_in, check_out, '[)') WITH &&) WHERE (status IN ('Pending','Confirmed','CheckedIn'))`.
  A violation (SQLSTATE `23P01`) is also mapped to **409** `room_unavailable`. If two requests race, exactly one succeeds and every other request gets 409, never 500.
- **BOOK-3 (Must)** Status flow:
  - Pending → Confirmed (pay) → CheckedIn → CheckedOut.
  - Pending or Confirmed → Cancelled.
  - Confirmed → NoShow (staff, from the day after `CheckIn` onward).
  - A Pending booking has `ExpiresAt = CreatedAt + 15 min`. An expired Pending booking never blocks availability, even before it is marked Cancelled. A background job runs every minute and marks expired Pending bookings Cancelled (audited as user `system`, publishes `BookingCancelled`). The job only tidies up; correctness never depends on it.
  - `POST /bookings/{id}/pay` returns **409** `booking_expired` when `now >= ExpiresAt`.
- **BOOK-4 (Must)** The Booking entity enforces status transitions with methods (`Confirm`, `CheckIn`, `CheckOut`, `Cancel`, `MarkNoShow`), not only the controller. Invalid transitions throw a domain exception mapped to **400** `invalid_status_transition`.
  - Check-in is allowed from the `CheckIn` date (property time zone) until the `CheckOut` date.
  - Check-out is allowed any time after check-in. An early departure keeps the full stored price.
  - The Booking entity has a concurrency token, so a concurrent pay and expiry, or two staff actions, can't both succeed. The loser gets **409** `concurrency_conflict`.
- **BOOK-5 (Must)** Cancellation policy:
  - Free until 48 hours before the **check-in instant**. After that, the fee is the first night's price (`PriceBreakdown[0].price`).
  - `RefundAmount = amount paid − fee`, never below 0. Cancelling an unpaid Pending booking gives fee 0 and refund 0.
  - Staff and Admin may pass `waiveFee: true`, which is recorded in the audit log.
  - CheckedIn, CheckedOut, NoShow and Cancelled bookings cannot be cancelled.
  - A NoShow is charged the first night (stored in `CancellationFee`).
- **BOOK-6 (Must)** Guests see and cancel only their own bookings. Staff and Admin see all of them.
- **BOOK-7 (Must)** A confirmation email is sent when a booking becomes Confirmed (to Mailpit in development). Email failure is logged and never fails the booking.
- **BOOK-8 (Must)** Idempotency: `POST /bookings` requires an `Idempotency-Key` header (UUID). The key is unique per (`CreatedById`, key).
  - Same key with the same request body → **200** with the original booking.
  - Same key with a different body → **422** `idempotency_key_reused`.
  - A double click never creates two bookings.
- **BOOK-9 (Must)** Walk-in bookings: Staff and Admin create a booking for an existing guest (`guestId`) or for new guest details (first name, last name, email, phone).
  - New guest details create a Guest account without a password (`IsActive = true`, no login until the password is set; password reset itself is out of scope).
  - Walk-in bookings skip Pending: they are created as **Confirmed**, with a Payment `Provider = "Desk"`.
  - Check-in today is allowed.
  - `CreatedById` records the staff user.

### 4.6 Back-office bookings (BO)

- **BO-1 (Must)** Staff see a bookings table with filters (property, status, date range overlapping the stay, guest name or reference), sorting and server-side paging.
- **BO-2 (Must)** Staff open a booking detail, change status (check in, check out, cancel with optional fee waiver, no-show) and edit internal notes (max 2,000 chars).
- **BO-3 (Should)** Staff export the filtered bookings to CSV (UTF-8 with BOM, so Excel opens it with umlauts intact; max 10,000 rows).
- **BO-4 (Must)** Staff see one booking's full change history (from the audit log) on the booking detail page, without access to the Admin audit log.

### 4.7 Occupancy calendar and real time (CAL)

- **CAL-1 (Must)** A calendar grid shows rooms as rows and dates as columns for a chosen property, in a 14- or 30-day window. Booked cells show the guest name and status, using colour **and** a text label or icon. Deactivated rooms with bookings in the window are shown greyed out.
- **CAL-2 (Must)** When any booking is created, changed or cancelled (including by the expiry job), the SignalR hub pushes an event to the group `property-{propertyId}` and to the group `dashboard`. Every open calendar and dashboard updates within 2 seconds without a page reload. Clients refetch only the affected data and debounce bursts (≥ 500 ms).
- **CAL-3 (Should)** Clicking an empty cell opens the walk-in booking form (BOOK-9) with room and date pre-filled.

### 4.8 Dashboard (DASH)

- **DASH-1 (Must)** KPI cards (optionally filtered by property):
  - **Today's arrivals:** Confirmed bookings with `CheckIn = today`.
  - **Today's departures:** CheckedIn bookings with `CheckOut = today`.
  - **Current occupancy %:** room-nights for tonight with status CheckedIn or Confirmed ÷ active rooms.
  - **Revenue this month (EUR):** sum of `PriceBreakdown` nightly prices for nights in this month on Confirmed, CheckedIn or CheckedOut bookings, + `CancellationFee` of bookings cancelled or marked NoShow this month.
- **DASH-2 (Must)** Charts:
  - Bookings created per day for the last 30 days (line).
  - Revenue by property this month (bar).
  - Occupancy by property for the next 30 days (bar).
- **DASH-3 (Must)** Dashboard data comes from one aggregate endpoint using SQL aggregation. It never loads all bookings into memory. Nightly revenue may use `jsonb_array_elements` on `PriceBreakdown` or a `BookingNights` table (implementer's choice, stated in the README).

### 4.9 Audit log (AUD)

- **AUD-1 (Must)** Every create, update and soft delete on Booking, Room, Property, PriceRule and user roles writes an AuditEntry: who (user ID + display name; `system` for background jobs), when, entity, entity ID, action, changed fields with old and new values as JSON, and correlation ID. Soft deletes are recorded as action `Deactivated`. Sensitive fields (password hashes, security stamps, tokens) are never recorded.
- **AUD-2 (Must)** Implemented with an EF Core `SaveChangesInterceptor`, not by hand in each service. All entity IDs are client-generated GUID v7 values, so IDs are known before saving.
- **AUD-3 (Must)** Admin views the audit log with filters (entity, user, date range) and can open the full history of one booking. Staff can read only a single booking's history (BO-4).
- **AUD-4 (Must)** Audit entries are append-only: no update or delete endpoint exists.

---

## 5. Data model

Booking is the centre of the model, linking a Room, a guest User, the staff member who created it, and the price snapshot taken at booking time. All IDs are `uuid` (GUID v7, generated in the application).

| Entity | Fields | Relationships and constraints |
|--------|--------|-------------------------------|
| User (Identity) | Id, Email, FirstName, LastName, PhoneNumber, CreatedAt, IsActive, IsDemo, PseudonymisedAt | Roles via Identity; 1–many Booking (as guest) |
| RefreshToken | Id, UserId, TokenHash, ExpiresAt, CreatedAt, RevokedAt, ReplacedByTokenHash | Many–1 User; only hashes stored; index TokenHash |
| Property | Id, Name, City, Address, Description, ImageUrl, CheckInTime, CheckOutTime, TimeZone (IANA, default `Europe/Berlin`), IsActive, CreatedAt | 1–many Room, 1–many PriceRule; index City |
| Room | Id, PropertyId, RoomNumber, Type (enum), MaxGuests, BasePrice `numeric(10,2)`, ImageUrls (`jsonb` string array), IsActive, Version (concurrency token mapped to PostgreSQL `xmin`) | Many–1 Property; unique (PropertyId, RoomNumber); check MaxGuests 1–10 |
| Amenity | Id, Name, Icon | Many–many Room via RoomAmenity |
| RoomAmenity | RoomId, AmenityId | PK (RoomId, AmenityId) |
| Booking | Id, Reference, RoomId, GuestId, CreatedById, CheckIn (`date`), CheckOut (`date`), Guests, GuestFullName, GuestEmail, GuestPhone, SpecialRequests, Status (enum), TotalPrice, PriceBreakdown (`jsonb`), CancellationFee, RefundAmount, FeeWaived, InternalNotes, IdempotencyKey, CreatedAt, ExpiresAt (nullable), ConfirmedAt, CheckedInAt, CheckedOutAt, CancelledAt, Version (concurrency token, `xmin`) | Many–1 Room, many–1 User (guest), many–1 User (creator); unique Reference; unique (CreatedById, IdempotencyKey); check CheckOut > CheckIn; exclusion constraint per BOOK-2; index (RoomId, CheckIn, CheckOut, Status) |
| PriceRule | Id, PropertyId, RoomId (nullable), Name, Type (enum), PercentChange `numeric(6,2)`, StartDate, EndDate, MinNights, DaysBeforeCheckIn, IsActive | Many–1 Property; optional many–1 Room; type-specific fields validated per PRICE-2 |
| Payment (mock) | Id, BookingId, Amount, Status (Succeeded, Refunded), Provider (`Mock` or `Desk`), CreatedAt | 1–1 Booking |
| AuditEntry | Id, UserId (nullable), UserDisplayName, EntityName, EntityId, Action, Changes (`jsonb`), Timestamp, CorrelationId | No foreign key (survives user erasure); index (EntityName, EntityId); index Timestamp |

**Enums.**
- RoomType: Single, Double, Twin, Suite, Apartment.
- BookingStatus: Pending, Confirmed, CheckedIn, CheckedOut, Cancelled, NoShow.
- PriceRuleType: Seasonal, Weekend, LengthOfStay, LastMinute.
- AuditAction: Created, Updated, Deactivated, Reactivated.

**Data rules.**
- Money is `decimal` in C# and `numeric(10,2)` in SQL, never `double` or `float`.
- Dates use `DateOnly` in C# and `date` in SQL. Timestamps use `DateTimeOffset` in UTC (`timestamptz`).
- **Soft delete** applies only to Property, Room, PriceRule and User (`IsActive` + a global query filter). Booking, Payment, RefreshToken and AuditEntry are never deleted. Queries that load a booking's Room or Property use `IgnoreQueryFilters()`, so history survives deactivation.
- Enums are stored as strings.
- **Seed data** is created by an idempotent `DbSeeder`:
  - On startup when `Seed__DemoData=true` (true in Development and on the public demo).
  - It includes about 40 bookings spread across the last 30 and next 30 days, relative to today.
  - On the demo, the nightly reset job (OPS-1) re-creates them.
- **Load seed:** `Seed__LoadTest=true` creates ~50 rooms and ~10,000 bookings for performance measurement (NFR-6).

---

## 6. REST API specification

All endpoints live under `/api/v1`, return JSON, and use RFC 9457 Problem Details for errors (with a machine-readable `code` extension such as `room_unavailable`).

| Method | Endpoint | Access | Purpose |
|--------|----------|--------|---------|
| POST | /auth/register | Public | Create Guest account |
| POST | /auth/login | Public | Get access + refresh token |
| POST | /auth/refresh | Public | Rotate refresh token |
| POST | /auth/logout | Logged in | Revoke refresh token |
| GET | /auth/me | Logged in | Current user, roles, IsActive |
| PUT | /users/me | Logged in | Edit own first name, last name, phone (AUTH-7) |
| GET | /properties?city&includeInactive | Public (`includeInactive`: Admin) | List properties |
| GET | /properties/{id} | Public | Property detail with active rooms |
| POST / PUT / DELETE | /properties, /properties/{id} | Admin | Manage properties (DELETE = deactivate) |
| POST | /properties/{id}/reactivate | Admin | Reactivate |
| GET | /rooms/search?city&checkIn&checkOut&guests&type&maxPrice&amenities&sort&dir&page&pageSize | Public | Availability search with total prices |
| GET | /rooms/{id} | Public | Room detail |
| GET | /rooms/{id}/availability?from&to | Public | Booked dates for mini-calendar (max 90 days) |
| POST / PUT / DELETE | /rooms, /rooms/{id} | Admin | Manage rooms (DELETE = deactivate, PROP-5 warning) |
| POST | /rooms/{id}/reactivate | Admin | Reactivate |
| GET | /amenities | Public | Amenity list for filters and forms |
| POST | /pricing/quote | Public | Per-night price breakdown for room + dates |
| POST | /pricing/preview | Admin | Quote with an unsaved rule added (PRICE-8) |
| GET / POST / PUT / DELETE | /price-rules, /price-rules/{id} | Admin | Manage price rules (DELETE = deactivate) |
| POST | /bookings | Guest, Staff, Admin | Create booking (`Idempotency-Key` header required; staff body includes `guestId` or `newGuest`) |
| GET | /bookings/my | Guest | Own bookings (`?scope=upcoming\|past`) |
| GET | /bookings?propertyId&status&from&to&search&sort&dir&page&pageSize | Staff, Admin | Back-office list |
| GET | /bookings/{id} | Owner, Staff, Admin | Booking detail |
| GET | /bookings/{id}/history | Staff, Admin | Audit entries for one booking (BO-4) |
| POST | /bookings/{id}/pay | Owner | Mock payment, Pending → Confirmed |
| POST | /bookings/{id}/cancel | Owner, Staff, Admin | Cancel with fee calculation (`waiveFee`: Staff and Admin only) |
| GET | /bookings/{id}/cancellation-quote | Owner, Staff, Admin | Fee and refund preview before confirming |
| POST | /bookings/{id}/check-in | Staff, Admin | Confirmed → CheckedIn |
| POST | /bookings/{id}/check-out | Staff, Admin | CheckedIn → CheckedOut |
| POST | /bookings/{id}/no-show | Staff, Admin | Confirmed → NoShow |
| PATCH | /bookings/{id}/notes | Staff, Admin | Internal notes |
| GET | /bookings/export?…same filters | Staff, Admin | CSV download |
| GET | /calendar?propertyId&from&days | Staff, Admin | Room × date grid data (`days` = 14 or 30) |
| GET | /dashboard/summary?propertyId | Staff, Admin | KPIs and chart series |
| GET | /audit?entity&entityId&userId&from&to&page&pageSize | Admin | Audit log |
| GET | /users?search&page&pageSize | Admin | List users |
| PUT | /users/{id}/roles | Admin | Change roles |
| PUT | /users/{id}/status | Admin | Activate or deactivate (AUTH-6) |
| WS | /hubs/bookings | Staff, Admin | SignalR events: BookingCreated, BookingUpdated, BookingCancelled (payload: bookingId, propertyId, roomId, checkIn, checkOut, status) |
| GET | /health | Public | Liveness + database check (outside `/api/v1`) |

**API rules.**
- Paged responses use one shape: `{ items, page, pageSize, totalCount, totalPages }`. `pageSize` max 100.
- Status codes:
  - 200 OK.
  - 201 Created with a Location header.
  - 204 No Content.
  - 400 validation error or invalid status transition.
  - 401, 403, 404.
  - 409 conflict (`room_unavailable`, `price_changed`, `booking_expired`, `concurrency_conflict`).
  - 422 `idempotency_key_reused`.
  - 429 rate limited.
- Validation errors return field-level messages that the Angular forms show next to each field.
- A Guest requesting another guest's booking gets **404** (not 403), so booking IDs aren't revealed.
- Controllers never return EF entities, only DTOs.
- The OpenAPI document lists every endpoint with example requests, and Swagger UI has an Authorize button.

---

## 7. Frontend (Angular)

One Angular app with three lazy-loaded areas: public pages for everyone, account pages for guests, and an admin area guarded by the Staff or Admin role.

| Route | Page | Access | Key contents |
|-------|------|--------|--------------|
| / | Home | Public | Hero search bar (city, dates, guests), featured properties |
| /search | Search results | Public | Filters sidebar (type, max total price, amenities), sort, room cards with total price, pagination; filters synced to URL query params |
| /properties/:id | Property detail | Public | Gallery, description, rooms list, map placeholder |
| /rooms/:id | Room detail | Public | Photos, amenities, 60-day availability calendar, live price quote with per-night breakdown |
| /book/:roomId | Booking flow | Guest | 3-step stepper: review stay → guest details → mock payment with a 15-minute countdown; then a confirmation page with the reference. Handles `price_changed` and `booking_expired` |
| /login, /register | Auth | Public | Reactive forms with field-level server errors |
| /account/bookings | My bookings | Guest | Upcoming and past tabs; cancel button showing the fee (from `cancellation-quote`) before confirming |
| /account/profile | Profile | Guest | Edit name and phone |
| /admin | Dashboard | Staff, Admin | KPI cards, three charts, live updates |
| /admin/bookings | Bookings table | Staff, Admin | Filters, sort, server paging, CSV export, "New walk-in" button; row click opens a detail drawer |
| /admin/bookings/:id | Booking detail | Staff, Admin | Status actions, fee waiver, notes, price breakdown, history timeline (`/bookings/{id}/history`) |
| /admin/calendar | Occupancy calendar | Staff, Admin | Rooms × dates grid, colour + label by status, live updates, 14/30-day toggle, click empty cell → walk-in form |
| /admin/properties, /admin/rooms | Inventory | Admin | CRUD tables and forms, show-inactive toggle, reactivate |
| /admin/pricing | Price rules | Admin | Rule list, rule form, "preview price" for a room and date range |
| /admin/users | Users | Admin | User list, role editor, activate/deactivate |
| /admin/audit | Audit log | Admin | Filterable table, JSON diff view of old vs new values |

**Frontend rules.**
- Standalone components, `OnPush` change detection, typed reactive forms.
- `core/` holds:
  - `AuthService`: access token in memory, refresh token in localStorage (D3; tradeoff documented in the README).
  - An HTTP interceptor that adds the bearer token and, on 401, performs **one shared in-flight refresh** (concurrent 401s wait for the same refresh) and then retries once.
  - An error interceptor that shows a snackbar.
  - `roleGuard`.
- `BookingHubService` wraps SignalR and exposes events as RxJS observables. It reconnects automatically and refreshes the access token before reconnecting. The calendar and dashboard subscribe and refetch only the affected data, debounced.
- Global loading indicator. Every list has loading, empty and error states.
- Language switcher (EN / DE) in the header, persisted per viewer. Prices are formatted as EUR with the `de-DE` or `en-GB` locale, and dates follow the chosen locale.
- Responsive: public pages work at 360 px wide. Admin pages are desktop-first but usable on a tablet.
- Visual style: Angular Material with one custom theme (primary deep blue, accent amber), clean cards, consistent 8 px spacing.

---

## 8. Non-functional requirements

The app must be secure by default, correct under concurrent bookings, correct when the host sleeps, and fast enough that search answers in under 500 ms on the load seed.

| ID | Area | Requirement |
|----|------|-------------|
| NFR-1 | Security | Passwords hashed by Identity. JWT secret, connection strings, SMTP settings and seed passwords come from environment variables or user-secrets, never from committed files. `.env` is git-ignored. Only `.env.example` is committed |
| NFR-2 | Security | Role- and ownership-based authorization on every endpoint. A Guest requesting another guest's booking gets 404 |
| NFR-3 | Security | CORS allows only the configured frontend origin. `ForwardedHeaders` is configured for the proxy, so client IPs are real. Rate limiting is 10 requests per minute per IP on `/auth/login`, `/auth/register` and `/auth/refresh` (not `/auth/me`) |
| NFR-4 | Security | Input validated server-side. EF parameterized queries only. Security headers middleware (CSP, X-Content-Type-Options, Referrer-Policy, frame-ancestors) |
| NFR-5 | Concurrency | 20 parallel requests to book the same room and dates (different idempotency keys) produce exactly 1 × 201 and 19 × 409, and **no 500s** (proven by an integration test) |
| NFR-6 | Performance | Search and booking list p95 under 500 ms with the **load seed** (§5), measured and reported in the README. No N+1 queries (use `Select` projections). `AsNoTracking` for reads |
| NFR-7 | Reliability | Global exception middleware. No stack traces in production responses. Health check at `/health`. Correctness never depends on background jobs running on time (BOOK-3) |
| NFR-8 | Observability | Serilog structured logs with CorrelationId on every request and in audit entries. The SignalR `access_token` query parameter is redacted from request logs and from nginx access logs |
| NFR-9 | Maintainability | Clean Architecture boundaries, async all the way, nullable reference types on, no warnings in build, `.editorconfig` + Prettier/ESLint |
| NFR-10 | i18n | All UI text in translation files. No hard-coded strings in templates |
| NFR-11 | Accessibility | Labels on all form fields, keyboard-navigable booking flow, colour is never the only status signal (the calendar also shows a text label or icon) |
| NFR-12 | Time | The server uses an injectable `TimeProvider`, so expiry, cancellation-window, last-minute pricing and "today" logic are testable |
| NFR-13 | Privacy (GDPR) | Personal data is limited to booking needs. Erasing a user replaces their name, email and phone on User and Booking with a pseudonym and sets `PseudonymisedAt`. Audit entries reference `UserId` and a display name that is pseudonymised the same way. The README states the retention period (demo: data reset nightly). The footer links to a privacy page placeholder |

**Operations.**
- **OPS-1 (Must)** On the public demo, a nightly job (03:00 Europe/Berlin) resets demo data to the seed state. If the host was asleep at 03:00, the job runs on the first startup after that time.
- **OPS-2 (Must)** Production migrations are applied by the CD pipeline (an EF Core migration bundle) before the new app version starts, never on app startup in Production.

---

## 9. Testing, DevOps and deployment

Every push runs build and tests in GitHub Actions, the whole stack starts with `docker compose up`, and a public demo URL is live.

**Tests (minimum).**
- **Unit** (target 80%+ coverage of the Application layer):
  - Pricing engine: each rule type, stacking, rounding (total = sum of nights), the 1.00 floor, and boundaries such as a stay crossing a season edge and a weekend.
  - Booking status transitions, including NoShow and invalid transitions.
  - Cancellation fee calculation at exactly 48 h before the check-in instant, ±1 minute, across a DST change.
  - Overlap check, including an expired Pending booking.
  - Validators.
- **Integration** (WebApplicationFactory + Testcontainers PostgreSQL; in-memory or SQLite providers are not allowed):
  - Register/login and refresh rotation, including reuse detection.
  - Search excludes booked rooms but includes rooms held by an expired Pending booking.
  - Create booking.
  - Idempotent retry returns the same booking; a reused key with a different body returns 422.
  - Concurrent double-booking test (NFR-5).
  - Paying after expiry returns 409.
  - A guest cannot read another guest's booking (404).
  - Staff walk-in creates a Confirmed booking.
  - An audit entry is written on booking update, and Staff can read it via `/bookings/{id}/history`.
- **Angular:**
  - Auth interceptor, including the single-flight refresh with 3 concurrent 401s.
  - Role guard.
  - Booking stepper form validation.
  - Search filter ↔ URL sync.

**Docker.**
- `docker-compose.yml` has these services:
  - `db`: PostgreSQL 16 with a healthcheck (`pg_isready`).
  - `api`: multi-stage .NET build, `depends_on: db: condition: service_healthy`.
  - `web`: Angular build served by nginx, proxying `/api` and `/hubs` (with WebSocket upgrade) to the API.
  - `mail`: Mailpit.
- In Development, the API applies migrations and seeds on startup, retrying the database connection.
- `.env.example` documents every variable. `.env` is in `.gitignore`.
- All images must run on both x64 and ARM64 machines.

**CI** (GitHub Actions `ci.yml`). On push and pull request:
- Restore, then build with warnings as errors.
- Run unit and integration tests (Testcontainers needs Docker on the runner).
- Run Angular lint, tests and a production build.
- Upload a coverage report.

**Deployment.**
- **API:** a container host with a free or low-cost tier and WebSocket support (e.g. Render, Fly.io, Azure App Service). Check the current free-tier terms at deploy time.
- **Database:** managed PostgreSQL (e.g. Neon, Render, Supabase), with the `btree_gist` extension available.
- **Angular:** Azure Static Web Apps, Netlify or Vercel.
- **CD:** a job deploys `main` after CI passes. It runs the migration bundle first (OPS-2).
- **Cold starts:** document in the README that the free tier sleeps and the first request may be slow. For example, Render's free web services spin down after 15 minutes with no HTTP requests or WebSocket messages, and may restart at any time.

**README must include.**
- The project pitch in 2 lines and the live demo link.
- Demo accounts (guest, staff, admin) and a note that data resets nightly.
- A 20-second GIF of the live calendar updating.
- An architecture diagram and the tech stack with exact versions.
- How to run locally and how to run tests.
- Measured performance numbers (NFR-6).
- Key technical decisions: double-booking prevention (row lock + exclusion constraint), lazy expiry, the pricing engine and rounding, the audit interceptor, the refresh-token storage tradeoff (D3).
- A roadmap of next ideas.

---

## 10. Build plan

Build in 7 phases of about one week each, part-time. Each phase is split into sub-features, and each sub-feature ends with working, committed, tested code before the next starts.

1. **Foundation**
   - 1a: solution and Angular skeleton.
   - 1b: Docker Compose with healthchecks, EF Core DbContext, all entities, constraints and the first migration.
   - 1c: seed data (demo + load), Serilog, OpenAPI/Swagger, `/health`.
   - 1d: CI pipeline building both apps.
2. **Auth and inventory**
   - 2a: Identity + JWT + rotating refresh tokens, roles, demo accounts, lockout, rate limiting.
   - 2b: property, room and amenity CRUD API with soft delete and reactivation.
   - 2c: Angular login/register, interceptors (single-flight refresh), guards.
   - 2d: admin properties and rooms pages.
3. **Search and pricing**
   - 3a: pricing engine with all four rule types and unit tests.
   - 3b: quote and preview endpoints.
   - 3c: availability search with filters and paging.
   - 3d: home, search, property and room pages.
   - 3e: price rules admin page.
4. **Booking core**
   - 4a: Booking entity and status machine with unit tests.
   - 4b: create endpoint (row lock + exclusion constraint + idempotency), concurrency integration test.
   - 4c: mock payment, lazy expiry + expiry job, cancellation quote and fee.
   - 4d: walk-in bookings.
   - 4e: booking stepper, confirmation and My bookings pages.
5. **Back-office**
   - 5a: bookings table with filters and CSV export.
   - 5b: booking detail with actions and notes.
   - 5c: audit interceptor, booking history endpoint, audit log page.
6. **Real time and dashboard**
   - 6a: SignalR hub (groups, token redaction) and Angular hub service.
   - 6b: occupancy calendar grid.
   - 6c: dashboard aggregate endpoint, KPIs and charts.
   - 6d: live updates on both.
7. **Polish and ship**
   - 7a: German translations, responsive checks, accessibility pass.
   - 7b: security headers, GDPR pseudonymisation, privacy page.
   - 7c: load-seed performance measurement.
   - 7d: deploy with migration bundle and nightly reset job.
   - 7e: README with GIF and diagram, final test pass.

**Git habits.**
- One feature branch and pull request per sub-feature.
- Conventional Commits (`feat:`, `fix:`, `test:`).
- Requirement IDs in PR descriptions (for example "Implements BOOK-2, BOOK-8").

---

## 11. Acceptance criteria

The project is done when every box below can be ticked on the deployed demo.

- [ ] `docker compose up` on a clean machine (x64 or ARM64) starts all services, applies migrations and seeds data with no manual steps or retries.
- [ ] A visitor searches Berlin for 3 nights and sees only free rooms. The total equals the sum of the per-night breakdown to the cent, including weekend and seasonal rules, for a stay crossing a season edge.
- [ ] A guest books a room, pays (mock), receives a reference, sees it under My bookings and gets an email in Mailpit.
- [ ] The same room and dates cannot be booked again. The concurrency test proves 1 success and 19 conflicts, with no 500s.
- [ ] Double-submitting the booking form (same Idempotency-Key) creates one booking.
- [ ] An unpaid Pending booking stops blocking the room after 15 minutes **even with the expiry job disabled**, and is marked Cancelled once the job runs. Paying after expiry returns 409.
- [ ] Cancelling inside 48 hours of the check-in instant shows and stores the one-night fee. Staff can waive it, and the waiver appears in the audit log.
- [ ] Staff create a walk-in booking from an empty calendar cell. It is Confirmed immediately and appears on another browser's calendar within 2 seconds.
- [ ] A guest cannot see another guest's booking (404). Staff cannot open Admin-only pages.
- [ ] With two browsers open, a booking made in one appears on the other's calendar and dashboard within 2 seconds.
- [ ] Changing a booking's status creates an audit entry showing old and new values and the staff member's name. Staff see it on the booking detail page.
- [ ] Switching to German translates every UI label and formats prices as `1.234,56 €`.
- [ ] Demo accounts cannot be locked out, and demo data resets nightly.
- [ ] CI is green, unit and integration tests pass, and the build has zero warnings.
- [ ] The README has the live link, demo accounts, GIF, architecture diagram, measured performance and run instructions.

---

## 12. Build prompt for Claude

Commit this SRS to the repo as `docs/SRS.md`. Add a short `CLAUDE.md` that says "Follow docs/SRS.md; it is the source of truth." Then use the prompt below in Claude Code. It builds one **sub-feature** at a time, so you can review, run and understand each step. Send "continue with 2b" (etc.) to move on.

```text
You are a senior full-stack engineer and my mentor. Build the project
"StayDesk" exactly as specified in docs/SRS.md. I am a junior .NET / Angular
developer preparing for an Associate Software Engineer role, so code quality,
clarity and explainability matter as much as features.

TECH (do not substitute): ASP.NET Core 10 Web API with Clean Architecture
(Domain, Application, Infrastructure, Api), EF Core 10 + Npgsql + PostgreSQL,
ASP.NET Core Identity + JWT + rotating refresh tokens, FluentValidation,
SignalR, Serilog, OpenAPI + Swagger UI; latest stable Angular with standalone
components, Angular Material, RxJS, ngx-translate v18 (en, de), ng2-charts,
Vitest; xUnit + Shouldly + Testcontainers (PostgreSQL); EFCore.NamingConventions
(snake_case); Docker Compose; GitHub Actions. Before adding any package, confirm it is free for this use and
tell me its name and purpose.

HOW TO WORK:
1. Follow the Build plan in SRS section 10. Build ONE sub-feature per
   response, starting with 1a. Do not start the next sub-feature until I say
   "continue".
2. At the start of each sub-feature, list the requirement IDs (e.g. BOOK-2,
   PRICE-3) you will implement and the files you will create or change.
3. Write complete, runnable code: no placeholders, no "// TODO: implement",
   no omitted files.
4. Follow SRS section 5 for the data model and section 6 for endpoint paths,
   methods, status codes and error codes exactly.
5. Critical logic must match the SRS precisely:
   - Active booking = Confirmed or CheckedIn, or Pending with ExpiresAt > now.
     Overlap rule: existing.CheckIn < requested.CheckOut AND
     existing.CheckOut > requested.CheckIn.
   - Booking creation: transaction, SELECT ... FOR UPDATE on the room row,
     lazily cancel expired Pending bookings, re-check, insert; exclusion
     constraint as a safety net; 23P01 and conflicts map to 409, never 500;
     Idempotency-Key unique per (CreatedById, key).
   - Pricing: Strategy pattern, one class per rule type, signed percentages,
     multiplicative stacking, per-night rounding AwayFromZero, total = sum of
     rounded nights, 1.00 EUR floor, snapshot stored on the booking.
   - Status transitions enforced inside the Booking entity, with a
     concurrency token on Booking.
   - Audit log via an EF Core SaveChangesInterceptor, append-only,
     GUID v7 IDs.
   - Inject TimeProvider for anything time-based; the pricing engine takes
     "today" as input.
6. Write the tests listed in SRS section 9 for each sub-feature in the same
   sub-feature, including the 20-parallel-request concurrency test in 4b.
7. Never commit secrets: use environment variables, provide .env.example,
   and keep .env in .gitignore.
8. Use async/await everywhere, DTOs only at the API boundary, AsNoTracking
   projections for reads, nullable reference types on, zero build warnings.
9. Angular: OnPush, typed reactive forms, interceptors and guards in core/,
   every list has loading, empty and error states, every UI string in
   en.json and de.json.

AT THE END OF EACH SUB-FEATURE, give me:
- The commands to run it and to run the tests, and their actual results.
- What I should see in the browser or Swagger to verify it works.
- A short "What you should understand" section: explain the 2-3 most
  important design decisions in plain language, as interview talking points.
- A suggested commit message in Conventional Commits format.

AMBIGUITY: if something in the SRS is ambiguous and affects the data model,
the API contract or concurrency, stop and ask me. Otherwise choose the
simplest option that satisfies section 11, state the assumption in one line,
and continue.

Start now with 1a.
```

**Tips for using it.**
- Use Claude Code if you can: it creates the files and runs the commands directly, instead of you copying code from chat.
- After each sub-feature, run it yourself and read the code before saying "continue". In interviews you must explain every part, especially double-booking prevention, lazy expiry, the pricing engine and the audit interceptor.
- If a sub-feature fails to build, paste the exact error back and ask Claude to fix it before moving on.

---

## Appendix A — Change log (Revision 1 → 2)

| Area | Change |
|------|--------|
| Stack | SQL Server → PostgreSQL (D1); .NET 8 → .NET 10, Angular 17 → latest stable (D2); FluentAssertions → Shouldly; MailHog removed; Karma/Jest → Angular's default runner; Mapster removed |
| BOOK-2 | Room RowVersion replaced by a row lock + exclusion constraint; conflicts never return 500 |
| SRCH-3 / BOOK-3 | Expired Pending no longer blocks rooms; lazy expiry; pay-after-expiry 409; Booking concurrency token; NoShow status |
| PRICE | Rounding, signed percentages, inclusive dates, 1.00 floor, priority removed (D4), PRICE-7 price-change check, PRICE-8 preview |
| BOOK-5 | Check-in instant anchor, unpaid cancellation, fee waiver, non-cancellable statuses |
| BOOK-7 / BOOK-8 | Upgraded to Must; idempotency key scoped per user, with 200/422 semantics |
| BOOK-9, BO-4 | New: walk-in bookings; Staff-readable booking history |
| AUTH | Reuse detection, AUTH-6 deactivation, AUTH-7 profile, demo lockout exemption |
| Data model | Guest-detail fields, CreatedById, NoShow, check-in/out timestamps, Amenity join table, GUID v7 IDs, soft delete limited to 4 entities, PseudonymisedAt |
| API | Added `/users/me`, `/users/{id}/status`, `/amenities`, `/pricing/preview`, `/bookings/{id}/history`, `/bookings/{id}/cancellation-quote`, `/no-show`, reactivate endpoints; error codes; 422/429 |
| NFR | Rate limit scope + ForwardedHeaders, token redaction, load-seed performance, NFR-13 GDPR, OPS-1 nightly reset, OPS-2 migration bundle |
| Build plan / prompt | Sub-features instead of whole phases; SRS lives in the repo; ask on high-impact ambiguity |
