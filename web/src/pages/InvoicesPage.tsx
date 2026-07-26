import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, formatDate, formatMoney, type Customer, type InvoiceSummary } from '../api'

const STATUSES = ['', 'draft', 'sent', 'paid', 'void']

export default function InvoicesPage() {
  const [invoices, setInvoices] = useState<InvoiceSummary[]>([])
  const [customers, setCustomers] = useState<Customer[]>([])
  const [status, setStatus] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)

  const [customerId, setCustomerId] = useState('')
  const [description, setDescription] = useState('Monthly retainer')
  const [quantity, setQuantity] = useState('1')
  const [unitPrice, setUnitPrice] = useState('250.00')

  async function load() {
    try {
      const query = status ? `?status=${status}` : ''
      setInvoices(await api.get<InvoiceSummary[]>(`/invoices${query}`))
      setError(null)
    } catch (err) {
      setError(String(err))
    }
  }

  useEffect(() => {
    void load()
  }, [status])

  useEffect(() => {
    api.get<Customer[]>('/customers').then((list) => {
      setCustomers(list)
      setCustomerId((current) => current || list[0]?.id || '')
    })
  }, [])

  async function createInvoice(event: React.FormEvent) {
    event.preventDefault()
    setCreating(true)
    try {
      await api.post('/invoices', {
        customerId,
        termDays: 30,
        currency: 'USD',
        lines: [
          {
            description,
            quantity: Number(quantity),
            unitPriceCents: Math.round(Number(unitPrice) * 100),
            taxRateBp: 875,
          },
        ],
      })
      await load()
    } catch (err) {
      setError(String(err))
    } finally {
      setCreating(false)
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>Invoices</h1>
        <div className="toolbar">
          <label className="field">
            <span>Status</span>
            <select value={status} onChange={(e) => setStatus(e.target.value)}>
              {STATUSES.map((s) => (
                <option key={s} value={s}>
                  {s === '' ? 'All' : s}
                </option>
              ))}
            </select>
          </label>
        </div>
      </div>

      {error && <div className="error">{error}</div>}

      <div className="card">
        <h2>New invoice</h2>
        <form onSubmit={createInvoice}>
          <div className="grid">
            <label className="field">
              <span>Customer</span>
              <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} required>
                {customers.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>Description</span>
              <input value={description} onChange={(e) => setDescription(e.target.value)} required />
            </label>
            <label className="field">
              <span>Quantity</span>
              <input value={quantity} onChange={(e) => setQuantity(e.target.value)} required />
            </label>
            <label className="field">
              <span>Unit price</span>
              <input value={unitPrice} onChange={(e) => setUnitPrice(e.target.value)} required />
            </label>
          </div>
          <p>
            <button type="submit" disabled={creating || !customerId}>
              {creating ? 'Creating…' : 'Create draft'}
            </button>
          </p>
        </form>
      </div>

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>Number</th>
              <th>Customer</th>
              <th>Status</th>
              <th>Issued</th>
              <th>Due</th>
              <th className="num">Total</th>
            </tr>
          </thead>
          <tbody>
            {invoices.map((invoice) => (
              <tr key={invoice.id}>
                <td>
                  <Link to={`/invoices/${invoice.id}`}>{invoice.number}</Link>
                </td>
                <td>{invoice.customerName}</td>
                <td>
                  <span className={`badge ${invoice.status}`}>{invoice.status}</span>
                </td>
                <td>{formatDate(invoice.issuedOn)}</td>
                <td>{formatDate(invoice.dueOn)}</td>
                <td className="num">{formatMoney(invoice.totalCents, invoice.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {invoices.length === 0 && <p className="empty">No invoices match this filter.</p>}
      </div>
    </>
  )
}
