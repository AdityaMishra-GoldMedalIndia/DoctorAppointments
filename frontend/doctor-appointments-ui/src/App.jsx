import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  BrowserRouter,
  Navigate,
  NavLink,
  Route,
  Routes,
} from 'react-router-dom'
import './App.css'
import { authStorage, createApiClient } from './api'

const roles = {
  admin: 'Admin',
  doctor: 'Doctor',
  patient: 'Patient',
}

const pageSize = 5

export default function App() {
  const [auth, setAuthState] = useState(() => authStorage.read())

  const setAuth = useCallback((nextValue) => {
    setAuthState(nextValue)

    if (nextValue) {
      authStorage.write(nextValue)
      return
    }

    authStorage.clear()
  }, [])

  const api = useMemo(
    () =>
      createApiClient({
        onAuthChanged: setAuth,
        onAuthFailure: () => setAuth(null),
      }),
    [setAuth],
  )

  const handleLogout = useCallback(async () => {
    if (auth?.refreshToken) {
      try {
        await api.post('/api/auth/logout', { refreshToken: auth.refreshToken })
      } catch {
        // English + Hindi: logout fail ho tab bhi local session clear karna safe hai.
      }
    }

    setAuth(null)
  }, [api, auth?.refreshToken, setAuth])

  return (
    <BrowserRouter>
      <Routes>
        <Route
          path="/login"
          element={
            auth ? (
              <Navigate to="/dashboard" replace />
            ) : (
              <LoginPage api={api} onSuccess={setAuth} />
            )
          }
        />
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute auth={auth}>
              <AppLayout auth={auth} onLogout={handleLogout}>
                <DashboardPage api={api} auth={auth} />
              </AppLayout>
            </ProtectedRoute>
          }
        />
        <Route
          path="/patients"
          element={
            <ProtectedRoute auth={auth} allowedRoles={[roles.admin, roles.doctor]}>
              <AppLayout auth={auth} onLogout={handleLogout}>
                <PatientsPage api={api} auth={auth} />
              </AppLayout>
            </ProtectedRoute>
          }
        />
        <Route
          path="/appointments"
          element={
            <ProtectedRoute auth={auth}>
              <AppLayout auth={auth} onLogout={handleLogout}>
                <AppointmentsPage api={api} />
              </AppLayout>
            </ProtectedRoute>
          }
        />
        <Route
          path="*"
          element={<Navigate to={auth ? '/dashboard' : '/login'} replace />}
        />
      </Routes>
    </BrowserRouter>
  )
}

function ProtectedRoute({ auth, allowedRoles, children }) {
  if (!auth?.accessToken) {
    return <Navigate to="/login" replace />
  }

  if (allowedRoles && !allowedRoles.includes(auth.user.role)) {
    return <Navigate to="/dashboard" replace />
  }

  return children
}

function AppLayout({ auth, onLogout, children }) {
  const navItems = [
    { to: '/dashboard', label: 'Dashboard', visible: true },
    {
      to: '/patients',
      label: auth.user.role === roles.doctor ? 'My Patients' : 'Patients',
      visible: [roles.admin, roles.doctor].includes(auth.user.role),
    },
    { to: '/appointments', label: 'Appointments', visible: true },
  ].filter((item) => item.visible)

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div>
          <p className="eyebrow">Doctor Appointment App</p>
          <h1>Welcome, {auth.user.name}</h1>
          <p className="muted">Role: {auth.user.role}</p>
        </div>

        <nav className="menu">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                isActive ? 'menu-link menu-link-active' : 'menu-link'
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>

        <button className="secondary-button" onClick={onLogout}>
          Logout
        </button>
      </aside>

      <main className="page-content">{children}</main>
    </div>
  )
}

function LoginPage({ api, onSuccess }) {
  const [form, setForm] = useState({
    email: 'admin@doctorapp.com',
    password: 'Admin@123',
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const quickUsers = [
    { label: 'Admin', email: 'admin@doctorapp.com', password: 'Admin@123' },
    { label: 'Doctor', email: 'sonia@doctorapp.com', password: 'Doctor@123' },
    { label: 'Patient', email: 'rahul@doctorapp.com', password: 'Patient@123' },
  ]

  async function handleSubmit(event) {
    event.preventDefault()
    setLoading(true)
    setError('')

    try {
      const response = await api.post('/api/auth/login', form)
      onSuccess(response.data)
    } catch {
      setError('Login failed. Please verify your credentials.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-page">
      <form className="auth-card" onSubmit={handleSubmit}>
        <div>
          <p className="eyebrow">JWT + Refresh Token</p>
          <h1>Doctor Appointment Login</h1>
          <p className="muted">
            Sign in as admin, doctor, or patient to see role-wise dashboard and
            menu visibility.
          </p>
        </div>

        <label>
          Email
          <input
            type="email"
            value={form.email}
            onChange={(event) =>
              setForm((current) => ({ ...current, email: event.target.value }))
            }
            required
          />
        </label>

        <label>
          Password
          <input
            type="password"
            value={form.password}
            onChange={(event) =>
              setForm((current) => ({
                ...current,
                password: event.target.value,
              }))
            }
            required
          />
        </label>

        {error ? <p className="error-text">{error}</p> : null}

        <button disabled={loading} type="submit">
          {loading ? 'Signing in...' : 'Login'}
        </button>

        <div className="quick-login-grid">
          {quickUsers.map((user) => (
            <button
              key={user.label}
              className="quick-login-button"
              type="button"
              onClick={() =>
                setForm({ email: user.email, password: user.password })
              }
            >
              Use {user.label}
            </button>
          ))}
        </div>
      </form>
    </div>
  )
}

function DashboardPage({ api, auth }) {
  const [dashboard, setDashboard] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let cancelled = false

    async function loadDashboard() {
      setLoading(true)
      setError('')

      try {
        const response = await api.get('/api/dashboard')
        if (!cancelled) {
          setDashboard(response.data)
        }
      } catch {
        if (!cancelled) {
          setError('Unable to load dashboard right now.')
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    loadDashboard()

    return () => {
      cancelled = true
    }
  }, [api])

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="eyebrow">Role wise dashboard</p>
          <h2>{auth.user.role} Overview</h2>
        </div>
      </header>

      {loading ? <SkeletonCards count={3} /> : null}
      {error ? <p className="error-text">{error}</p> : null}

      {dashboard ? (
        <>
          <p className="muted page-copy">{dashboard.message}</p>
          <div className="stats-grid">
            {dashboard.cards.map((card) => (
              <article className="stat-card" key={card.label}>
                <span>{card.label}</span>
                <strong>{card.value}</strong>
              </article>
            ))}
          </div>
        </>
      ) : null}
    </section>
  )
}

function PatientsPage({ api, auth }) {
  const [result, setResult] = useState(null)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let cancelled = false

    async function loadPatients() {
      setLoading(true)
      setError('')

      try {
        const response = await api.get('/api/patients', {
          params: { pageNumber: page, pageSize },
        })

        if (!cancelled) {
          setResult(response.data)
        }
      } catch {
        if (!cancelled) {
          setError('Unable to load patients.')
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    loadPatients()

    return () => {
      cancelled = true
    }
  }, [api, page])

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="eyebrow">Patient list</p>
          <h2>{auth.user.role === roles.doctor ? 'My Patients' : 'All Patients'}</h2>
        </div>
      </header>

      {loading ? <SkeletonTable rows={5} /> : null}
      {error ? <p className="error-text">{error}</p> : null}

      {result ? (
        <>
          <div className="table-card">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Doctor(s)</th>
                  <th>Appointments</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((patient) => (
                  <tr key={patient.id}>
                    <td>{patient.name}</td>
                    <td>{patient.email}</td>
                    <td>{patient.doctorNames || '-'}</td>
                    <td>{patient.appointmentCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <PaginationControls page={page} result={result} onPageChange={setPage} />
        </>
      ) : null}
    </section>
  )
}

function AppointmentsPage({ api }) {
  const [result, setResult] = useState(null)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let cancelled = false

    async function loadAppointments() {
      setLoading(true)
      setError('')

      try {
        const response = await api.get('/api/appointments', {
          params: { pageNumber: page, pageSize },
        })

        if (!cancelled) {
          setResult(response.data)
        }
      } catch {
        if (!cancelled) {
          setError('Unable to load appointments.')
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    loadAppointments()

    return () => {
      cancelled = true
    }
  }, [api, page])

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="eyebrow">Appointments</p>
          <h2>Appointment List</h2>
        </div>
      </header>

      {loading ? <SkeletonTable rows={5} /> : null}
      {error ? <p className="error-text">{error}</p> : null}

      {result ? (
        <>
          <div className="table-card">
            <table>
              <thead>
                <tr>
                  <th>Doctor</th>
                  <th>Patient</th>
                  <th>Date</th>
                  <th>Status</th>
                  <th>Notes</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((appointment) => (
                  <tr key={appointment.id}>
                    <td>{appointment.doctorName}</td>
                    <td>{appointment.patientName}</td>
                    <td>{appointment.appointmentDate}</td>
                    <td>{appointment.status}</td>
                    <td>{appointment.notes || '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <PaginationControls page={page} result={result} onPageChange={setPage} />
        </>
      ) : null}
    </section>
  )
}

function PaginationControls({ page, result, onPageChange }) {
  const hasNextPage = page < result.totalPages

  return (
    <div className="pagination-bar">
      <p className="muted">
        Page {result.pageNumber} of {Math.max(result.totalPages, 1)} • Total{' '}
        {result.totalCount}
      </p>
      <div className="pagination-actions">
        <button disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
          Previous
        </button>
        <button disabled={!hasNextPage} onClick={() => onPageChange(page + 1)}>
          Next
        </button>
      </div>
    </div>
  )
}

function SkeletonCards({ count }) {
  return (
    <div className="stats-grid">
      {Array.from({ length: count }).map((_, index) => (
        <div className="skeleton skeleton-card" key={index} />
      ))}
    </div>
  )
}

function SkeletonTable({ rows }) {
  return (
    <div className="table-card">
      {Array.from({ length: rows }).map((_, index) => (
        <div className="skeleton skeleton-row" key={index} />
      ))}
    </div>
  )
}
