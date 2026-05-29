# DoctorAppointments

Doctor Appointment Application built with **.NET 8 Web API + Dapper (no Entity Framework)** and a **React.js + Vite** frontend.

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
- Seeded SQLite demo database using Dapper

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

## Run backend

```bash
cd /tmp/workspace/AdityaMishra-GoldMedalIndia/DoctorAppointments/backend/DoctorAppointments.Api
export DOCTOR_APPOINTMENTS_JWT_SECRET='replace-with-a-long-random-secret-for-non-dev-use'
dotnet restore
dotnet run --urls http://127.0.0.1:5050
```

Swagger UI:
- `http://127.0.0.1:5050/swagger`

## Run frontend

```bash
cd /tmp/workspace/AdityaMishra-GoldMedalIndia/DoctorAppointments/frontend/doctor-appointments-ui
npm install
VITE_API_BASE_URL=http://127.0.0.1:5050 npm run dev -- --host 127.0.0.1 --port 5173
```

Frontend URL:
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
- SQLite database file is created automatically on first backend run.
- Default CORS origins are configured for `http://localhost:5173` and `http://127.0.0.1:5173`.
- Refresh tokens are stored as SHA-256 hashes in the database.
- Configure the signing key with `DOCTOR_APPOINTMENTS_JWT_SECRET` for predictable tokens across restarts and for non-development deployments.
- Code includes small Hindi + English comments in key implementation areas as requested.
