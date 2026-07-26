export const TENANT_STORAGE_KEY = 'ledgerline.tenant'

let activeTenant = localStorage.getItem(TENANT_STORAGE_KEY) ?? 'northwind-studio'

export function getActiveTenant(): string {
  return activeTenant
}

export function setActiveTenant(slug: string): void {
  activeTenant = slug
  localStorage.setItem(TENANT_STORAGE_KEY, slug)
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message)
  }
}

async function request<T>(path: string, init: RequestInit = {}, withTenant = true): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }
  if (withTenant) {
    headers.set('X-Tenant', activeTenant)
  }

  const response = await fetch(`/api${path}`, { ...init, headers })

  if (!response.ok) {
    const detail = await response.text()
    throw new ApiError(response.status, detail || response.statusText)
  }

  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T
  }

  return (await response.json()) as T
}

export const api = {
  get: <T,>(path: string) => request<T>(path),
  post: <T,>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T,>(path: string, body: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  admin: <T,>(path: string) => request<T>(path, {}, false),
}

export interface TenantRef {
  id: string
  slug: string
  name: string
}

export interface InvoiceSummary {
  id: string
  number: string
  status: string
  currency: string
  customerId: string
  customerName: string
  issuedOn: string
  dueOn: string
  totalCents: number
  sentAt: string | null
}

export interface InvoiceLine {
  id: string
  position: number
  description: string
  quantity: number
  unitPriceCents: number
  taxRateBp: number
  amountCents: number
}

export interface InvoiceDetail extends Omit<InvoiceSummary, 'totalCents'> {
  customerEmail: string
  subtotalCents: number
  taxCents: number
  totalCents: number
  paidCents: number
  notes: string | null
  lines: InvoiceLine[]
}

export interface Customer {
  id: string
  name: string
  email: string
  externalRef: string | null
  invoiceCount: number
}

export interface Branding {
  legalName: string
  accentColor: string
  logoFile: string
  replyTo: string
  remitTo: string
  emailFooter: string
}

export interface EmailActivity {
  id: string
  invoiceId: string
  invoiceNumber: string
  toAddress: string
  subject: string
  status: string
  error: string | null
  queuedAt: string
  sentAt: string | null
}

export function formatMoney(cents: number, currency = 'USD'): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(cents / 100)
}

export function formatDate(value: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
}
