/*
 * File:    authContextInstance.js
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The React context object itself. Kept apart from AuthContext.jsx so that file
 *          exports only a component (Vite fast refresh requirement).
 */
import { createContext } from 'react'

export const AuthContext = createContext(null)
