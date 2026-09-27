/*
 * File:    App.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The route table. Every staff page sits inside ProtectedRoute (role gate) and
 *          AppLayout (navbar). Keep each route's roles in step with layouts/navItems.js.
 *          Other members: replace your <ComingSoon/> with your page — same path, same roles.
 */
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import ComingSoon from './components/ComingSoon'
import HomeRedirect from './components/HomeRedirect'
import ProtectedRoute from './components/ProtectedRoute'
import AppLayout from './layouts/AppLayout'
import ForbiddenPage from './pages/auth/ForbiddenPage'
import LoginPage from './pages/auth/LoginPage'
import UseMobileAppPage from './pages/auth/UseMobileAppPage'
import BackofficeHomePage from './pages/home/BackofficeHomePage'
import OperatorHomePage from './pages/home/OperatorHomePage'
import NotFoundPage from './pages/NotFoundPage'
import { ROLES } from './utils/roles'

const { BACKOFFICE, GRID_OPERATOR, PROSUMER } = ROLES
const STAFF = [BACKOFFICE, GRID_OPERATOR]

// Declares every web route and which roles may open it. The role lists are UX only:
// each API route carries its own [Authorize(Roles = ...)], which is the real rule.
export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Public */}
        <Route path="/login" element={<LoginPage />} />

        {/* "/" → role home for now; M2's public landing page will take this path. */}
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <HomeRedirect />
            </ProtectedRoute>
          }
        />

        {/* Prosumers have no web screens — they are pointed to the Android app. */}
        <Route element={<ProtectedRoute roles={[PROSUMER]} />}>
          <Route path="/use-mobile" element={<UseMobileAppPage />} />
        </Route>

        {/* Signed-in pages inside the navbar layout */}
        <Route element={<ProtectedRoute />}>
          <Route element={<AppLayout />}>
            <Route path="/forbidden" element={<ForbiddenPage />} />

            {/* M1 — Backoffice only */}
            <Route element={<ProtectedRoute roles={[BACKOFFICE]} />}>
              <Route path="/backoffice" element={<BackofficeHomePage />} />
              <Route path="/webusers" element={<ComingSoon title="Web Users" owner="M1" />} />
              <Route
                path="/prosumers/pending"
                element={<ComingSoon title="Pending Activations" owner="M1" />}
              />
            </Route>

            {/* M1 — Grid Operator only */}
            <Route element={<ProtectedRoute roles={[GRID_OPERATOR]} />}>
              <Route path="/operator" element={<OperatorHomePage />} />
            </Route>

            {/* Backoffice + Grid Operator */}
            <Route element={<ProtectedRoute roles={STAFF} />}>
              {/* M1 */}
              <Route path="/prosumers" element={<ComingSoon title="Prosumers" owner="M1" />} />
              {/* M2 — proposed path */}
              <Route path="/nodes" element={<ComingSoon title="Nodes" owner="M2" />} />
              {/* M3 — proposed path */}
              <Route
                path="/reservations"
                element={<ComingSoon title="Reservations" owner="M3" />}
              />
              {/* M4 — proposed paths */}
              <Route path="/dashboard" element={<ComingSoon title="Dashboard" owner="M4" />} />
              <Route path="/history" element={<ComingSoon title="Booking History" owner="M4" />} />
              <Route
                path="/fulfilments"
                element={<ComingSoon title="Fulfilment Log" owner="M4" />}
              />
            </Route>
          </Route>
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </BrowserRouter>
  )
}
