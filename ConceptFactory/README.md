# ConceptFactory – Product & Service Management

ASP.NET Core MVC (C#) application for managing products and printing services.

---

## ✅ Prerequisites

- Visual Studio 2022 (v17+)
- .NET 8 SDK
- SQL Server / SQL Server Express (or LocalDB)
- SQL Server Management Studio (SSMS)

---

## 🗄️ Step 1 – Create the Database

1. Open **SQL Server Management Studio (SSMS)**
2. Connect to your SQL Server instance
3. Open the file: `Database/ConceptFactoryDB.sql`
4. Press **F5** to execute — this creates the `ConceptFactoryDB` database with all tables and sample data

---

## ⚙️ Step 2 – Configure Connection String

Open `appsettings.json` and update the connection string to match your SQL Server instance:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=ConceptFactoryDB;Trusted_Connection=true;TrustServerCertificate=True"
}
```

**Common server names:**
| Setup | Server value |
|---|---|
| LocalDB | `(localdb)\\mssqllocaldb` |
| SQL Server Express | `.\\SQLEXPRESS` |
| Full SQL Server | `.` or `localhost` |

---

## ▶️ Step 3 – Run the Application

1. Open `ConceptFactory.sln` in Visual Studio 2022
2. Right-click the project → **Set as Startup Project**
3. Press **F5** or click **Run**
4. The app opens at `https://localhost:{port}/Products`

---

## 📁 Project Structure

```
ConceptFactory/
├── Controllers/
│   ├── HomeController.cs
│   ├── ProductsController.cs      ← Product CRUD + image upload
│   └── ServicesController.cs      ← Printing service CRUD
│
├── Models/
│   ├── Category.cs
│   ├── Product.cs
│   └── Service.cs
│
├── Data/
│   └── ApplicationDbContext.cs    ← EF Core DbContext
│
├── Views/
│   ├── Products/
│   │   ├── Index.cshtml           ← Product list with filter/search
│   │   ├── Create.cshtml          ← Add product form
│   │   └── Edit.cshtml            ← Edit product form
│   ├── Services/
│   │   ├── Index.cshtml           ← Service list
│   │   ├── Create.cshtml          ← Add service form
│   │   └── Edit.cshtml            ← Edit service form
│   └── Shared/
│       ├── _Layout.cshtml         ← Admin sidebar layout
│       └── _ValidationScriptsPartial.cshtml
│
├── wwwroot/
│   ├── css/site.css               ← All UI styles
│   ├── js/site.js
│   └── images/products/           ← Uploaded product images (auto-created)
│
├── Database/
│   └── ConceptFactoryDB.sql       ← Full DB schema + seed data
│
├── appsettings.json
├── Program.cs
└── ConceptFactory.csproj
```

---

## 🗃️ Database Tables (ERD)

| Table | Purpose |
|---|---|
| `Categories` | Product categories (T-Shirts, Hoodies, etc.) |
| `Products` | Product catalogue with pricing & stock |
| `Services` | Printing services (DTG, Screen Print, etc.) |

---

## ✨ Features

### Products
- List all products with search, category filter, and status filter
- Paginated grid (12 per page)
- Add product with image upload
- Edit product details and replace image
- Delete product (with confirmation modal)
- Active / Inactive status badges

### Printing Services
- List all services with search and status filter
- Add / Edit / Delete services
- Pricing management

---

## 🎨 UI Notes

The UI matches the admin dashboard design from the screenshots:
- Fixed left sidebar with navigation
- Purple (`#5B5EF4`) primary colour
- Clean white cards with subtle borders
- Active/Inactive status pills (green/red)
- Edit (pencil) and Delete (trash) icon buttons
