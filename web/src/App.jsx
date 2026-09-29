/*
 * File:    App.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The route table. Every staff page sits inside ProtectedRoute (role gate) and
 *          AppLayout (navbar). Keep each route's roles in step with layouts/navItems.js.
 *          Other members: replace your <ComingSoon/> with your page — same path, same roles.
 */
import { BrowserRouter, Route, Routes } from "react-router-dom";
import ComingSoon from "./components/ComingSoon";
import ProtectedRoute from "./components/ProtectedRoute";
import AppLayout from "./layouts/AppLayout";
import HomePage from "./pages/HomePage";
import ForbiddenPage from "./pages/auth/ForbiddenPage";
import LoginPage from "./pages/auth/LoginPage";
import UseMobileAppPage from "./pages/auth/UseMobileAppPage";
import DashboardPage from "./pages/dashboard/DashboardPage";
import BookingHistoryPage from "./pages/history/BookingHistoryPage";
import BackofficeHomePage from "./pages/home/BackofficeHomePage";
import OperatorHomePage from "./pages/home/OperatorHomePage";
import PendingActivationsPage from "./pages/users/PendingActivationsPage";
import ProsumersPage from "./pages/users/ProsumersPage";
import WebUsersPage from "./pages/users/WebUsersPage";
import NodesPage from "./pages/nodes/NodesPage";
import ReservationsPage from "./pages/reservations/ReservationsPage";
import NotFoundPage from "./pages/NotFoundPage";
import { ROLES } from "./utils/roles";

const { BACKOFFICE, GRID_OPERATOR, PROSUMER } = ROLES;
const STAFF = [BACKOFFICE, GRID_OPERATOR];

// Declares every web route and which roles may open it. The role lists are UX only:
// each API route carries its own [Authorize(Roles = ...)], which is the real rule.
export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Public — no token required. The landing page is what a visitor sees first,
            and it offers a signed-in visitor a link onward to their own home. */}
        <Route path="/" element={<HomePage />} />
        <Route path="/login" element={<LoginPage />} />

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
              <Route path="/webusers" element={<WebUsersPage />} />
              <Route
                path="/prosumers/pending"
                element={<PendingActivationsPage />}
              />
            </Route>

            {/* M1 — Grid Operator only */}
            <Route element={<ProtectedRoute roles={[GRID_OPERATOR]} />}>
              <Route path="/operator" element={<OperatorHomePage />} />
            </Route>

            {/* Backoffice + Grid Operator */}
            <Route element={<ProtectedRoute roles={STAFF} />}>
              {/* M1 */}
              <Route path="/prosumers" element={<ProsumersPage />} />
              {/* Microgrid nodes and their booking windows */}
              <Route path="/nodes" element={<NodesPage />} />
              {/* M3 — reservation management */}
              <Route path="/reservations" element={<ReservationsPage />} />
              {/* M4 — dashboards and booking history */}
              <Route path="/dashboard" element={<DashboardPage />} />
              <Route path="/history" element={<BookingHistoryPage />} />
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
  );
}
