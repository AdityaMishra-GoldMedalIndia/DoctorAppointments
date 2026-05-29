# DoctorAppointments

Doctor Appointment Application built with **.NET 8 Web API + Dapper (no Entity Framework)** and a **React.js + Vite** frontend. Data access uses **SQL Server stored procedures** executed through a single generic data access helper.

## Implemented features

### Backend (.NET 8 + Dapper)
- JWT authentication
- Refresh token flow
- Password hashing using ASP.NET Core `PasswordHasher`
- Role-based authorization for **Admin**, **Doctor**, and **Patient**
- Role-wise dashboard API
- Rate limiting / throttling on API requests
- Restricted protected endpoints with JWT authorization
- CORS configuration for frontend access
- Pagination for patient and appointment listing APIs
- Swagger/OpenAPI documentation
- Seeded SQL Server database using Dapper stored procedures
- All data access goes through stored procedures via a generic `DataAccess` helper

### Frontend (React.js)
- API consumption with a shared Axios client
- Auto refresh-token handling on `401`
- Role-wise menu visibility and protected pages
- Role-wise dashboard UI
- Admin can see all patients
- Doctor can see only linked patients and doctor-wise appointments
- Patient can see only personal appointments
- Skeleton loading states for dashboard and tables

## Project structure

```text
backend/DoctorAppointments.Api
frontend/doctor-appointments-ui
```

## Demo credentials

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin@doctorapp.com` | `Admin@123` |
| Doctor | `sonia@doctorapp.com` | `Doctor@123` |
| Doctor | `amit@doctorapp.com` | `Doctor@123` |
| Patient | `rahul@doctorapp.com` | `Patient@123` |
| Patient | `pooja@doctorapp.com` | `Patient@123` |

## Run locally (step by step)

Follow these steps to get the application running on your local machine.

### Prerequisites

Make sure the following are installed before you start:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for the backend)
- [Node.js 18+](https://nodejs.org/) and npm (for the frontend)
- [SQL Server](https://www.microsoft.com/sql-server/) (Express, Developer, or LocalDB) reachable from your machine
- [Git](https://git-scm.com/) to clone the repository

### Step 1 — Clone the repository

```bash
git clone https://github.com/AdityaMishra-GoldMedalIndia/DoctorAppointments.git
cd DoctorAppointments
```

### Step 2 — Run the backend (.NET 8 Web API)

Open a terminal and run:

```bash
cd backend/DoctorAppointments.Api

# 1. Set a long random secret used to sign JWT tokens
export DOCTOR_APPOINTMENTS_JWT_SECRET='replace-with-a-long-random-secret-for-non-dev-use'

# 2. Point the connection string at your SQL Server instance
export ConnectionStrings__DefaultConnection='Server=localhost;Database=DoctorAppointments;Trusted_Connection=True;TrustServerCertificate=True;'

# 3. Restore dependencies and run the API
dotnet restore
dotnet run --urls http://127.0.0.1:5050
```

On Windows PowerShell, set the environment variables with `$env:` instead of `export`:

```powershell
$env:DOCTOR_APPOINTMENTS_JWT_SECRET='replace-with-a-long-random-secret-for-non-dev-use'
$env:ConnectionStrings__DefaultConnection='Server=localhost;Database=DoctorAppointments;Trusted_Connection=True;TrustServerCertificate=True;'
```

The SQL Server schema, stored procedures, and demo data are created automatically on the first run.

Once it is running, open the Swagger UI to explore the API:

- `http://127.0.0.1:5050/swagger`

### Step 3 — Run the frontend (React + Vite)

Open a **second** terminal (leave the backend running) and run:

```bash
cd frontend/doctor-appointments-ui

# 1. Install dependencies
npm install

# 2. Start the dev server pointing at the backend
VITE_API_BASE_URL=http://127.0.0.1:5050 npm run dev -- --host 127.0.0.1 --port 5173
```

On Windows PowerShell, set the variable separately before running the dev server:

```powershell
$env:VITE_API_BASE_URL='http://127.0.0.1:5050'
npm run dev -- --host 127.0.0.1 --port 5173
```

### Step 4 — Open the app

Open the frontend in your browser and log in with one of the [demo credentials](#demo-credentials) above:

- `http://127.0.0.1:5173`

## Important API endpoints

### Auth
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`

### User / Dashboard
- `GET /api/users/me`
- `GET /api/dashboard`

### Lists with pagination
- `GET /api/patients?pageNumber=1&pageSize=5`
- `GET /api/appointments?pageNumber=1&pageSize=5`
- `GET /api/doctors`

## Notes
- SQL Server schema and stored procedures are created automatically on first backend run (and seeded with demo data).
- Configure `ConnectionStrings:DefaultConnection` to point at your SQL Server instance.
- Default CORS origins are configured for `http://localhost:5173` and `http://127.0.0.1:5173`.
- Refresh tokens are stored as SHA-256 hashes in the database.
- Configure the signing key with `DOCTOR_APPOINTMENTS_JWT_SECRET` for predictable tokens across restarts and for non-development deployments.
- Code includes small Hindi + English comments in key implementation areas as requested.
