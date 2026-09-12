# 🌾 KrishiLink — Smart Agriculture Platform

[![.NET Version](https://img.shields.io/badge/.NET-9.0%20%7C%208.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Framework](https://img.shields.io/badge/ASP.NET%20Core-MVC-blue?logo=aspnet)](https://dotnet.microsoft.com/apps/aspnet)
[![ORM](https://img.shields.io/badge/Entity%20Framework-Core%209.0-68217A?logo=nuget)](https://docs.microsoft.com/ef/)
[![Database](https://img.shields.io/badge/Database-MsSQL-CC292B?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3-7952B3?logo=bootstrap)](https://getbootstrap.com/)
[![Localization](https://img.shields.io/badge/Localization-EN%20%7C%20BN%20(বাংলা)-28a745)](https://github.com/ArKoSaHa-AUST/KrishiLink)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**KrishiLink** is an enterprise-grade 3-tier ASP.NET Core MVC platform designed to empower agricultural communities in Bangladesh by seamlessly connecting **Farmers**, **Agricultural Equipment Owners**, and **Godown / Storage Facility Owners** into a transparent, secure, and highly efficient marketplace.

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
| **Weather & Pest Early Warning** | Live 7-Day Open-Meteo Forecast & Alerts | — | — | Regional Alert Triggers |
| **Multi-Channel Notifications** | In-App Real-Time & Emailed Updates | In-App Real-Time & Emailed Updates | In-App Real-Time & Emailed Updates | System Audit Notifications |

---

## 🚀 Key Modules & Architecture Enhancements

### 1. 🛡️ Identity Verification & Private PII Vault
- **Encrypted NID at Rest**: National ID numbers are encrypted at rest using ASP.NET Core `IDataProtectionProvider` (`KrishiLink.Nid`), preventing plain-text data exposure. Masked representations (`***-***-1234`) are served for profiles and administrative lists.
- **Private Physical Storage**: Sensitive identity documents (NID front/back, trade licenses) are stored outside web-accessible roots (`App_Data/verifications/{userId}/`) and streamed through authenticated, role-guarded endpoints (`/Account/VerificationDocument`) with `Cache-Control: private, no-store`.
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

**Ledger accounts**: `FarmerExternal`, `PlatformEscrow`, `PlatformCommission`, `OwnerExternal`. Conservation invariant, checked at startup and via `GET /Home/LedgerCheck` (Development only):

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
│   ├── EquipmentController.cs     # Machinery Catalog, Search, Filtering & Details
│   ├── GodownController.cs        # Storage Facilities Directory & Booking
│   ├── AdvisoryController.cs      # Weather Forecasts & Crop Calendars
│   ├── BookingsController.cs      # User Booking History & Status Updates
│   ├── ReviewsController.cs       # Verified Review Submission, AJAX Pagination & Owner Replies
│   ├── EquipmentOwnerController.cs# Owner Listings, Rental Requests, Maintenance & Revenue
│   ├── GodownOwnerController.cs   # Facility Listings, Space Requests & Revenue
│   └── OwnerRevenueControllerBase.cs # Shared Revenue / Invoice / Expense base controller
├── Views/                         # Razor Views & Component Partials
│   ├── Admin/Verifications/       # Admin Identity Verification Management
│   ├── Shared/                    # Layouts, Partials, Navigation, Modals, SVGs
│   ├── Home/                      # Landing Page
│   ├── Account/                   # Login, Register, Profile, Verification UI
│   ├── Farmer/                    # Farmer Dashboard & Advisory Strips
│   ├── Equipment/                 # Equipment Catalog & Details UI
│   ├── Godown/                    # Storage Directory UI
│   ├── Advisory/                  # Advisory Dashboard & Pest Warnings
│   ├── Bookings/                  # History & Review Modal Views
│   ├── EquipmentOwner/            # Equipment Management Views
│   └── GodownOwner/               # Godown Management Views
├── Models/                        # Data Transfer & Entity Models
│   ├── Entities/                  # EF Core Domain Entities (User, Equipment, Godown, Bookings, CropCalendar, WeatherData, etc.)
│   └── ViewModels/                # Strongly-typed Razor ViewModels
├── BLL/                           # Business Logic Layer Services
│   ├── Services/                  # Core Business Services:
│   │   ├── AppLinks.cs            # Centralized Type-Safe URL Registry
│   │   ├── BangladeshGeo.cs       # 64-District Geo-Hierarchy & GPS Coordinates
│   │   ├── BookingService.cs      # Booking Lifecycle & Validation
│   │   ├── CropCalendarService.cs # DAE Crop Recommendations & Caching
│   │   ├── EmailDispatchService.cs# Asynchronous Background Email Dispatcher
│   │   ├── EquipmentService.cs    # Equipment Management & Search
│   │   ├── GodownService.cs       # Godown Space Management & Search
│   │   ├── NotificationService.cs # Localized Multi-Channel Alerts
│   │   ├── OwnerRevenueService.cs # Revenue, Settlements & QuestPDF Reporting
│   │   ├── OwnerVerificationService.cs # Encrypted NID & Document Verification
│   │   ├── PestAlertService.cs    # Weather-Driven Outbreak Predictor
│   │   ├── ReviewService.cs       # Verified Reviews & Atomic Aggregations
│   │   └── WeatherService.cs      # Open-Meteo Live 7-Day Forecast Integrator
├── DAL/                           # Data Access Layer
│   ├── ApplicationDbContext.cs    # EF Core DbContext with Identity Integration
│   ├── DbInitializer.cs           # Database Migrations, Seeding & Geographic Data
│   ├── Repositories/              # Generic EF Repository<T> + Specialized Repositories
│   └── Migrations/                # EF Core Database Migrations
├── Resources/                     # Localization Resources (SharedResource.en.resx, SharedResource.bn.resx)
├── wwwroot/                       # Static Assets (Bootstrap 5, Vanilla JS, CSS, Icons)
└── appsettings.json               # Application Configuration Settings
```

---

## 💻 Tech Stack

- **Backend Framework**: C# / ASP.NET Core MVC (.NET 8 LTS / .NET 9)
- **Data Access & ORM**: Entity Framework Core 9.0, Microsoft SQL Server / LocalDB
- **Security & Cryptography**: ASP.NET Core Identity (RBAC), `IDataProtectionProvider` (NID Data at Rest)
- **PDF Generation**: QuestPDF (Community license) for monthly owner accounting statements
- **Frontend Architecture**: Razor Views (HTML5), Bootstrap 5.3, Bootstrap Icons, Vanilla JavaScript (no heavy runtime dependencies)
- **Localization**: Full Bilingual Support — English (`en-US`) and Bengali (`bn-BD` বাংলা)

---

## ⚙️ Configuration & Environment Settings

The application is configured through `appsettings.json` and environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=KrishiLinkDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "App": {
    "PublicBaseUrl": "https://localhost:7276"
  },
  "Revenue": {
    "PlatformCommissionRate": 0.05
  },
  "Payments": {
    "Provider": "Simulated",
    "SettlementDelay": "00:02:00",
    "SettlementPollSeconds": 15,
    "FailAccountSuffix": "0000"
  },
  "Upload": {
    "MaxImagesPerListing": 8,
    "MaxFileSizeMb": 5
  },
  "Email": {
    "Enabled": false,
    "Host": "smtp.example.com",
    "Port": 587,
    "Username": "notifications@krishilink.com",
    "FromAddress": "no-reply@krishilink.com"
  }
}
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 8.0 SDK or .NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Microsoft SQL Server](https://www.microsoft.com/sql-server) or SQL Server LocalDB
- [Git](https://git-scm.com/)

### Installation & Setup

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/ArKoSaHa-AUST/KrishiLink.git
   cd KrishiLink
   ```

2. **Configure Database Connection**:
   Update `appsettings.json` or `appsettings.Development.json` with your SQL Server connection string.

3. **Build the Application**:
   ```bash
   dotnet build -c Release
   ```

4. **Run the Application**:
   ```bash
   dotnet run
   ```
   On startup in `Development` mode, `DbInitializer` automatically applies pending EF Core migrations, seeds all application roles, inserts the 23 DAE crop calendars, and provisions demo seed data (including escrow payments, a failed payment attempt, and completed / processing / failed payouts with a balanced ledger). `Payments:SettlementDelay` defaults to 2 minutes in Development (30 minutes otherwise).

5. **Access the Portal**:
   Open your browser at `https://localhost:7276` or `http://localhost:5141`.

---

## 👥 Demo Accounts (Development Environment)

All demo accounts use the standard password: **`Krishi@123`**

| Role | Email | Phone | Capabilities |
| :--- | :--- | :--- | :--- |
| 🛡️ **Administrator** | `admin@krishilink.com` | `01710000000` | Review NID Verifications, System Audits |
| 🌾 **Farmer** | `farmer@krishilink.com` | `01711000001` | Browse Equipment & Godowns, Bookings, Advisory & Weather |
| 🚜 **Equipment Owner** | `equipment@krishilink.com` | `01712000001` | List Machinery, Accept/Reject Rentals, Revenue & Payouts |
| 🏭 **Godown Owner** | `godown@krishilink.com` | `01713000001` | List Warehouses, Capacity Management, Revenue & Invoices |

---

## 🌐 Localization

KrishiLink features first-class localization in **Bengali (বাংলা)** and **English**:
- Switch cultures at runtime via the navbar language selector.
- All dynamic alerts, notifications, validation errors, and view strings are localized in `Resources/SharedResource.bn.resx`.

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
