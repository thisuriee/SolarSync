/*
 * File:    AuthContext.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Holds the signed-in user for the whole web app. Exposes login/logout and
 *          restores the session on refresh via GET /auth/me (docs/PLUMBING-GUIDE.md §8 step 5).
 */
import { useCallback, useEffect, useMemo, useState } from 'react'
import apiClient from '../services/apiClient'
import { AuthContext } from './authContextInstance'
import {
  clearSession,
  getStoredUser,
  getToken,
  saveSession,
  updateStoredUser,
} from '../services/session'

// The context object lives in authContextInstance.js (fast-refresh rule).

// Keeps only the identity fields the UI needs, so the user object has the same shape
// whether it came from /auth/login or /auth/me. Display only — the API never trusts it.
function toSessionUser(u) {
  return { id: u.id, nic: u.nic ?? null, fullName: u.fullName, role: u.role }
}

// Provides {user, isRestoring, login, logout} to every component below it.
export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => getStoredUser())
  // Only restore when a token exists; with no token there is nothing to check.
  const [isRestoring, setIsRestoring] = useState(() => Boolean(getToken()))

  // On mount, asks the API who the token belongs to. This keeps a refresh from logging
  // the user out, and re-validates the token server-side instead of trusting storage.
  // A dead token gets a 401, which the apiClient interceptor turns into clear + /login.
  useEffect(() => {
    if (!getToken()) return undefined

    let cancelled = false
    apiClient
      .get('/auth/me')
      .then(({ data }) => {
        if (cancelled) return
        const restored = toSessionUser(data)
        updateStoredUser(restored)
        setUser(restored)
      })
      .catch((err) => {
        // 401 is already handled by the interceptor; drop the in-memory user too.
        // Any other failure (e.g. API briefly down) keeps the cached user so the
        // page can show its own error on the next call.
        if (!cancelled && err.status === 401) setUser(null)
      })
      .finally(() => {
        if (!cancelled) setIsRestoring(false)
      })

    return () => {
      cancelled = true
    }
  }, [])

  // Calls POST /auth/login and stores the token. Pending/Deactivated accounts are refused
  // by the API (AUTH_ACCOUNT_PENDING / AUTH_ACCOUNT_DEACTIVATED) — the rejection is passed
  // to the caller as {code, detail}, never decided here. Returns the user for role redirect.
  const login = useCallback(async (username, password) => {
    const { data } = await apiClient.post('/auth/login', { username, password })
    const signedIn = toSessionUser(data.user)
    saveSession({ token: data.token, expiresAt: data.expiresAt, user: signedIn })
    setUser(signedIn)
    return signedIn
  }, [])

  // Forgets the token locally. JWTs are stateless, so there is no server call —
  // the trade-off (no mid-session revocation, 8h expiry) is in docs/auth.md.
  const logout = useCallback(() => {
    clearSession()
    setUser(null)
  }, [])

  const value = useMemo(
    () => ({ user, isRestoring, login, logout }),
    [user, isRestoring, login, logout],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
