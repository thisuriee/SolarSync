/*
 * File:    StatCard.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: One summary number on a home page, linking to the screen behind it.
 */
import { Link } from 'react-router-dom'
import Card from 'react-bootstrap/Card'
import Spinner from 'react-bootstrap/Spinner'

// Shows a label and a count. `value` undefined = still loading, null = failed to load
// (shown as '—' so one failed call does not blank the whole home page).
export default function StatCard({ label, value, to, linkText }) {
  return (
    <Card className="h-100 shadow-sm">
      <Card.Body>
        <Card.Subtitle className="text-secondary mb-2">{label}</Card.Subtitle>
        <div className="display-6 fw-semibold">
          {value === undefined ? <Spinner animation="border" size="sm" /> : (value ?? '—')}
        </div>
      </Card.Body>
      {to && (
        <Card.Footer className="bg-transparent">
          <Link to={to}>{linkText}</Link>
        </Card.Footer>
      )}
    </Card>
  )
}
