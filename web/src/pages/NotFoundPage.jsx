/*
 * File:    NotFoundPage.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: 404 page for any URL not in the route table.
 */
import { Link } from 'react-router-dom'
import Button from 'react-bootstrap/Button'

// Tells the user the page does not exist and links back to the start.
export default function NotFoundPage() {
  return (
    <div className="text-center py-5">
      <p className="display-4 fw-bold text-secondary mb-2">404</p>
      <h1 className="h4">Page not found</h1>
      <Button as={Link} to="/" className="mt-2">
        Back to SmartSolar
      </Button>
    </div>
  )
}
