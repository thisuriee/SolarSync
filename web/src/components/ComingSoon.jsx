/*
 * File:    ComingSoon.jsx
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: Placeholder for routes whose screen is not built yet, so the nav and route
 *          table can be wired now. Each owner replaces it with their page in App.jsx.
 */

// Shows the page title and who is building it.
export default function ComingSoon({ title, owner }) {
  return (
    <>
      <h1 className="h3">{title}</h1>
      <p className="text-secondary">This screen is being built ({owner}).</p>
    </>
  )
}
