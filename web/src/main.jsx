import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// Bootstrap is imported once, here, for the whole app (docs/PLUMBING-GUIDE.md §8 step 1).
import 'bootstrap/dist/css/bootstrap.min.css'
import './index.css'
import App from './App.jsx'
import { AuthProvider } from './context/AuthContext'

// AuthProvider wraps everything so every page can read the signed-in user.
createRoot(document.getElementById('root')).render(
  <StrictMode>
    <AuthProvider>
      <App />
    </AuthProvider>
  </StrictMode>,
)
