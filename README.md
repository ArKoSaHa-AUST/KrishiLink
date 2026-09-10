# 🌾 KrishiLink — Smart Agriculture Platform

[![.NET Version](https://img.shields.io/badge/.NET-9.0%20%7C%208.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Framework](https://img.shields.io/badge/ASP.NET%20Core-MVC-blue?logo=aspnet)](https://dotnet.microsoft.com/apps/aspnet)
[![ORM](https://img.shields.io/badge/Entity%20Framework-Core%209.0-68217A?logo=nuget)](https://docs.microsoft.com/ef/)
[![Database](https://img.shields.io/badge/Database-MsSQL-CC292B?logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3-7952B3?logo=bootstrap)](https://getbootstrap.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**KrishiLink** is a 3-tier ASP.NET Core MVC platform designed to empower agricultural communities by seamlessly connecting **Farmers**, **Equipment Owners**, and **Godown Owners** in a unified ecosystem.

---

## 🌟 Key Features & Role Matrix

| Feature Module | 🌾 Farmers | 🚜 Equipment Owners | 🏭 Godown Owners |
| :--- | :---: | :---: | :---: |
| **Identity & Authentication** | Custom Profile & Role | Custom Profile & Role | Custom Profile & Role |
| **Equipment Marketplace** | Browse & Rent Machinery | List Equipment & Set Rates | View Catalog |
| **Godown Storage Directory** | Book Storage Space | View Storage Directory | List Godowns & Manage Space |
| **Booking Management** | Track Active & Past Requests | Accept/Reject Rental Requests | Accept/Reject Storage Requests |
| **Revenue & Earnings** | — | KPIs, Trend Chart, Settlement/Payouts, Utilization, Monthly PDF Statement, Invoices | KPIs, Trend Chart, Settlement/Payouts, Utilization, Monthly PDF Statement, Invoices |
| **Advisory Services** | Real-Time Weather & Crop Tips | — | — |

---

## 🏗️ Architecture & Project Structure

The project follows a clean **3-Tier Architecture** (Presentation / Business Logic / Data Access Layer) within a modular ASP.NET Core structure:

```
KrishiLink/
├── Controllers/                  # Presentation Layer Controllers
│   ├── HomeController.cs          # Public Landing & Overview
│   ├── AccountController.cs       # Auth, Registration, Login & User Profiles
│   ├── FarmerController.cs        # Farmer Hub & Action Center
│   ├── EquipmentController.cs     # Equipment Catalog & Detail Views
│   ├── GodownController.cs        # Storage Facilities Directory & Booking
│   ├── AdvisoryController.cs      # Weather Forecasts & Crop Recommendations
│   ├── BookingsController.cs      # User Booking History & Status Updates
│   ├── EquipmentOwnerController.cs# Owner Listings, Rental Request Management & Revenue Reports
│   ├── GodownOwnerController.cs   # Facility Listings, Storage Booking Requests & Revenue Reports
│   └── OwnerRevenueControllerBase.cs # Shared Revenue / Invoice / Expense actions for both owner roles
├── Views/                         # Razor Views & Component Partials
│   ├── Shared/                    # Base Layouts, Partials, shared Revenue & Invoice pages, SVG Revenue Chart
│   ├── Home/                      # Landing Page
│   ├── Account/                   # Login, Register, Profile UI
│   ├── Farmer/                    # Farmer Dashboard
│   ├── Equipment/                 # Equipment Catalog & Details UI
│   ├── Godown/                    # Storage Directory UI
│   ├── Advisory/                  # Advisory Dashboard
│   ├── Bookings/                  # History Views
│   ├── EquipmentOwner/            # Equipment Management Views
│   └── GodownOwner/               # Godown Management Views
├── Models/                        # Data Transfer & Entity Models
│   ├── Entities/                  # EF Core Domain Entities (User, Equipment, Godown, Bookings, Expenses, etc.)
│   └── ViewModels/                # Strongly-typed Razor ViewModels
├── BLL/                           # Business Logic Layer Services
│   └── Services/                  # EquipmentService, GodownService, BookingService, OwnerRevenueService, FileStorageService
├── DAL/                           # Data Access Layer
│   ├── ApplicationDbContext.cs    # EF Core DbContext with Identity Integration
│   ├── DbInitializer.cs           # Applies migrations, seeds roles (+ demo data in Development)
│   ├── Repositories/              # Generic EF Repository<T> + revenue reporting repositories
│   └── Migrations/                # EF Core Database Migrations
├── wwwroot/                       # Static Assets (Bootstrap, CSS, JS, Images, uploaded listing photos)
└── appsettings.json               # Database Connection, Revenue (Platform Commission) & Configuration Settings
```

---

## 💻 Tech Stack

- **Backend**: C# / ASP.NET Core MVC (.NET 9 / .NET 8 LTS)
- **Data Access & ORM**: Entity Framework Core, MsSQL (Microsoft SQL Server / LocalDB)
- **Security & Authentication**: ASP.NET Core Identity (Role-Based Authorization)
- **Reporting**: QuestPDF (Community licence) for server-generated monthly owner statements
- **Frontend**: Razor Views (HTML5), Bootstrap 5, Bootstrap Icons, Google Fonts (Inter)

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
   `appsettings.json` targets a local SQL Server Express instance by default. Adjust it for your server or LocalDB:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.\\SQLEXPRESS;Database=KrishiLinkDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
     }
   }
   ```

3. **Build and Run**:
   ```bash
   dotnet build
   dotnet run
   ```
   On startup the app applies any pending EF Core migrations and creates the three Identity roles automatically.
   (You can still apply migrations manually with `dotnet ef database update`.)

4. **Access the Application**:
   Open your browser and navigate to `http://localhost:5141` or `https://localhost:7276`.

### Demo Accounts (Development only)

When running with `ASPNETCORE_ENVIRONMENT=Development` against an empty database, a small demo dataset is seeded
(listings, bookings, payouts). Password for all demo accounts: `Krishi@123`

| Role | Email | Phone |
| :--- | :--- | :--- |
| 🌾 Farmer | `farmer@krishilink.com` | `01711000001` |
| 🚜 Equipment Owner | `equipment@krishilink.com` | `01712000001` |
| 🏭 Godown Owner | `godown@krishilink.com` | `01713000001` |

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
