/*
 * File:    apiClient.js
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The single axios instance every member uses to call the API. Attaches the
 *          bearer token and turns every failure into {status, code, detail, errors}
 *          (docs/PLUMBING-GUIDE.md §8, docs/response-format.md — client handling).
 */
import axios from 'axios'
import { clearSession, getToken } from './session'

const baseURL = import.meta.env.VITE_API_BASE_URL

if (!baseURL) {
  // Fail loudly in the console rather than silently calling the Vite dev server.
  console.error('VITE_API_BASE_URL is not set — copy web/.env.example to web/.env.local')
}

const apiClient = axios.create({
  baseURL,
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
})

// Request interceptor: attaches "Authorization: Bearer <token>" to every call, so no
// screen handles the token itself. Identity travels only in the token, never in a body.
apiClient.interceptors.request.use((config) => {
  const token = getToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Response interceptor: success passes through untouched (the API returns raw payloads).
// Failure is normalised by toApiError; a 401 on a call that carried a token means the
// session is dead (expired/invalid token), so it is cleared and the user sent to /login.
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const apiError = toApiError(error)

    // A 401 without a token is a failed login (AUTH_INVALID_CREDENTIALS) — the login
    // screen must show its detail, so do not redirect in that case.
    const sentToken = Boolean(error.config?.headers?.Authorization)
    if (apiError.status === 401 && sentToken) {
      clearSession()
      if (window.location.pathname !== '/login') {
        // Full reload on purpose: it also resets AuthContext and any cached screen state.
        window.location.replace('/login')
      }
    }

    return Promise.reject(apiError)
  },
)

// Converts an axios error into the one shape screens consume. The API's problem object
// supplies code + detail; screens display detail and switch on code, never on text.
function toApiError(error) {
  if (!error.response) {
    // No HTTP response at all: API down, CORS rejection, timeout. Not a business rule,
    // so this is the one message the client is allowed to write itself.
    return {
      status: 0,
      code: 'NETWORK_ERROR',
      detail: 'Cannot reach the SmartSolar server. Check your connection and try again.',
      errors: null,
    }
  }

  const { status, data } = error.response
  const problem = data && typeof data === 'object' ? data : {}

  return {
    status,
    // null for the automatic [ApiController] 400, which has no code (known gap — M2's Program.cs).
    code: problem.code ?? null,
    detail: problem.detail ?? problem.title ?? `Request failed (HTTP ${status}).`,
    // Field-level validation map, present only on 400.
    errors: problem.errors ?? null,
  }
}

export default apiClient
