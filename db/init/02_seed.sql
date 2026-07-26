-- Demo data for local development.
-- Four tenants with distinct branding so the app is usable end to end without
-- having to hand-create an account first.

INSERT INTO tenants (id, slug, name, plan) VALUES
    ('8c1a4f62-3d7b-4e19-9a02-51f6c8b3d470', 'northwind-studio',  'Northwind Studio',   'standard'),
    ('2e9d70b4-6c15-4a83-b7de-90a4f1c25836', 'atlas-freight',     'Atlas Freight',      'growth'),
    ('c47b1e58-9f02-4d6a-8351-6be0a7d9421c', 'verity-health',     'Verity Health',      'enterprise'),
    ('5f83d016-7a4e-42bc-96d1-3c85e0b7f249', 'brightpath-labs',   'BrightPath Labs',    'standard');

INSERT INTO tenant_settings (tenant_id, legal_name, accent_color, logo_file, reply_to, remit_to, email_footer) VALUES
    ('8c1a4f62-3d7b-4e19-9a02-51f6c8b3d470', 'Northwind Studio Ltd',      '#2f6f4e', 'northwind.svg',
     'billing@northwind-studio.test',
     'Northwind Studio Ltd · IBAN GB29 NWBK 6016 1331 9268 19',
     'Northwind Studio Ltd is registered in England and Wales, no. 08812234.'),
    ('2e9d70b4-6c15-4a83-b7de-90a4f1c25836', 'Atlas Freight Partners LLC', '#b4531f', 'atlas.svg',
     'ar@atlas-freight.test',
     'Atlas Freight Partners LLC · Routing 021000021 · Acct 4417-9920',
     'Freight charges are governed by our published tariff, revision 14.'),
    ('c47b1e58-9f02-4d6a-8351-6be0a7d9421c', 'Verity Health Group',        '#1d4e89', 'verity.svg',
     'accounts@verity-health.test',
     'Verity Health Group · Wire to First Cascade Bank, acct 7781-2200',
     'This statement may contain protected health information. Handle accordingly.'),
    ('5f83d016-7a4e-42bc-96d1-3c85e0b7f249', 'BrightPath Labs, Inc.',      '#6b3fa0', 'brightpath.svg',
     'invoices@brightpath-labs.test',
     'BrightPath Labs, Inc. · ACH 0031-7788-2',
     'Questions? Reply to this email and a human will get back to you.');

DO $$
DECLARE
    t           RECORD;
    cust_names  text[] := ARRAY['Halcyon Robotics', 'Pearl District Coffee', 'Kestrel Logistics',
                                'Ferrous Design Co', 'Lantern Bay Media'];
    line_descs  text[] := ARRAY['Monthly retainer', 'Onboarding services', 'Platform usage',
                                'Support hours', 'Data migration'];
    c_id        uuid;
    i_id        uuid;
    n           int;
    k           int;
    ln          int;
    st          text;
    seq         int;
BEGIN
    FOR t IN SELECT id, slug FROM tenants ORDER BY slug LOOP
        seq := 0;
        FOR n IN 1 .. array_length(cust_names, 1) LOOP
            c_id := gen_random_uuid();
            INSERT INTO customers (id, tenant_id, name, email, external_ref)
            VALUES (c_id, t.id, cust_names[n],
                    lower(replace(cust_names[n], ' ', '.')) || '@' || t.slug || '.example.test',
                    'C-' || lpad(n::text, 4, '0'));

            FOR k IN 1 .. 3 LOOP
                seq := seq + 1;
                i_id := gen_random_uuid();
                st := CASE k WHEN 1 THEN 'paid' WHEN 2 THEN 'sent' ELSE 'draft' END;

                INSERT INTO invoices (id, tenant_id, customer_id, number, status,
                                      issued_on, due_on, sent_at)
                VALUES (i_id, t.id, c_id, 'INV-' || lpad((1000 + seq)::text, 5, '0'), st,
                        current_date - (k * 17), current_date - (k * 17) + 30,
                        CASE WHEN st = 'draft' THEN NULL ELSE now() - (k || ' days')::interval END);

                FOR ln IN 1 .. (2 + (n + k) % 3) LOOP
                    INSERT INTO invoice_lines (id, tenant_id, invoice_id, position, description,
                                               quantity, unit_price_cents, tax_rate_bp)
                    VALUES (gen_random_uuid(), t.id, i_id, ln,
                            line_descs[1 + ((n + k + ln) % array_length(line_descs, 1))],
                            (1 + (ln % 4))::numeric,
                            25000 + ((n * 7 + k * 13 + ln * 29) % 40) * 1000,
                            CASE WHEN t.slug = 'verity-health' THEN 0 ELSE 875 END);
                END LOOP;
            END LOOP;
        END LOOP;
    END LOOP;
END $$;

UPDATE invoices i
SET subtotal_cents = s.sub,
    tax_cents      = s.tax,
    total_cents    = s.sub + s.tax
FROM (
    SELECT invoice_id,
           SUM(round(quantity * unit_price_cents))::bigint                        AS sub,
           SUM(round(quantity * unit_price_cents * tax_rate_bp / 10000.0))::bigint AS tax
    FROM invoice_lines
    GROUP BY invoice_id
) s
WHERE s.invoice_id = i.id;

INSERT INTO payments (id, tenant_id, invoice_id, amount_cents, method, reference, paid_at)
SELECT gen_random_uuid(), tenant_id, id, total_cents, 'ach',
       'REF-' || upper(substr(id::text, 1, 8)), now() - interval '3 days'
FROM invoices
WHERE status = 'paid';

INSERT INTO email_log (id, tenant_id, invoice_id, to_address, subject, status, queued_at, sent_at)
SELECT gen_random_uuid(), i.tenant_id, i.id, c.email,
       'Invoice ' || i.number, 'sent', i.sent_at, i.sent_at
FROM invoices i
JOIN customers c ON c.id = i.customer_id
WHERE i.status = 'sent';
