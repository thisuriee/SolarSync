/*
 * File:    useAuth.js
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Hook every screen uses to read the signed-in user and call login/logout.
 */
import { useContext } from 'react'
import { AuthContext } from './authContextInstance'

// Returns {user, isRestoring, login, logout}. Throws if used outside <AuthProvider>,
// so a wiring mistake shows up immediately instead of as a silent null user.
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used inside <AuthProvider>')
  }
  return ctx
}
