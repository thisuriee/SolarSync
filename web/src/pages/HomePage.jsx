/*
 * File:    HomePage.jsx
 * Author:  Aman
 * Created: 2026-09-28
 * Purpose: The public landing page at "/". The one screen a visitor sees before signing
 *          in: what the system is for, who uses it, and how a transfer works end to end.
 *
 *          Deliberately makes no API call. It is served to callers with no token, so
 *          there is nothing it could ask for — and nothing it shows is a live figure
 *          dressed up as one.
 *
 *          A signed-in visitor gets a link onward to their own home rather than a sign-in
 *          button they do not need.
 */
import { Link } from 'react-router-dom'
import Button from 'react-bootstrap/Button'
import Card from 'react-bootstrap/Card'
import Col from 'react-bootstrap/Col'
import Container from 'react-bootstrap/Container'
import Row from 'react-bootstrap/Row'
import { useAuth } from '../context/useAuth'
import { homePathForRole } from '../utils/roles'

// The three actors, in the order a transfer moves through them.
const ACTORS = [
  {
    title: 'Solar Prosumer',
    where: 'Mobile app',
    body: 'Finds a nearby hub on the map, reserves a charging window, and carries a QR code that proves the booking when they arrive.',
  },
  {
    title: 'Grid Operator',
    where: 'Web and mobile',
    body: 'Keeps each hub’s availability up to date, approves or rejects bookings, and scans the prosumer’s code on site to finalise the transfer.',
  },
  {
    title: 'Backoffice Officer',
    where: 'Web app',
    body: 'Registers microgrid nodes with their location, capacity and opening hours, and administers prosumer and staff accounts.',
  },
]

// How one energy transfer travels through the system.
const JOURNEY = [
  { step: '01', title: 'Reserve', body: 'A prosumer picks a hub and a window within the next seven days.' },
  { step: '02', title: 'Approve', body: 'An operator reviews the request and confirms the capacity.' },
  { step: '03', title: 'Verify', body: 'A single-use code is issued and scanned at the hub on arrival.' },
  { step: '04', title: 'Complete', body: 'The transfer is recorded against the booking and the slot is released.' },
]

// What the platform keeps track of. Descriptions of the system, not live counts —
// a public page has no session to read them with.
const CAPABILITIES = [
  { title: 'Located hubs', body: 'Every node carries real coordinates, so the app can rank hubs by distance from where you are standing.' },
  { title: 'Shared windows', body: 'A booking window holds several prosumers at once, bounded by the hub’s physical battery slots.' },
  { title: 'Seven-day horizon', body: 'Bookings open a week ahead, with changes and cancellations needing twelve hours’ notice.' },
  { title: 'Verified transfers', body: 'A transfer is only ever finalised against a code scanned in person at the hub.' },
]

// The public landing page. Rendered outside the signed-in layout, so it brings its own
// container and footer.
export default function HomePage() {
  const { user } = useAuth()

  return (
    <div className="bg-white">
      {/* Hero. The one place the page uses colour heavily — a warm sunrise gradient,
          which is the only visual nod to what the system is about. */}
      <div className="text-white py-5" style={heroStyle}>
        <Container className="py-4 py-lg-5">
          <Row className="align-items-center g-4">
            <Col lg={7}>
              <p className="text-uppercase fw-semibold mb-2 opacity-75" style={{ letterSpacing: '0.08em' }}>
                Smart Solar Microgrid
              </p>
              <h1 className="display-5 fw-bold mb-3">
                Trade solar capacity across a shared grid
              </h1>
              <p className="fs-5 mb-4 opacity-75" style={{ maxWidth: '38rem' }}>
                SolarSync connects households with surplus solar energy to a network of
                neighbourhood storage hubs — reserve a window, turn up, and let the grid
                do the rest.
              </p>

              {user ? (
                <Button as={Link} to={homePathForRole(user.role)} size="lg" variant="light">
                  Continue to your dashboard
                </Button>
              ) : (
                <Button as={Link} to="/login" size="lg" variant="light">
                  Sign in
                </Button>
              )}

              {!user && (
                <p className="small mt-3 mb-0 opacity-75">
                  Prosumers register and book through the Android app.
                </p>
              )}
            </Col>

            <Col lg={5} className="d-none d-lg-block">
              {/* A plain summary panel rather than an illustration: it says something
                  true about the system, and it cannot go stale. */}
              <div className="rounded-4 p-4" style={panelStyle}>
                <div className="fw-semibold mb-3">What a hub holds</div>
                {[
                  ['Location', 'Latitude and longitude, mapped'],
                  ['Capacity', 'Stored energy in kilowatt-hours'],
                  ['Battery slots', 'How many can charge at once'],
                  ['Opening hours', 'Per day of the week'],
                ].map(([label, detail]) => (
                  <div key={label} className="d-flex justify-content-between py-2 border-top border-light border-opacity-25">
                    <span className="opacity-75">{label}</span>
                    <span className="text-end">{detail}</span>
                  </div>
                ))}
              </div>
            </Col>
          </Row>
        </Container>
      </div>

      {/* Who uses it */}
      <Container className="py-5">
        <Row className="mb-4">
          <Col lg={8}>
            <h2 className="h3 mb-2">Three ways in</h2>
            <p className="text-secondary mb-0">
              Each role sees only the tools its work needs, and every action is decided by
              the central service rather than by the app asking.
            </p>
          </Col>
        </Row>

        <Row xs={1} md={3} className="g-4">
          {ACTORS.map((actor) => (
            <Col key={actor.title}>
              <Card className="h-100 shadow-sm border-0">
                <Card.Body className="p-4">
                  <div className="text-uppercase small fw-semibold text-secondary mb-2">
                    {actor.where}
                  </div>
                  <Card.Title className="h5">{actor.title}</Card.Title>
                  <Card.Text className="text-secondary mb-0">{actor.body}</Card.Text>
                </Card.Body>
              </Card>
            </Col>
          ))}
        </Row>
      </Container>

      {/* How a transfer works */}
      <div className="border-top" style={{ backgroundColor: '#f5f6f8' }}>
        <Container className="py-5">
          <h2 className="h3 mb-4">From booking to transfer</h2>
          <Row xs={1} sm={2} lg={4} className="g-4">
            {JOURNEY.map((stage) => (
              <Col key={stage.step}>
                <div className="h-100">
                  <div className="fw-bold fs-4 mb-2" style={{ color: '#d97706' }}>
                    {stage.step}
                  </div>
                  <div className="fw-semibold mb-1">{stage.title}</div>
                  <p className="text-secondary small mb-0">{stage.body}</p>
                </div>
              </Col>
            ))}
          </Row>
        </Container>
      </div>

      {/* What the platform tracks */}
      <Container className="py-5">
        <h2 className="h3 mb-4">Built around the grid</h2>
        <Row xs={1} md={2} className="g-4">
          {CAPABILITIES.map((item) => (
            <Col key={item.title}>
              <div className="d-flex gap-3">
                <div className="flex-shrink-0 rounded-circle mt-1" style={dotStyle} />
                <div>
                  <div className="fw-semibold mb-1">{item.title}</div>
                  <p className="text-secondary mb-0">{item.body}</p>
                </div>
              </div>
            </Col>
          ))}
        </Row>
      </Container>

      <footer className="border-top py-4">
        <Container className="d-flex flex-wrap justify-content-between gap-2 text-secondary small">
          <span>SolarSync · Smart Solar Microgrid Trading System</span>
          <span>SE4040 Enterprise Application Development · Group 49</span>
        </Container>
      </footer>
    </div>
  )
}

// Sunrise gradient behind the hero. Inline rather than in index.css because it belongs
// to this one screen and nothing else should inherit it.
const heroStyle = {
  background: 'linear-gradient(135deg, #b45309 0%, #ea8a1e 55%, #f5b942 100%)',
}

// Translucent panel on the hero, so it reads as part of the gradient.
const panelStyle = {
  backgroundColor: 'rgba(255, 255, 255, 0.12)',
  border: '1px solid rgba(255, 255, 255, 0.2)',
}

// Small amber marker beside each capability.
const dotStyle = {
  width: '0.625rem',
  height: '0.625rem',
  backgroundColor: '#ea8a1e',
}
