# House of Runs

Faculty number: 2401321072
Name: Vasil Kirov

House of Runs is a secured Hades run platform. Users can register, log in, create runs manually, import runs from the ExportRunHistory JSON file, review generated drafts, and save their own run history. Admins can manage platform lookup data and accounts.

## Tech Stack

- Backend: ASP.NET Core Web API, Entity Framework Core
- Database: PostgreSQL
- Frontend: Separate ASP.NET Core MVC app with Razor views
- Security: Bearer token authentication in the API, cookie/session authentication in the MVC frontend

## Database

Create a PostgreSQL database:

```sql
CREATE USER app WITH PASSWORD 'a';
CREATE DATABASE house_of_runs OWNER app;
GRANT ALL PRIVILEGES ON DATABASE house_of_runs TO app;
```

The backend connection string is in `backend/appsettings.json`:

```txt
Host=localhost;Port=5432;Database=house_of_runs;Username=app;Password=a
```

## Run Backend

```bash
cd backend
dotnet restore
dotnet run --urls http://localhost:5119
```

The API runs at `http://localhost:5119`.

## Run Frontend

```bash
cd frontend
dotnet restore
dotnet run --urls http://localhost:5120
```

Open `http://localhost:5120`.

Demo account after backend seed:

```txt
username: demo
password: demo1234
```

Admin account after backend seed:

```txt
username: admin
password: admin1234
```

## Features

- Full CRUD for users, weapons, boons, runs, and run-boons
- Role-based access: users manage only their own runs; admins manage accounts, weapons, boons, and all runs
- Protected API endpoints and protected frontend views
- Pagination, sorting, and filtering on list endpoints
- Global API exception handling with Problem Details responses
- Async database access
- JSON import flow for all Hades ExportRunHistory runs in a file
- No-SPA server-rendered MVC frontend

## Import Notes

The import page accepts `output.json` from the ExportRunHistory mod. Leave the run index blank to import every run in the file as expandable drafts, or enter a specific `index` value from the JSON to import only that run.

During import, the backend parses the run result, weapon, aspect, heat, duration, clear message, and traits. Missing weapon/aspect and boon/trait records are created automatically so each generated draft can be edited, saved, or deleted from the review list.
