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
│   ├── EquipmentOwnerController.cs# Owner Listings & Rental Request Management
│   └── GodownOwnerController.cs   # Facility Listings & Storage Booking Requests
├── Views/                         # Razor Views & Component Partials
│   ├── Shared/                    # Base Layouts (_Layout.cshtml, _LoginPartial, Partials)
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
│   ├── Entities/                  # EF Core Domain Entities (User, Equipment, Godown, etc.)
│   └── ViewModels/                # Strongly-typed Razor ViewModels
├── BLL/                           # Business Logic Layer Services
│   └── Services/                  # Business Logic & Validation Services
├── DAL/                           # Data Access Layer
│   ├── ApplicationDbContext.cs    # EF Core DbContext with Identity Integration
│   └── Migrations/                # EF Core Database Migrations
├── wwwroot/                       # Static Assets (Bootstrap, CSS, JS, Images)
└── appsettings.json               # Database Connection & Configuration Settings
```

---

## 💻 Tech Stack

- **Backend**: C# / ASP.NET Core MVC (.NET 9 / .NET 8 LTS)
- **Data Access & ORM**: Entity Framework Core, MsSQL (Microsoft SQL Server / LocalDB)
- **Security & Authentication**: ASP.NET Core Identity (Role-Based Authorization)
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
   Update `appsettings.json` with your SQL Server connection string:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=KrishiLinkDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
     }
   }
   ```

3. **Apply EF Core Migrations**:
   ```bash
   dotnet ef database update
   ```

4. **Build and Run**:
   ```bash
   dotnet build
   dotnet run
   ```

5. **Access the Application**:
   Open your browser and navigate to `http://localhost:5141` or `https://localhost:7141`.

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
