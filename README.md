# 🌾 KrishiLink — Smart Agriculture Platform

[![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Framework](https://img.shields.io/badge/ASP.NET%20Core-MVC-blue?logo=aspnet)](https://dotnet.microsoft.com/apps/aspnet)
[![ORM](https://img.shields.io/badge/Entity%20Framework-Core%208.0-68217A?logo=nuget)](https://docs.microsoft.com/ef/)
[![Database](https://img.shields.io/badge/Database-Supabase%20PostgreSQL-3ECF8E?logo=supabase)](https://supabase.com/)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3-7952B3?logo=bootstrap)](https://getbootstrap.com/)
[![Localization](https://img.shields.io/badge/Localization-EN%20%7C%20BN%20(বাংলা)-28a745)](https://github.com/ArKoSaHa-AUST/KrishiLink)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**KrishiLink** is a 3-tier ASP.NET Core MVC agriculture marketplace for Bangladesh, connecting **Farmers**, **Agricultural Equipment Owners**, and **Godown / Storage Facility Owners**. Supabase provides PostgreSQL, authentication, and object storage. Razor MVC remains the frontend; sensitive data access stays on the ASP.NET backend.

---

## 🌟 Key Features & Role Matrix

| Feature Module | 🌾 Farmers | 🚜 Equipment Owners | 🏭 Godown Owners | 🛡️ Administrators |
| :--- | :---: | :---: | :---: | :---: |
| **Authentication & Identity** | Custom Profile & Agro-Specialization | NID Verification & PII Vault | NID Verification & Trade License | Admin Portal & Audit Oversight |
| **Equipment Marketplace** | Filter by District, Price & Book | List Machinery, Calendar & Rates | View Directory | Manage Listings & Maintenance |
| **Godown Storage Directory** | Structured Capacity Search & Book | View Facilities | Manage Blocked Dates & Capacity | Oversee Storage Networks |
| **Booking & Settlement Lifecycle** | Active/Past Bookings & QR Verification | Accept/Reject Requests & QR Scan | Accept/Reject Requests & QR Scan | Dispute & Booking Audits |
| **Verified Ratings & Reviews** | Review Completed Bookings (1-5 ⭐) | Reply to Reviews & View Aggregates | Reply to Reviews & View Aggregates | Moderate Reviews |
| **Revenue & Financial Reports** | — | KPIs, Trends, PDF Statements | KPIs, Trends, PDF Statements | Platform Commission & Payouts |
| **Agronomic Crop Calendar** | DAE 23 Crop Advisories by District | — | — | Database Calendar Management |
| **Harvest Plan Cart** | Linked Multi-Item Plan, Seasonal Clone & Single-Click Submit | Context Badges on Requests | Context Badges on Requests | — |
| **Farmer Trust Profile** | Read-Only Profile & Verified Reputation | Completed, Cancellation % & Punctuality | Completed, Cancellation % & Punctuality | Full Audit & Profile Inspection |
| **Weather & Pest Early Warning** | Live 7-Day Open-Meteo Forecast & Alerts | — | — | Regional Alert Triggers |
| **Multi-Channel Notifications** | In-App Real-Time & Emailed Updates | In-App Real-Time & Emailed Updates | In-App Real-Time & Emailed Updates | System Audit Notifications |

---

## 🚀 Key Modules & Architecture Enhancements

### 1. 🛡️ Identity Verification & Private PII Vault
- **Encrypted NID at Rest**: National ID numbers are encrypted at rest using ASP.NET Core `IDataProtectionProvider` (`KrishiLink.Nid`), preventing plain-text data exposure. Masked representations (`***-***-1234`) are served for profiles and administrative lists.
- **Private Supabase Storage**: Sensitive identity documents (NID front/back, trade licenses) are stored in the private `verification-documents` bucket and streamed through authenticated, role-guarded endpoints (`/Account/VerificationDocument`) with `Cache-Control: private, no-store`. Listing images use the public `listing-images` bucket.
- **Admin Review Portal**: Dedicated administrator workflow (`/Admin/Verifications`) supporting document inspection, one-click approvals, and structured rejection reasons with automated owner notifications.
- **Single Pending Constraint**: Prevents duplicate concurrent verification requests while allowing seamless resubmission upon rejection.

### 2. ⭐ Hardened Ratings & Verified Reviews
- **Verified Booking Constraint**: Reviews are strictly restricted to farmers who have completed bookings.
- **Atomic Aggregation**: Listing ratings and owner-level aggregate ratings (`OwnerAverageRating`, `OwnerReviewCount`) update atomically within a single database transaction, resilient against race conditions (`DbErrors.IsUniqueViolation`).
- **Owner Response Workflow**: Equipment and godown owners can submit official public replies to customer reviews.
- **Undo Lock**: Bookings with submitted reviews cannot be reopened, preserving rating integrity.
- **Paginated Review Lists**: Initial server-rendered review list with asynchronous AJAX pagination (`GET /Reviews/List`).

### 3. 🗺️ Bangladesh Geo-Hierarchy & Enhanced Discovery
- **64-District Geographic Mapping**: Standardized mapping linking all 64 districts to 8 administrative divisions (`BangladeshGeo`), preventing mismatched or ambiguous text-based searches.
- **Multi-Parameter Discovery**: Exact district matching, minimum/maximum price boundaries, and SQL-projected capacity filtering for godown space.
- **Server-Side Pagination**: Responsive paginated grids (`PageSize = 24`) with location and price sorting.

### 4. 🌦️ Live Weather & Predictive Pest Outbreak Alerts
- **Open-Meteo Forecast Integration**: Fetches real-time 7-day daily forecasts (temperature, humidity, precipitation probability, weather codes) without external API keys.
- **64-District GPS Mapping**: Latitude and longitude coordinates mapped for every district in Bangladesh.
- **Forecast Caching & Resilient Fallback**: Database caching with thread-safe semaphore gates (`WeatherRefreshGate`) and seasonal agro-climatic fallback.
- **Automated Pest Outbreak Detection**: Rule-based engine analyzing consecutive days of high humidity (>80%) and precipitation to trigger early warning advisories for specific crops (e.g. Potato Late Blight, Rice Blast, Aphid infestation).

### 5. 📅 DAE Crop Calendar Database Integration
- **DAE 23-Crop Cultivation Database**: Fully seeded crop calendar table based on Department of Agricultural Extension (DAE) guidelines.
- **Monthly Agronomic Advisories**: Dynamic dashboard banner displaying personalized monthly activities (land prep, sowing, vegetative care, harvesting) based on the farmer's district, division, and primary crop.

### 6. 📬 Centralized Route Registry & Asynchronous Notifications
- **Canonical Routing Registry (`AppLinks`)**: Centralized type-safe route resolver eliminating dead links across all notification channels.
- **Notification Deduplication**: Database unique filtered index (`(UserId, DedupeKey) WHERE DedupeKey IS NOT NULL`) preventing duplicate alert spam.
- **Background Email Dispatch Queue**: Non-blocking channel queue (`EmailDispatchService : BackgroundService`) using `System.Threading.Channels.Channel<EmailJob>`.
- **Real-Email Hygiene**: Filters out simulated development emails (`@krishilink.local`) to ensure clean delivery in production.
- **Idempotent Reminder Scheduler (`ReminderScheduler`)**: Time-based background service sending proactive notifications (rental/storage starts tomorrow, equipment return due, storage ending soon, unpaid booking nudges, owner completion reminders, and stale pending request alerts). Fully idempotent via unique `(UserId, DedupeKey)` constraints with downtime-tolerant date windows; configurable in the `Reminders` appsettings section.

### 7. 💳 Money Flow (Simulated Escrow, Commission & Payouts)
Farmers pay into **platform escrow** through a simulated gateway; owners are paid out of escrow net of commission. Every movement is written to an append-only, double-entry ledger, and no step needs a human on the platform side.

**Booking state machine** (`BookingWorkflow.Next`, guards in `BookingWorkflow.Guard`):

```
Pending ──accept──▶ Accepted ──paid (IPaymentService only)──▶ Paid ──complete──▶ Completed
   ▲                   │ undo (only if not paid)                ▲ undo (only if PayoutId == null)
   └───────────────────┘                                        │
Pending ──reject──▶ Rejected ──undo──▶ Pending                  └── farmer can cancel Paid before start → refund
```

- Accepting snapshots `AgreedRate` / `AgreedGross` / `CommissionRate` on the booking (`BookingPricing` is the single pricing function); later rate edits never change history.
- Only a **Paid** booking can be completed; completing posts `CommissionEarned`, undo posts `CommissionReversed`.
- `RequestPayout` refuses any Completed booking without a succeeded payment; the payout starts `Processing` and the `PayoutSettlementScheduler` settles it automatically to `Completed` (ledger `PayoutOut`) or `Failed` (bookings return to the owed balance).

**Ledger accounts**: `FarmerExternal`, `PlatformEscrow`, `PlatformCommission`, `OwnerExternal`. Conservation invariant, available via `GET /Home/LedgerCheck` (Development only):

```
Σ PaymentIn − Σ Refund = EscrowBalance + Σ CommissionEarned − Σ CommissionReversed + Σ PayoutOut
```

**Sandbox failure hooks**: a farmer wallet ending in `0000` is declined by the gateway; a payout account ending in `0000` fails at settlement.

**Swapping in a real gateway**: implement `IPaymentGateway` (initiate → redirect URL, verify outcome, refund), register it for its name in `Program.cs`, and set `Payments:Provider`. `PaymentService`, the ledger and the UI stay unchanged.

- **Monthly PDF Statements**: Automated QuestPDF statements sent via email on month rollover.
- **Development Settlement Trigger**: Instant manual settlement trigger available in Development environments.

### 8. 🏷️ Dynamic & Seasonal Pricing + Minimum Rental Days (Equipment)
- **Rate Rule Hierarchy**: Supports custom `Season` (date-ranged) and `Weekend` (Friday & Saturday by default) rate rules. Season rules take precedence over Weekend rules, which take precedence over the base daily rate.
- **Strict Invariants**: Enforces at most one active Weekend rule per equipment and guarantees no overlapping active Season rules.
- **Minimum Rental Duration**: Configurable `MinRentalDays` (1–90 days) per machine, validated on the client and enforced server-side upon rental request submission.
- **Real-Time Live Quote API**: `GET /Equipment/Quote` provides debounced instant quoting with per-rule segment breakdown and pricing descriptions during checkout.
- **Snapshot Persistence**: `QuotedGross` and `PricingNote` are captured at request time; accepting re-evaluates active rules and records updated totals with audit notes.

### 9. 🚜 Multi-Unit Machinery Listings (Quantity & Available Capacity)
- **Fleet Inventory (`Quantity = 1..50`)**: Equipment owners can specify fleet inventory quantity per machine listing. Owners cannot reduce listed quantity below maximum concurrently booked units on any future date.
- **Concurrent Unit Booking (`Units = 1..Quantity`)**: Farmers can book multiple units in a single request. Gross rental fee and loyalty pricing automatically scale by `Units × DailyRate × Days`.
- **Per-Day Availability Calendar**: Partial bookings are tracked per calendar date (`bookedOnDay = Σ Units`). Calendar displays distinct `.cal-partial` states for partially booked dates and `"x/Quantity"` utilization badges for equipment owners. Blocked blackout dates take all units off the market.
- **Capacity-Aware Auto-Rejection**: When an owner accepts a rental request, overlapping pending requests are evaluated chronologically and automatically rejected only if remaining free capacity on any overlapping day cannot accommodate their requested units.
- **Backward Compatible**: Listings with `Quantity = 1` retain legacy single-unit behavior, visual styles, and exact conflict messaging.

### 10. 🗓️ Bulk Availability Tools & Date Range Blocking
- **Bulk Date Range Block/Unblock**: Equipment and godown owners can block or unblock full date ranges (up to 366 days) with optional recurring weekday filters (Saturday through Friday, respecting the Bangladesh work week) and quick presets (`Every Friday`, `Fri + Sat`, `All weekdays`, `Next 7 days`, `Next 30 days`, `Rest of this month`).
- **Farmer-Booked Date Protection**: Days already booked or stored by farmers are completely protected and automatically skipped during bulk blocking.
- **Audit & Reason Tracking**: Optional reasons (up to 100 characters, e.g. "Annual Maintenance", "Harvest Festival") are persisted on `EquipmentBlockedDate` and `GodownBlockedDate`, displayed as tooltips and badges on calendar days.
- **Diff-Based Calendar Persistence**: Single-month calendar updates use diffs (`Except` set operations) to preserve existing blocked date reasons when toggling availability.
- **Upcoming Blocked Periods (12-Month Horizon)**: Contiguous blocked dates sharing identical reasons are automatically grouped (`DateRanges.Group`) and displayed in an upcoming blocked periods widget with quick inline unblock modals.
- **Pending Conflict Badges**: Pending rental and storage booking requests that overlap any owner-blocked date display clear warning indicators with the conflicting date.

### 11. 🔄 Booking Modification Lifecycle (Dates, Units & Capacity)
- **Pre-Payment Flexibility**: Farmers can modify dates and requested quantity (equipment units / godown tonnage) on **Pending** and unpaid **Accepted** bookings prior to the booking start date.
- **Re-Approval Safety**: Modifying an **Accepted** booking returns it to **Pending**, resets owner rejection reasons, and clears the financial snapshot (`AgreedRate`, `AgreedGross`, `CommissionRate`) to prevent stale pricing or bypass of owner consent. The payment button is disabled until the owner re-approves.
- **Modification Guardrails**: Enforces a strict limit of 3 modifications per booking (`MaxModifications = 3`) and preserves audit history (`ModificationCount`, `ModifiedOn`, `PreviousDetails`). Paid bookings are locked in escrow and cannot be modified (farmers are prompted to cancel and refund instead).
- **Self-Excluding Conflict Checks**: Availability validation excludes the booking's own ID (`excludeBookingId`), allowing farmers to safely shrink, shift, or expand their reservation within their reserved window.
- **Automated Loyalty Re-pricing**: Prior loyalty points redemption and promo code discounts are safely refunded and re-calculated against the newly quoted gross amount.
- **Owner Visibility & Notifications**: Triggers `NotificationTypes.BookingModified` alerts to owners and displays `"Changed ×N"` indicator badges with previous booking summaries on all owner dashboards and request rows.

### 12. 🌾 Harvest Plan Cart (Multi-Item Seasonal Planning & Re-usable Templates)
- **Multi-Item Agricultural Cart**: Farmers can bundle multiple equipment rentals (combine harvester, tractor, tiller) and godown storage facilities into a single unified seasonal plan (up to 10 items per plan, up to 5 draft plans per farmer).
- **Live Availability & Quoting**: Real-time evaluation of live availability, conflicting dates, owner-blocked blackout periods, and dynamic daily / seasonal rate quotes on every plan item.
- **Fail-Fast Two-Pass Submission**: Batch submission performs a complete validation pass over all items before writing any bookings. If all items pass, bookings are created via canonical booking flows, atomically linking `HarvestPlanId`.
- **Race Condition Resiliency**: If any item fails during creation due to concurrent bookings, successfully created bookings remain intact, the plan remains in `Draft`, and the farmer is guided to adjust dates and resubmit (skipping already-booked items).
- **Owner Visibility & Request Linking**: Equipment and godown owner request dashboards display a distinct `bi-diagram-3` badge (`Harvest plan: {name} · {n} items`) with a comprehensive tooltip listing other items in the plan for context.
- **Seasonal Plan Cloning**: One-click duplication of previous harvest plans shifted by a customizable number of days (1–730 days, default 365 days) with all booking references cleared, streamlining recurring seasonal operations.
- **Automated Lifecycle Closure**: Harvest plans automatically transition from `Submitted` to `Completed` once all associated bookings reach terminal states (`Completed`, `Rejected`, or `Cancelled`).

### 13. 🛡️ Farmer Trust Profile for Owners
- **Trust Signals on Request Lists**: Equipment and godown owner request dashboards display inline mini-stats (`✔ {0} completed · {1}% cancellations · since {2}`) and a color-coded trust chip (`Reliable`, `New to KrishiLink`, or `Frequent cancellations`) for each requesting farmer.
- **Dedicated Read-Only Profile (`/FarmerProfile/{id}`)**: Owners can inspect a deep trust profile displaying total completed bookings, active rentals/storage, cancellation rate (excluding owner rejections and pending requests), average hours from acceptance to payment into platform escrow, reviews given, and repeat booking history with this specific owner.
- **Strict Privacy Scoping**: Access is strictly limited to Administrators and Owners who have received at least one booking request from that farmer. Unrelated owners or unknown IDs receive a generic `404 NotFound` response to prevent ID enumeration. Phone numbers are revealed only after a booking is confirmed; email addresses are never exposed.
- **High-Performance In-Memory Caching**: Summary lookups for request grids are cached per farmer ID for 5 minutes (`IMemoryCache`), preventing redundant database queries while keeping fresh trust metrics available.

### 14. ❤️ Favorites / Wishlist & Saved Searches with Availability Alerts
- **Farmer Wishlist (`/Favorites`)**: Farmers can bookmark machinery and storage facilities with responsive heart toggles across catalog cards and details pages. The wishlist provides real-time availability hints ("Available today", "Free from ...") and prunes stale or deleted listings automatically.
- **Saved Searches with Filters (`/SavedSearches`)**: Farmers can save multi-criteria browse queries (keyword, category/type, district, max price, capacity, and date windows) directly from browse filter bars with instant "Run now" links.
- **Automated Availability Alert Scheduler**: `SavedSearchAlertScheduler` background service continuously re-evaluates active saved searches against canonical `BrowseAsync` availability logic, sending deduplicated notifications and emails (`NotificationTypes.SavedSearchAlert`) when new matching listings appear.
- **Immediate New Listing Evaluation**: When equipment or godown owners publish new listings, matching saved searches are immediately evaluated to notify interested farmers without delay.
- **Lifecycle & Spam Guardrails**: Searches with past target dates are automatically retired with notification, while strict user scoping and deduplication keys prevent redundant alerts.

### 15. 📦 Godown Produce Intake Tracking & Official Warehouse Receipts (QuestPDF + QR Verification)
- **Produce Intake Lot Recording**: Godown owners can record physical batches/lots of produce (crop, variety, number of bags, bag weight in kg, auto-calculated net weight, moisture %, quality grade: Ungraded, Grade A, Grade B, Grade C, and handling/storage location remarks) against **Paid** or **Completed** storage bookings.
- **Over-Storage Guard**: Enforces that total stored net weight (`Σ Stored + New Lot`) cannot exceed booked storage capacity plus a 5% weighbridge tolerance (`StorageTons * 1000 * 1.05`), protecting against inadvertent warehouse overfill.
- **Sequential Year-Based Receipt Numbers**: Generates canonical, human-readable receipt identifiers in the format `KL-WR-{yyyy}-{Id:D5}` (e.g. `KL-WR-2026-00001`).
- **Official Warehouse Receipt PDF (`QuestPDF`)**: Generates an official, print-ready A4 PDF receipt using KrishiLink's green design palette, complete with depositor and warehouse facility credentials, commodity specifications, quality grades, storage terms, official non-negotiable legal disclaimer, and a scannable verification QR code.
- **Public QR Code Verification (`/Verify/Receipt/{receiptNumber}`)**: Anyone scanning the receipt's QR code is directed to a public verification page validating authenticity, current storage status (`STORED` vs `RELEASED`), and commodity specs while strictly protecting farmer privacy (zero farmer PII exposed).
- **Produce Release Workflow**: Godown owners can release stored lots to the farmer or an authorized representative (recording release timestamp, recipient name/ID, and gate pass remarks). Releasing permanently stamps the lot and its receipt as `RELEASED` and renders it immutable against further edits or deletions.
- **Comprehensive Lifecycle Notifications**: Farmers receive instant notifications when produce is accepted into the warehouse and when lots are released at pickup.

### 16. 🧾 Farmer Payment Receipts & Profit-and-Loss / Tax Summary Reporting
- **Farmer Payment Receipt (QuestPDF)**: Downloadable, print-ready A4 PDF payment receipt generated for all `Paid`, `Completed`, or `Refunded` equipment and godown bookings. Accessible via `GET /Bookings/Receipt?type={type}&id={id}` (authorized to the booking farmer or listing owner), with direct download buttons in *My Bookings*, *Booking Confirmation*, and the shared invoice view.
- **Automated Email Delivery & In-App Alerts**: On successful payment completion (`PaymentService.CompleteAsync`), the receipt PDF is automatically attached and emailed to the farmer via `IEmailQueue`, alongside an in-app `PaymentReceived` confirmation notification linking directly to the booking receipt.
- **Financial Invariants & Loyalty Clarity**: Strict separation between the legally agreed gross amount (`Payment.Amount == AgreedGross`) and platform loyalty point redemptions. Discounts are presented as an informational savings note without tampering with contractual gross amounts, escrow records, or refund amounts.
- **Tamper-Evident Verification**: Includes escrow trust notices, payment method references, platform contact details, and a scannable QR code resolving to the booking verification endpoint (`/Verify/{code}`). Refunded bookings feature a prominent `REFUNDED` watermark and refund ledger breakdown.
- **Categorized & General Operating Expenses**: `BookingExpense` extended with standard agricultural accounting categories (`Fuel`, `Labour`, `Repair`, `Transport`, `Fumigation`, `Utilities`, `Other`), optional listing associations, explicit expense dates, and support for general facility operating expenses (`BookingId == null`).
- **Owner Profit-and-Loss & Tax Summary (`/{Owner}/ProfitAndLoss`)**: Dedicated single-page A4 P&L report PDF designed for owners' tax advisers, presenting gross receipts, tax-deductible platform commission, categorical operating expense breakdowns, net operating profit, and operating margin.
- **Bangladesh Fiscal Year & Range Presets**: Range filters support Bangladesh fiscal year cycles (`1 Jul – 30 Jun`, preset `fy`), previous fiscal year (`lastfy`), year-to-date (`ytd`), rolling 12 months (`12m`), and current month (`month`), with automatic header dates and tax disclaimer notices.
- **Unified Statements & Interactive Dashboard**: Integrated P&L summary into the monthly PDF statement (`MonthlyStatementDocument`) with shared styling (`PdfStyle`), alongside interactive dashboard range bars, category chips, general expense tables, and modal inputs in `Views/Shared/Revenue.cshtml`.

---

## 🏗️ Architecture & Project Structure

The project follows a clean **3-Tier Architecture** (Presentation / Business Logic / Data Access Layer) within a modular ASP.NET Core structure:

```
KrishiLink/
├── Controllers/                  # Presentation Layer Controllers
│   ├── Admin/                    # Administrator Workflows (VerificationsController)
│   ├── HomeController.cs          # Public Landing & Overview
│   ├── AccountController.cs       # Auth, Registration, Login, Profile & Private Verification Docs
│   ├── FarmerController.cs        # Farmer Hub, Dashboard & Crop Recommendations
│   ├── FavoritesController.cs     # Wishlist Management & AJAX Heart Toggling
│   ├── SavedSearchesController.cs # Saved Search Queries, Alert Toggling & Dev Triggers
│   ├── EquipmentController.cs     # Machinery Catalog, Search, Filtering & Details
│   ├── GodownController.cs        # Storage Facilities Directory & Booking
│   ├── HarvestPlanController.cs   # Multi-Item Harvest Plan Cart, Submission & Seasonal Cloning
│   ├── FarmerProfileController.cs # Privacy-Scoped Read-Only Farmer Trust Profiles
│   ├── AdvisoryController.cs      # Weather Forecasts & Crop Calendars
│   ├── BookingsController.cs      # User Booking History & Status Updates
│   ├── VerifyController.cs        # Public QR Verification (Bookings & Warehouse Receipts)
│   ├── ReviewsController.cs       # Verified Review Submission, AJAX Pagination & Owner Replies
│   ├── EquipmentOwnerController.cs# Owner Listings, Rental Requests, Maintenance & Revenue
│   ├── GodownOwnerController.cs   # Facility Listings, Space Requests, Produce Intake & Revenue
│   └── OwnerRevenueControllerBase.cs # Shared Revenue / Invoice / Expense base controller
├── Views/                         # Razor Views & Component Partials
│   ├── Admin/Verifications/       # Admin Identity Verification Management
│   ├── Shared/                    # Layouts, Partials, Navigation, Modals, SVGs
│   ├── Home/                      # Landing Page
│   ├── Account/                   # Login, Register, Profile, Verification UI
│   ├── Farmer/                    # Farmer Dashboard & Advisory Strips
│   ├── Favorites/                 # Wishlist & Saved Items Management UI
│   ├── SavedSearches/             # Saved Searches & Alert Subscriptions UI
│   ├── Equipment/                 # Equipment Catalog & Details UI
│   ├── Godown/                    # Storage Directory UI
│   ├── HarvestPlan/               # Harvest Plan Management & Multi-Item Details UI
│   ├── FarmerProfile/             # Farmer Trust Profile Views
│   ├── Advisory/                  # Advisory Dashboard & Pest Warnings
│   ├── Bookings/                  # History & Review Modal Views
│   ├── Verify/                    # QR Verification Views (Booking & Warehouse Receipt)
│   ├── EquipmentOwner/            # Equipment Management Views
│   └── GodownOwner/               # Godown Management Views & Intake Modals
├── Models/                        # Data Transfer & Entity Models
│   ├── Entities/                  # EF Core Domain Entities (User, Equipment, Godown, Bookings, StorageIntakeLot, HarvestPlan, CropCalendar, WeatherData, etc.)
│   └── ViewModels/                # Strongly-typed Razor ViewModels (StorageIntakeViewModels, etc.)
├── BLL/                           # Business Logic Layer Services
│   ├── Services/                  # Core Business Services:
│   │   ├── AppLinks.cs            # Centralized Type-Safe URL Registry
│   │   ├── BangladeshGeo.cs       # 64-District Geo-Hierarchy & GPS Coordinates
│   │   ├── BookingService.cs      # Booking Lifecycle & Validation
│   │   ├── CropCalendarService.cs # DAE Crop Recommendations & Caching
│   │   ├── EmailDispatchService.cs# Asynchronous Background Email Dispatcher
│   │   ├── EquipmentService.cs    # Equipment Management & Search
│   │   ├── FarmerProfileService.cs# Farmer Trust Metrics & Privacy Verification
│   │   ├── FavoriteService.cs     # Wishlist Management & Availability Tracking
│   │   ├── GodownService.cs       # Godown Space Management & Search
│   │   ├── HarvestPlanService.cs  # Harvest Plan Cart, Multi-Item Submission & Validation
│   │   ├── NotificationService.cs # Localized Multi-Channel Alerts
│   │   ├── OwnerRevenueService.cs # Revenue, Settlements & QuestPDF Reporting
│   │   ├── OwnerVerificationService.cs # Encrypted NID & Document Verification
│   │   ├── PestAlertService.cs    # Weather-Driven Outbreak Predictor
│   │   ├── ReviewService.cs       # Verified Reviews & Atomic Aggregations
│   │   ├── SavedSearchService.cs  # Saved Queries & Immediate Match Evaluation
│   │   ├── SavedSearchAlertScheduler.cs # Background Periodic Search Match Dispatcher
│   │   ├── StorageIntakeService.cs# Produce Intake Tracking & Warehouse Receipts
│   │   ├── WarehouseReceiptDocument.cs # QuestPDF A4 Non-Negotiable Warehouse Receipt Document
│   │   └── WeatherService.cs      # Open-Meteo Live 7-Day Forecast Integrator
├── DAL/                           # Data Access Layer
│   ├── ApplicationDbContext.cs    # PostgreSQL DbContext, application profiles and roles
│   ├── ApplicationDbContextFactory.cs # EF tooling using the migration connection
│   ├── EnvironmentConfiguration.cs # Local dotenv and deployment environment loading
│   ├── DbInitializer.cs           # Roles and reference crop calendar seeding
│   ├── Repositories/              # Generic EF Repository<T> + Specialized Repositories
│   └── Migrations/                # Fresh PostgreSQL baseline and subsequent migrations
├── Resources/                     # Localization Resources (SharedResource.en.resx, SharedResource.bn.resx)
├── wwwroot/                       # Static Assets (Bootstrap 5, Vanilla JS, CSS, Icons)
└── appsettings.json               # Application Configuration Settings
```

---

## 💻 Tech Stack

- **Backend Framework**: C# / ASP.NET Core MVC targeting .NET 8 LTS (also builds using the .NET 9 SDK)
- **Data Access & ORM**: Entity Framework Core, Npgsql, Supabase PostgreSQL
- **Authentication**: Supabase Auth; ASP.NET Identity tables are retained for application profiles, roles and MVC cookie integration, not local password authentication
- **Object Storage**: Supabase Storage, with separate public listing and private verification buckets
- **Security & Cryptography**: ASP.NET Core Identity (RBAC), ASP.NET Data Protection for NID data and protected authentication state
- **PDF Generation**: QuestPDF (Community license) for monthly owner accounting statements & official non-negotiable warehouse receipts
- **QR Code Generation**: QRCoder for gate passes, booking verification & warehouse receipts
- **Frontend Architecture**: Razor Views (HTML5), Bootstrap 5.3, Bootstrap Icons, Vanilla JavaScript (no heavy runtime dependencies)
- **Localization**: Full Bilingual Support — English (`en-US`) and Bengali (`bn-BD` বাংলা)

---

## ⚙️ Configuration & Environment Settings

Configuration precedence, lowest to highest: ASP.NET JSON settings, root `.env`, root `.env.local`, process environment, command-line arguments. Local files are loaded explicitly by the app and EF design-time factory; ASP.NET does not load dotenv files by itself.

Copy [`.env.example`](.env.example) to `.env.local` and fill it locally. Both real environment files are Git-ignored and excluded from build/publish output. Use deployment secrets instead of shipping them.

| Setting | Purpose |
| --- | --- |
| `SUPABASE_URL` | Project HTTPS URL |
| `SUPABASE_PUBLISHABLE_KEY` | Auth API publishable key |
| `SUPABASE_SECRET_KEY` | Full server-only secret key; masked keys cannot work |
| `ConnectionStrings__SessionConnection` or `DIRECT_URL` | Preferred runtime session/direct connection on port 5432 |
| `DATABASE_URL` or `ConnectionStrings__DefaultConnection` | Runtime fallback when no session setting is supplied; must also use port 5432 |
| `DIRECT_URL` or `ConnectionStrings__MigrationConnection` | Session/direct connection for migrations; explicit migration setting takes priority |
| `SUPABASE_PUBLIC_BUCKET` / `SUPABASE_PRIVATE_BUCKET` | Defaults: `listing-images` / `verification-documents` |
| `App__PublicBaseUrl` | Actual externally reachable app origin, including HTTPS |
| `DataProtection__KeyPath` | Durable private key-ring directory; default `App_Data/keys` |
| `Database__ApplyMigrationsOnStartup` | Default `false`; opt in for local setup only |

`SUPABASE_*` aliases map to `Supabase:*`; ASP.NET `Supabase__Url`-style settings are also supported. The supplied `NEXT_PUBLIC_*` keys, `DB_*` components, project reference and JWKS URL are not needed by this server-rendered application. No database credentials or service-secret keys are emitted to JavaScript.

**Connections:** copy exact values from Dashboard **Connect**. This persistent ASP.NET application uses direct PostgreSQL or **session pooling (`5432`)** for runtime requests and migrations. Transaction-pooler (`6543`) settings are rejected: live validation against the supplied transaction endpoint stalled on repeated commands, while session pooling passed those checks. The supplied `DIRECT_URL` is used for runtime as well as migrations unless an explicit `ConnectionStrings__SessionConnection` is configured; legacy `6543` values elsewhere in the local files are therefore not selected. For a separate restricted runtime role, set `ConnectionStrings__SessionConnection` explicitly and keep privileged migration credentials separate.

Direct hosts commonly require IPv6; session pooling is the IPv4 alternative. Do not guess the pooler hostname from the region. TLS certificate/hostname validation is enforced (`VerifyFull`), prepared statements are disabled, and connection errors do not include server detail or parameters.

The public [Supabase CA certificate](supabase-ca.crt), downloaded from the Dashboard's **Database > Settings > SSL configuration**, is bundled for certificate verification. It is not a private key. Set `Database__RootCertificate` (or Npgsql `Root Certificate`) to a replacement trusted CA file when needed. Never resolve TLS errors by disabling certificate validation.

**Database isolation:** application tables and EF migration history live in the `krishilink` schema, not Supabase's exposed `public` schema. Do not add `krishilink` to exposed Data API schemas or grant `anon`/`authenticated` direct access. MVC controllers/services enforce application authorization; Supabase Auth UUIDs identify corresponding application profiles. Database migrations do not replace Supabase-owned `auth` or `storage` schemas.

**Dates:** calendar dates (booking ranges, blocked days, forecast days, maintenance days and statement months) use PostgreSQL `date`. Recorded instants use `timestamp with time zone` and UTC. Decimal financial columns retain their configured precision.

**Production storage:** back up PostgreSQL, Storage objects and the Data Protection key ring together. Persist the key directory across redeployments. Losing these keys prevents decryption of existing NID data and invalidates protected sessions. Use a private volume and appropriate OS access controls/encryption at rest.

If a database write/commit acknowledgement is lost after an upload, the application reports the error and conservatively retains the uploaded objects: deleting them could break a record that actually committed. Inspect the persisted listing/verification request before retrying or cleaning up unreferenced objects. This favors preserving documents over automatic deletion when the outcome is uncertain.

**Authentication sessions:** Supabase access/refresh tokens remain in server memory, never browser storage. This implementation targets a single app instance; restarting it logs users out. Do not deploy multiple replicas without implementing a shared protected token-session store. The backend checks Supabase's `auth.sessions` to reject revoked/expired sessions; a restricted runtime database role needs `USAGE` on `auth` and `SELECT (id, user_id, not_after)` on `auth.sessions`, in addition to access to application tables. These are read-only accesses to Supabase-managed tables, not application migrations.

Authentication cookies require HTTPS, including local testing. Use the `https` launch profile; an HTTP-only launch can render public pages but will not maintain authenticated sessions. Behind a reverse proxy, configure trusted forwarding/HTTPS termination for your hosting environment rather than relaxing cookie security.

Revenue, simulated payment, upload limit, localization and email settings remain in [appsettings.json](appsettings.json). Supabase Auth emails are configured in the Supabase Dashboard; application notification/statement emails use the separate `Email` SMTP section.

### Supabase email delivery (required before public registration)

The account screens use one-time email codes. In Dashboard **Authentication > Emails**:

1. Configure custom SMTP with a verified sender for production delivery. Supabase's built-in sender has restrictions and is not a general-purpose production mail service.
2. In **Confirm sign up**, **Reset password** and **Change email address** templates, include the literal `{{ .Token }}` so users can enter the code in KrishiLink. Do not send only a confirmation link to an app that expects an OTP.
3. Keep email confirmation enabled and test delivery to a non-team-member address.

Template editing may require custom SMTP or a paid Supabase plan. SMTP and email-template configuration remain external setup requirements; valid database/API keys alone do not make email verification or password recovery deliverable.

Email links are also supported through a server-side token-hash callback. Set Supabase's Site URL to the application's HTTPS origin and allow the exact `/Account/AuthCallback` redirect URL. A custom confirmation template can use:

```html
<a href="{{ .SiteURL }}/Account/AuthCallback?token_hash={{ .TokenHash }}&amp;type=signup">Confirm email</a>
<p>Or enter this code in KrishiLink: {{ .Token }}</p>
```

Use `type=recovery` for password resets and `type=email_change` for email changes. The callback stores the hash server-side and redirects to a clean confirmation page; a GET/email preview does not consume it. Do not use default implicit-fragment `ConfirmationURL` links. Redact callback query strings in hosting/reverse-proxy logs.

### Administrator provisioning

There are no default administrator passwords. Provision a real, email-confirmed Supabase Auth user (or confirm a normal signup), set the exact verified address in server-only `SUPABASE_ADMIN_EMAIL`, and sign in. The backend creates/promotes the matching passwordless application profile and assigns the Admin role. Client-submitted roles and Auth user metadata cannot grant it.

Remove the bootstrap setting afterward if it is no longer needed. Removing it does **not** revoke an already granted Admin role; manage existing role membership separately through trusted administration.

### Transaction consistency

Booking, payment, payout, loyalty and review mutations use a shared PostgreSQL transaction-scoped advisory lock. Nested service calls reuse their transaction, protecting multi-step writes and coordinating reviews with booking reopening. This deliberately serializes those mutations for correctness, so throughput is limited under heavy write load. Do not enable EF automatic retries without wrapping the entire transaction in an execution strategy; do not wrap these services in unrelated external transactions.

---

## 🚀 Getting Started

### Prerequisites

- [.NET 8.0 SDK or .NET 9.0 SDK](https://dotnet.microsoft.com/download)
- A [Supabase project](https://supabase.com/) with PostgreSQL, Email Auth and Storage enabled
- [Git](https://git-scm.com/)

### Installation & Setup

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/ArKoSaHa-AUST/KrishiLink.git
   cd KrishiLink
   ```

2. **Configure Supabase**:
   Fill `.env.local` using `.env.example`. Obtain the full keys and the session/direct connection strings from your project's Dashboard. Use a real email address for registration; phone numbers remain profile/contact identifiers.

3. **Restore and Build**:
   ```bash
   dotnet tool restore
   dotnet restore
   dotnet build -c Release
   ```

4. **Create the Fresh Database Schema**:
   ```bash
   dotnet ef database update
   ```
   This repository now has a PostgreSQL migration baseline. It is for a **fresh database**, not an in-place conversion of an existing database. Historical SQL Server migrations and development demo credentials/data have been removed. No business records or fake accounts are inserted.

   For deployments, generate and review a migration script with `dotnet ef migrations script --idempotent`, apply it through a session/direct connection before starting the new release, and leave startup migration disabled.

5. **Run the Application**:
   ```bash
   dotnet run --launch-profile https
   ```
   Startup seeds application roles and the 23 DAE reference crop calendars in every environment. It does not create demo users or payments. Register your own accounts using Supabase Auth. `Payments:SettlementDelay` still defaults to 2 minutes in Development and 30 minutes otherwise.

6. **Access the Portal**:
   Open your browser at `https://localhost:7276`. The HTTP endpoint is for redirection, not authenticated use.

---

## Deployment and Verification

- Configure Supabase Auth's Site URL, allowed redirect URLs, email templates and SMTP for your real application origin before production signups.
- Register separate farmer and owner accounts; confirm email, complete onboarding, create listings and verify images load from Supabase Storage.
- Test a booking through acceptance, simulated payment, completion, review and payout; check the database records and ledger.
- Verify anonymous users cannot access dashboards or private identity documents, and one owner cannot access another owner's documents.
- Keep the `verification-documents` bucket private; never grant blanket public policies to private objects.
- Payments remain **simulated**, not real bKash/Nagad/Rocket/card integrations. The deployment workflow prepares an artifact; it does not deploy to a configured hosting provider.
- Existing billing caveat: checkout uses `AgreedGross`, while confirmation/loyalty calculations also subtract `DiscountAmount`. Promo-discount charging and commission policy need a separate business-rule reconciliation before real payments; the migration does not redefine that policy.
- CI builds and checks the PostgreSQL migration model/script without contacting your Supabase project. There is no existing unit-test project; an empty `dotnet test` run is not evidence of end-to-end coverage.

Official references: [Supabase PostgreSQL connections](https://supabase.com/docs/guides/database/connecting-to-postgres), [Supabase Auth](https://supabase.com/docs/guides/auth), [Storage access control](https://supabase.com/docs/guides/storage/security/access-control), [Npgsql date/time mapping](https://www.npgsql.org/doc/types/datetime.html).

---

## 🌐 Localization

KrishiLink features first-class localization in **Bengali (বাংলা)** and **English**:
- Switch cultures at runtime via the navbar language selector.
- All dynamic alerts, notifications, validation errors, and view strings are localized in `Resources/SharedResource.bn.resx`.

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
