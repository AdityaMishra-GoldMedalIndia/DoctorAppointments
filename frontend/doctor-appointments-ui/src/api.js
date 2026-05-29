import axios from 'axios'

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5050'

const STORAGE_KEY = 'doctor-appointments-auth'

export const authStorage = {
  read() {
    const rawValue = localStorage.getItem(STORAGE_KEY)
    return rawValue ? JSON.parse(rawValue) : null
  },
  write(value) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
  },
  clear() {
    localStorage.removeItem(STORAGE_KEY)
  },
}

export function createApiClient({ onAuthChanged, onAuthFailure }) {
  const client = axios.create({
    baseURL: API_BASE_URL,
  })

  client.interceptors.request.use((config) => {
    const auth = authStorage.read()

    if (auth?.accessToken) {
      config.headers = config.headers ?? {}
      config.headers.Authorization = 'Bearer ' + auth.accessToken
    }

    return config
  })

  client.interceptors.response.use(
    (response) => response,
    async (error) => {
      const originalRequest = error.config ?? {}
      const isRefreshCall = originalRequest.url?.includes('/api/auth/refresh')

      if (
        error.response?.status === 401 &&
        !originalRequest._retry &&
        !isRefreshCall
      ) {
        const auth = authStorage.read()

        if (!auth?.refreshToken) {
          onAuthFailure()
          return Promise.reject(error)
        }

        originalRequest._retry = true

        try {
          const refreshResponse = await axios.post(
            `${API_BASE_URL}/api/auth/refresh`,
            {
              refreshToken: auth.refreshToken,
            },
          )

          onAuthChanged(refreshResponse.data)
          originalRequest.headers = originalRequest.headers ?? {}
          originalRequest.headers.Authorization = 'Bearer ' + refreshResponse.data.accessToken
          return client(originalRequest)
        } catch (refreshError) {
          onAuthFailure()
          return Promise.reject(refreshError)
        }
      }

      return Promise.reject(error)
    },
  )

  return client
}
