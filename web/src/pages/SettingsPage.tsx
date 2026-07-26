import { useEffect, useState } from 'react'
import { api, type Branding } from '../api'

const LOGOS = ['northwind.svg', 'atlas.svg', 'verity.svg', 'brightpath.svg', 'default.svg']

export default function SettingsPage() {
  const [branding, setBranding] = useState<Branding | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    api
      .get<Branding>('/settings')
      .then(setBranding)
      .catch((err) => setError(String(err)))
  }, [])

  async function save(event: React.FormEvent) {
    event.preventDefault()
    if (!branding) return
    try {
      await api.put('/settings', branding)
      setSaved(true)
      window.setTimeout(() => setSaved(false), 2500)
    } catch (err) {
      setError(String(err))
    }
  }

  function update<K extends keyof Branding>(key: K, value: Branding[K]) {
    setBranding((current) => (current ? { ...current, [key]: value } : current))
  }

  if (error) return <div className="error">{error}</div>
  if (!branding) return <p className="empty">Loading…</p>

  return (
    <>
      <div className="page-head">
        <h1>Branding</h1>
      </div>

      {saved && <div className="notice">Saved. New invoice emails will use these details.</div>}

      <div className="card">
        <form onSubmit={save}>
          <div className="grid">
            <label className="field">
              <span>Legal name</span>
              <input value={branding.legalName} onChange={(e) => update('legalName', e.target.value)} />
            </label>
            <label className="field">
              <span>Accent colour</span>
              <input value={branding.accentColor} onChange={(e) => update('accentColor', e.target.value)} />
            </label>
            <label className="field">
              <span>Logo</span>
              <select value={branding.logoFile} onChange={(e) => update('logoFile', e.target.value)}>
                {LOGOS.map((logo) => (
                  <option key={logo} value={logo}>
                    {logo}
                  </option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Reply-to</span>
              <input value={branding.replyTo} onChange={(e) => update('replyTo', e.target.value)} />
            </label>
          </div>

          <p>
            <img src={`/branding/${branding.logoFile}`} alt="Logo preview" height={36} />
          </p>

          <label className="field">
            <span>Remit-to</span>
            <input value={branding.remitTo} onChange={(e) => update('remitTo', e.target.value)} />
          </label>

          <label className="field" style={{ marginTop: 12 }}>
            <span>Email footer</span>
            <textarea
              rows={3}
              value={branding.emailFooter}
              onChange={(e) => update('emailFooter', e.target.value)}
            />
          </label>

          <p>
            <button type="submit">Save branding</button>
          </p>
        </form>
      </div>
    </>
  )
}
