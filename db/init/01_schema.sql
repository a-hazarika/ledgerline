-- Ledgerline core schema.
-- Applied by the postgres image entrypoint on first start of an empty data volume.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE tenants (
    id          uuid PRIMARY KEY,
    slug        text NOT NULL UNIQUE,
    name        text NOT NULL,
    plan        text NOT NULL DEFAULT 'standard',
    created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE tenant_settings (
    tenant_id       uuid PRIMARY KEY REFERENCES tenants (id) ON DELETE CASCADE,
    legal_name      text NOT NULL,
    accent_color    text NOT NULL DEFAULT '#2f6f4e',
    logo_file       text NOT NULL DEFAULT 'default.svg',
    reply_to        text NOT NULL,
    remit_to        text NOT NULL,
    email_footer    text NOT NULL DEFAULT '',
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE customers (
    id           uuid PRIMARY KEY,
    tenant_id    uuid NOT NULL REFERENCES tenants (id) ON DELETE CASCADE,
    name         text NOT NULL,
    email        text NOT NULL,
    external_ref text,
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_customers_tenant ON customers (tenant_id);

CREATE TABLE invoices (
    id             uuid PRIMARY KEY,
    tenant_id      uuid NOT NULL REFERENCES tenants (id) ON DELETE CASCADE,
    customer_id    uuid NOT NULL REFERENCES customers (id) ON DELETE RESTRICT,
    number         text NOT NULL,
    status         text NOT NULL DEFAULT 'draft',
    currency       char(3) NOT NULL DEFAULT 'USD',
    issued_on      date NOT NULL,
    due_on         date NOT NULL,
    subtotal_cents bigint NOT NULL DEFAULT 0,
    tax_cents      bigint NOT NULL DEFAULT 0,
    total_cents    bigint NOT NULL DEFAULT 0,
    notes          text,
    created_at     timestamptz NOT NULL DEFAULT now(),
    sent_at        timestamptz,
    CONSTRAINT uq_invoices_tenant_number UNIQUE (tenant_id, number)
);

CREATE INDEX ix_invoices_tenant_status ON invoices (tenant_id, status);

CREATE TABLE invoice_lines (
    id               uuid PRIMARY KEY,
    tenant_id        uuid NOT NULL REFERENCES tenants (id) ON DELETE CASCADE,
    invoice_id       uuid NOT NULL REFERENCES invoices (id) ON DELETE CASCADE,
    position         int NOT NULL,
    description      text NOT NULL,
    quantity         numeric(12, 3) NOT NULL,
    unit_price_cents bigint NOT NULL,
    tax_rate_bp      int NOT NULL DEFAULT 0
);

CREATE INDEX ix_invoice_lines_invoice ON invoice_lines (invoice_id);

CREATE TABLE payments (
    id           uuid PRIMARY KEY,
    tenant_id    uuid NOT NULL REFERENCES tenants (id) ON DELETE CASCADE,
    invoice_id   uuid NOT NULL REFERENCES invoices (id) ON DELETE CASCADE,
    amount_cents bigint NOT NULL,
    method       text NOT NULL,
    reference    text,
    paid_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_payments_invoice ON payments (invoice_id);

CREATE TABLE email_log (
    id          uuid PRIMARY KEY,
    tenant_id   uuid NOT NULL REFERENCES tenants (id) ON DELETE CASCADE,
    invoice_id  uuid NOT NULL REFERENCES invoices (id) ON DELETE CASCADE,
    to_address  text NOT NULL,
    subject     text NOT NULL DEFAULT '',
    status      text NOT NULL DEFAULT 'queued',
    error       text,
    queued_at   timestamptz NOT NULL DEFAULT now(),
    sent_at     timestamptz
);

CREATE INDEX ix_email_log_tenant ON email_log (tenant_id, queued_at DESC);
