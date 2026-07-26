import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, formatDate, formatMoney, type InvoiceDetail } from '../api'

export default function InvoiceDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const [invoice, setInvoice] = useState<InvoiceDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    if (!id) return
    try {
      setInvoice(await api.get<InvoiceDetail>(`/invoices/${id}`))
      setError(null)
    } catch (err) {
      setError(String(err))
    }
  }, [id])

  useEffect(() => {
    void load()
  }, [load])

  async function send() {
    if (!id) return
    setBusy(true)
    try {
      await api.post(`/invoices/${id}/send`)
      setNotice('Queued for delivery. Check Mailpit in a moment.')
      window.setTimeout(() => void load(), 1200)
    } catch (err) {
      setError(String(err))
    } finally {
      setBusy(false)
    }
  }

  async function duplicate() {
    if (!id) return
    setBusy(true)
    try {
      const copy = await api.post<InvoiceDetail>(`/invoices/${id}/duplicate`)
      navigate(`/invoices/${copy.id}`)
    } catch (err) {
      setError(String(err))
    } finally {
      setBusy(false)
    }
  }

  async function payInFull() {
    if (!id || !invoice) return
    setBusy(true)
    try {
      setInvoice(
        await api.post<InvoiceDetail>(`/invoices/${id}/payments`, {
          amountCents: invoice.totalCents - invoice.paidCents,
          method: 'ach',
          reference: null,
        }),
      )
    } catch (err) {
      setError(String(err))
    } finally {
      setBusy(false)
    }
  }

  if (error) return <div className="error">{error}</div>
  if (!invoice) return <p className="empty">Loading…</p>

  return (
    <>
      <div className="page-head">
        <h1>
          {invoice.number} <span className={`badge ${invoice.status}`}>{invoice.status}</span>
        </h1>
        <div className="toolbar">
          <button className="secondary" onClick={duplicate} disabled={busy}>
            Duplicate
          </button>
          <button onClick={send} disabled={busy || invoice.status === 'void'}>
            {busy ? 'Working…' : 'Send invoice'}
          </button>
        </div>
      </div>

      {notice && <div className="notice">{notice}</div>}

      <div className="card">
        <div className="grid">
          <div>
            <p className="muted">Billed to</p>
            <p>
              <strong>{invoice.customerName}</strong>
              <br />
              {invoice.customerEmail}
            </p>
          </div>
          <div>
            <p className="muted">Issued</p>
            <p>{formatDate(invoice.issuedOn)}</p>
          </div>
          <div>
            <p className="muted">Due</p>
            <p>{formatDate(invoice.dueOn)}</p>
          </div>
          <div>
            <p className="muted">Last sent</p>
            <p>{formatDate(invoice.sentAt)}</p>
          </div>
        </div>
      </div>

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>Description</th>
              <th className="num">Qty</th>
              <th className="num">Unit</th>
              <th className="num">Tax</th>
              <th className="num">Amount</th>
            </tr>
          </thead>
          <tbody>
            {invoice.lines.map((line) => (
              <tr key={line.id}>
                <td>{line.description}</td>
                <td className="num">{line.quantity}</td>
                <td className="num">{formatMoney(line.unitPriceCents, invoice.currency)}</td>
                <td className="num">{(line.taxRateBp / 100).toFixed(2)}%</td>
                <td className="num">{formatMoney(line.amountCents, invoice.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="card">
        <table>
          <tbody>
            <tr>
              <td className="muted">Subtotal</td>
              <td className="num">{formatMoney(invoice.subtotalCents, invoice.currency)}</td>
            </tr>
            <tr>
              <td className="muted">Tax</td>
              <td className="num">{formatMoney(invoice.taxCents, invoice.currency)}</td>
            </tr>
            <tr>
              <td>
                <strong>Total</strong>
              </td>
              <td className="num">
                <strong>{formatMoney(invoice.totalCents, invoice.currency)}</strong>
              </td>
            </tr>
            <tr>
              <td className="muted">Paid</td>
              <td className="num">{formatMoney(invoice.paidCents, invoice.currency)}</td>
            </tr>
          </tbody>
        </table>
        {invoice.paidCents < invoice.totalCents && (
          <p>
            <button className="secondary" onClick={payInFull} disabled={busy}>
              Record payment in full
            </button>
          </p>
        )}
      </div>

      <p>
        <Link to="/invoices">← Back to invoices</Link>
      </p>
    </>
  )
}
