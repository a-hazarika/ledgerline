import { useEffect, useState } from 'react'
import { NavLink, Navigate, Route, Routes } from 'react-router-dom'
import { api, getActiveTenant, setActiveTenant, type TenantRef } from './api'
import CustomersPage from './pages/CustomersPage'
import EmailActivityPage from './pages/EmailActivityPage'
import InvoiceDetailPage from './pages/InvoiceDetailPage'
import InvoicesPage from './pages/InvoicesPage'
import SettingsPage from './pages/SettingsPage'

const MAILPIT_URL = 'http://localhost:8025'

export default function App() {
  const [tenants, setTenants] = useState<TenantRef[]>([])
  const [tenant, setTenant] = useState(getActiveTenant())

  useEffect(() => {
    api.admin<TenantRef[]>('/admin/tenants').then(setTenants).catch(() => setTenants([]))
  }, [])

  function switchTenant(slug: string) {
    setActiveTenant(slug)
    setTenant(slug)
    // Every page's data is tenant-scoped; a reload is the honest way to reset it all.
    window.location.reload()
  }

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="mark" />
          Ledgerline
        </div>

        <nav>
          <NavLink to="/invoices">Invoices</NavLink>
          <NavLink to="/customers">Customers</NavLink>
          <NavLink to="/email">Email</NavLink>
          <NavLink to="/settings">Settings</NavLink>
        </nav>

        <div className="topbar-right">
          <a className="mailpit" href={MAILPIT_URL} target="_blank" rel="noreferrer">
            Mailpit ↗
          </a>
          <label className="tenant-switch">
            <span>Tenant</span>
            <select value={tenant} onChange={(event) => switchTenant(event.target.value)}>
              {tenants.length === 0 && <option value={tenant}>{tenant}</option>}
              {tenants.map((t) => (
                <option key={t.id} value={t.slug}>
                  {t.name}
                </option>
              ))}
            </select>
          </label>
        </div>
      </header>

      <main>
        <Routes>
          <Route path="/" element={<Navigate to="/invoices" replace />} />
          <Route path="/invoices" element={<InvoicesPage />} />
          <Route path="/invoices/:id" element={<InvoiceDetailPage />} />
          <Route path="/customers" element={<CustomersPage />} />
          <Route path="/email" element={<EmailActivityPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="*" element={<p className="empty">Nothing here.</p>} />
        </Routes>
      </main>
    </div>
  )
}
