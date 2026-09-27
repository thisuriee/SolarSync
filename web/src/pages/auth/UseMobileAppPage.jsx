/*
 * File:    UseMobileAppPage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Landing page for a Prosumer who signs in on the web. Prosumer features
 *          (profile, reservations, QR) live in the Android app only.
 */
import { useNavigate } from 'react-router-dom'
import Button from 'react-bootstrap/Button'
import Card from 'react-bootstrap/Card'
import { useAuth } from '../../context/useAuth'

// Tells the prosumer to use the mobile app and lets them sign out.
export default function UseMobileAppPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  // Clears the session and returns to the login page.
  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="d-flex justify-content-center align-items-center vh-100 px-3">
      <Card className="shadow-sm text-center w-100" style={{ maxWidth: 440 }}>
        <Card.Body className="p-4">
          <h1 className="h4">Hi {user.fullName}</h1>
          <p className="text-secondary">
            Prosumer accounts are managed in the SmartSolar mobile app. Use it to view your
            profile, reserve battery slots and show your QR code at the node.
          </p>
          <Button variant="outline-secondary" onClick={handleLogout}>
            Log out
          </Button>
        </Card.Body>
      </Card>
    </div>
  )
}
