/*
 * File:    AppLayout.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Shell around every signed-in staff page: sidebar with role-filtered links,
 *          the signed-in user, logout, and the page content (<Outlet/>).
 */
import { useMemo } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import Button from 'react-bootstrap/Button'
import Container from 'react-bootstrap/Container'
import Nav from 'react-bootstrap/Nav'
import Navbar from 'react-bootstrap/Navbar'
import { useAuth } from '../context/useAuth'
import { homePathForRole } from '../utils/roles'
import { navItemsForRole } from './navItems'

// Renders the sidebar and the current page. Shows only the links for the signed-in
// role, so a Grid Operator never sees Backoffice-only links — the API still enforces it.
export default function AppLayout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const items = useMemo(() => navItemsForRole(user.role), [user.role])

  // Clears the session and goes back to the login page.
  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    // One Navbar serves both layouts: index.css turns it into a left sidebar from lg up,
    // and below that it stays a collapsing top bar with the usual toggle.
    <div className="app-shell">
      <Navbar expand="lg" className="app-sidebar">
        <Navbar.Brand as={Link} to={homePathForRole(user.role)} className="app-brand">
          <span className="brand-mark" aria-hidden="true" />
          SmartSolar
        </Navbar.Brand>
        <Navbar.Toggle aria-controls="main-nav" />
        <Navbar.Collapse id="main-nav">
          <Nav className="app-nav">
            {items.map((item) => (
              // `end` so /prosumers is not highlighted while on /prosumers/pending.
              <Nav.Link key={item.to} as={NavLink} to={item.to} end>
                {item.label}
              </Nav.Link>
            ))}
          </Nav>
          <div className="app-user">
            <div className="fw-semibold text-truncate">{user.fullName}</div>
            <div className="text-secondary small mb-2">{user.role}</div>
            <Button variant="outline-secondary" size="sm" className="w-100" onClick={handleLogout}>
              Log out
            </Button>
          </div>
        </Navbar.Collapse>
      </Navbar>

      <Container fluid as="main" className="app-main">
        <Outlet />
      </Container>
    </div>
  )
}
