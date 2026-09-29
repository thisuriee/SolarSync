/*
 * File:    AppLayout.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Shell around every signed-in staff page: navbar with role-filtered links,
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

// Renders the navbar and the current page. Shows only the links for the signed-in
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
    <>
      <Navbar bg="dark" data-bs-theme="dark" expand="lg" className="mb-4">
        <Container fluid>
          <Navbar.Brand as={Link} to={homePathForRole(user.role)}>
            SmartSolar
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="main-nav" />
          <Navbar.Collapse id="main-nav">
            <Nav className="me-auto">
              {items.map((item) => (
                // `end` so /prosumers is not highlighted while on /prosumers/pending.
                <Nav.Link key={item.to} as={NavLink} to={item.to} end>
                  {item.label}
                </Nav.Link>
              ))}
            </Nav>
            <Navbar.Text className="me-3">
              {user.fullName} <span className="text-secondary">· {user.role}</span>
            </Navbar.Text>
            <Button variant="outline-light" size="sm" onClick={handleLogout}>
              Log out
            </Button>
          </Navbar.Collapse>
        </Container>
      </Navbar>

      <Container fluid as="main" className="pb-4">
        <Outlet />
      </Container>
    </>
  )
}
