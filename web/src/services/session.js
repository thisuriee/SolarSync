/*
 * File:    session.js
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The only code that touches sessionStorage. Holds the JWT and the
 *          signed-in user returned by POST /auth/login (docs/auth.md — web token storage).
 */

const TOKEN_KEY = 'smartsolar.token'
const USER_KEY = 'smartsolar.user'
const EXPIRES_KEY = 'smartsolar.expiresAt'

// Stores the login response. sessionStorage (not localStorage) so the token dies
// with the tab — the trade-off against httpOnly cookies is recorded in docs/auth.md.
export function saveSession({ token, expiresAt, user }) {
  sessionStorage.setItem(TOKEN_KEY, token)
  sessionStorage.setItem(EXPIRES_KEY, expiresAt)
  sessionStorage.setItem(USER_KEY, JSON.stringify(user))
}

// Returns the bearer token, or null when nobody is signed in.
export function getToken() {
  return sessionStorage.getItem(TOKEN_KEY)
}

// Returns the cached {id, nic?, fullName, role} from login, or null.
// Display only — the API re-reads identity from the token on every call.
export function getStoredUser() {
  const raw = sessionStorage.getItem(USER_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw)
  } catch {
    return null
  }
}

// Replaces the cached user, e.g. after /auth/me or a profile edit, keeping the token.
export function updateStoredUser(user) {
  sessionStorage.setItem(USER_KEY, JSON.stringify(user))
}

// Removes everything. Called on logout and whenever the API answers 401.
export function clearSession() {
  sessionStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem(USER_KEY)
  sessionStorage.removeItem(EXPIRES_KEY)
}
