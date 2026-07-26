import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, formatDate, type EmailActivity } from '../api'

export default function EmailActivityPage() {
  const [activity, setActivity] = useState<EmailActivity[]>([])
  const [depth, setDepth] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function load() {
    try {
      const [rows, queue] = await Promise.all([
        api.get<EmailActivity[]>('/emails'),
        api.get<{ depth: number }>('/emails/queue'),
      ])
      setActivity(rows)
      setDepth(queue.depth)
      setError(null)
    } catch (err) {
      setError(String(err))
    }
  }

  useEffect(() => {
    void load()
    const timer = window.setInterval(() => void load(), 4000)
    return () => window.clearInterval(timer)
  }, [])

  return (
    <>
      <div className="page-head">
        <h1>Email activity</h1>
        <span className="muted">Queue depth: {depth ?? '—'}</span>
      </div>

      {error && <div className="error">{error}</div>}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>Invoice</th>
              <th>Recipient</th>
              <th>Subject</th>
              <th>Status</th>
              <th>Queued</th>
              <th>Sent</th>
            </tr>
          </thead>
          <tbody>
            {activity.map((row) => (
              <tr key={row.id}>
                <td>
                  <Link to={`/invoices/${row.invoiceId}`}>{row.invoiceNumber}</Link>
                </td>
                <td>{row.toAddress}</td>
                <td className="muted">{row.subject || '—'}</td>
                <td>
                  <span className={`badge ${row.status}`}>{row.status}</span>
                  {row.error && <div className="muted">{row.error}</div>}
                </td>
                <td>{formatDate(row.queuedAt)}</td>
                <td>{formatDate(row.sentAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {activity.length === 0 && <p className="empty">Nothing sent yet.</p>}
      </div>
    </>
  )
}
