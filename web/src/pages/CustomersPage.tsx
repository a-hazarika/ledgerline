import { useEffect, useState } from 'react'
import { api, type Customer } from '../api'

export default function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([])
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function load() {
    try {
      setCustomers(await api.get<Customer[]>('/customers'))
      setError(null)
    } catch (err) {
      setError(String(err))
    }
  }

  useEffect(() => {
    void load()
  }, [])

  async function addCustomer(event: React.FormEvent) {
    event.preventDefault()
    try {
      await api.post('/customers', { name, email, externalRef: null })
      setName('')
      setEmail('')
      await load()
    } catch (err) {
      setError(String(err))
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>Customers</h1>
      </div>

      {error && <div className="error">{error}</div>}

      <div className="card">
        <h2>Add customer</h2>
        <form onSubmit={addCustomer}>
          <div className="grid">
            <label className="field">
              <span>Name</span>
              <input value={name} onChange={(e) => setName(e.target.value)} required />
            </label>
            <label className="field">
              <span>Email</span>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </label>
          </div>
          <p>
            <button type="submit">Add</button>
          </p>
        </form>
      </div>

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Reference</th>
              <th className="num">Invoices</th>
            </tr>
          </thead>
          <tbody>
            {customers.map((customer) => (
              <tr key={customer.id}>
                <td>{customer.name}</td>
                <td>{customer.email}</td>
                <td className="muted">{customer.externalRef ?? '—'}</td>
                <td className="num">{customer.invoiceCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {customers.length === 0 && <p className="empty">No customers yet.</p>}
      </div>
    </>
  )
}
