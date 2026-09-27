/*
 * File:    QuickLinks.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Home-page shortcuts to every screen the signed-in role may open, taken from
 *          layouts/navItems.js so the home page and the navbar can never disagree.
 */
import { Link } from 'react-router-dom'
import ListGroup from 'react-bootstrap/ListGroup'
import { navItemsForRole } from '../layouts/navItems'
import { homePathForRole } from '../utils/roles'

// Lists the role's nav entries except its own home page. Convenience only — the API
// still enforces each route's roles.
export default function QuickLinks({ role }) {
  const home = homePathForRole(role)
  const items = navItemsForRole(role).filter((item) => item.to !== home)

  return (
    <ListGroup>
      {items.map((item) => (
        <ListGroup.Item key={item.to} action as={Link} to={item.to}>
          {item.label}
        </ListGroup.Item>
      ))}
    </ListGroup>
  )
}
