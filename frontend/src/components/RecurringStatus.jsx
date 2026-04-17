import { useEffect, useState } from 'react';
import { fetchRecurring, forceRecurring, deleteCustomer } from '../services/api';
import ErrorBanner from './ErrorBanner';

const POLL_INTERVAL = 30_000;

export default function RecurringStatus() {
  const [customers, setCustomers] = useState([]);
  const [error, setError] = useState('');
  const [lastRefresh, setLastRefresh] = useState(null);
  const [forcing, setForcing] = useState(false);

  const load = async () => {
    try {
      const data = await fetchRecurring();
      setCustomers(data);
      setLastRefresh(new Date());
      setError('');
    } catch (err) {
      setError(err.message);
    }
  };

  useEffect(() => {
    load();
    const timer = setInterval(load, POLL_INTERVAL);
    return () => clearInterval(timer);
  }, []);

  const handleForce = async () => {
    setForcing(true);
    setError('');
    try {
      await forceRecurring();
      await load();
    } catch (err) {
      setError(err.message);
    } finally {
      setForcing(false);
    }
  };

  const handleDelete = async (customerKey) => {
    try {
      await deleteCustomer(customerKey);
      setCustomers((prev) => prev.filter((c) => c.customerKey !== customerKey));
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div style={styles.container}>
      <div style={styles.header}>
        <h2 style={styles.heading}>Recurring Billing Status</h2>
        <div style={styles.headerButtons}>
          <button
            style={{ ...styles.actionBtn, opacity: forcing ? 0.6 : 1 }}
            onClick={handleForce}
            disabled={forcing}
          >
            {forcing ? 'Billing…' : '⚡ Force Billing'}
          </button>
          <button style={styles.refreshBtn} onClick={load}>↻ Refresh</button>
        </div>
      </div>

      {lastRefresh && (
        <p style={styles.hint}>
          Last updated: {lastRefresh.toLocaleTimeString()} — auto-refreshes every 30 s
        </p>
      )}

      <ErrorBanner error={error} onDismiss={() => setError('')} />

      {customers.length === 0 ? (
        <div style={styles.empty}>
          No saved customers yet. Save a card using the payment form to enrol in recurring billing.
        </div>
      ) : (
        customers.map((c) => (
          <CustomerCard key={c.customerKey} customer={c} onDelete={handleDelete} />
        ))
      )}
    </div>
  );
}

function CustomerCard({ customer, onDelete }) {
  const [expanded, setExpanded] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const payments = customer.payments ?? [];
  const successCount = payments.filter((p) => p.success).length;

  return (
    <>
      {showConfirm && (
        <div style={styles.overlay}>
          <div style={styles.modal}>
            <h3 style={styles.modalTitle}>Remove customer?</h3>
            <p style={styles.modalBody}>
              <strong>{customer.name}</strong> will be removed from local recurring billing.
            </p>
            <p style={styles.modalNote}>
              ⚠ This does <em>not</em> delete the customer or payment method from the USAePay portal.
            </p>
            <div style={styles.modalActions}>
              <button style={styles.cancelBtn} onClick={() => setShowConfirm(false)}>
                Cancel
              </button>
              <button
                style={styles.confirmBtn}
                onClick={() => { setShowConfirm(false); onDelete(customer.customerKey); }}
              >
                Remove
              </button>
            </div>
          </div>
        </div>
      )}

      <div style={styles.card}>
        <div
          style={styles.cardHeader}
          role="button"
          tabIndex={0}
          onClick={() => setExpanded((x) => !x)}
          onKeyDown={(e) => e.key === 'Enter' && setExpanded((x) => !x)}
        >
          <div>
            <span style={styles.name}>{customer.name}</span>
            <span style={styles.email}>{customer.email}</span>
          </div>
          <div style={styles.meta}>
            <span style={styles.badge}>
              {successCount}/{payments.length} succeeded
            </span>
            <button
              style={styles.deleteBtn}
              onClick={(e) => { e.stopPropagation(); setShowConfirm(true); }}
            >
              ✕ Delete
            </button>
            <span style={styles.chevron}>{expanded ? '▲' : '▼'}</span>
          </div>
        </div>

        <div style={styles.custKey}>
          Customer key: <code>{customer.customerKey}</code>
        </div>
        {customer.paymentMethodKey && (
          <div style={styles.custKey}>
            Payment method key: <code>{customer.paymentMethodKey}</code>
          </div>
        )}

        {expanded && (
          <div style={styles.paymentList}>
            {payments.length === 0 ? (
              <p style={styles.noPayments}>No charges yet (next charge in ≤ 10 min)</p>
            ) : (
              [...payments].reverse().map((p, i) => (
                <PaymentRow key={i} payment={p} />
              ))
            )}
          </div>
        )}
      </div>
    </>
  );
}

function PaymentRow({ payment }) {
  return (
    <div style={{ ...styles.paymentRow, borderColor: payment.success ? '#68d391' : '#fc8181' }}>
      <span style={{ ...styles.status, color: payment.success ? '#276749' : '#c53030' }}>
        {payment.success ? '✓' : '✗'}
      </span>
      <div style={styles.paymentDetail}>
        <span style={styles.paymentTime}>
          {new Date(payment.timestamp).toLocaleString()}
        </span>
        <span style={styles.paymentAmt}>${payment.amount.toFixed(2)}</span>
        {payment.transactionKey && (
          <code style={styles.txnKey}>{payment.transactionKey}</code>
        )}
        {payment.error && (
          <span style={styles.paymentError}>{payment.error}</span>
        )}
      </div>
    </div>
  );
}

// ── Styles ───────────────────────────────────────────────────────────────────

const styles = {
  container: {
    background: '#fff',
    borderRadius: 12,
    padding: 28,
    boxShadow: '0 4px 24px rgba(0,0,0,.08)',
    maxWidth: 680,
    width: '100%',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 6,
  },
  heading: { fontSize: 22, fontWeight: 700, color: '#1a202c' },
  headerButtons: { display: 'flex', gap: 8 },
  actionBtn: {
    background: '#4299e1',
    color: '#fff',
    border: 'none',
    borderRadius: 6,
    padding: '5px 12px',
    fontSize: 13,
    fontWeight: 600,
    cursor: 'pointer',
  },
  refreshBtn: {
    background: 'none',
    border: '1px solid #cbd5e0',
    borderRadius: 6,
    padding: '5px 12px',
    fontSize: 13,
    cursor: 'pointer',
    color: '#4a5568',
  },
  hint: { fontSize: 12, color: '#718096', marginBottom: 14 },
  empty: {
    padding: 20,
    textAlign: 'center',
    color: '#718096',
    fontSize: 14,
    border: '1px dashed #cbd5e0',
    borderRadius: 8,
  },
  card: {
    border: '1px solid #e2e8f0',
    borderRadius: 8,
    marginBottom: 12,
    overflow: 'hidden',
  },
  cardHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: '12px 16px',
    cursor: 'pointer',
    background: '#f7fafc',
    userSelect: 'none',
  },
  name: { fontWeight: 600, fontSize: 15, marginRight: 10, color: '#1a202c' },
  email: { fontSize: 13, color: '#718096' },
  meta: { display: 'flex', alignItems: 'center', gap: 10 },
  badge: {
    background: '#ebf8ff',
    color: '#2b6cb0',
    fontSize: 12,
    fontWeight: 600,
    padding: '2px 8px',
    borderRadius: 20,
  },
  deleteBtn: {
    background: 'none',
    border: '1px solid #fc8181',
    color: '#c53030',
    borderRadius: 5,
    padding: '2px 8px',
    fontSize: 12,
    cursor: 'pointer',
    fontWeight: 600,
  },
  chevron: { color: '#718096', fontSize: 12 },
  custKey: {
    padding: '6px 16px',
    fontSize: 12,
    color: '#718096',
    background: '#f7fafc',
    borderTop: '1px solid #e2e8f0',
  },
  paymentList: { padding: '10px 16px', display: 'flex', flexDirection: 'column', gap: 6 },
  noPayments: { color: '#718096', fontSize: 13, textAlign: 'center', padding: 8 },
  paymentRow: {
    display: 'flex',
    alignItems: 'flex-start',
    gap: 10,
    padding: '8px 10px',
    borderLeft: '3px solid',
    borderRadius: 4,
    background: '#fafafa',
  },
  status: { fontWeight: 700, fontSize: 16, lineHeight: 1.4 },
  paymentDetail: { display: 'flex', flexWrap: 'wrap', gap: '4px 12px', fontSize: 13 },
  paymentTime: { color: '#4a5568' },
  paymentAmt: { fontWeight: 600, color: '#2d3748' },
  txnKey: { fontFamily: 'monospace', color: '#2b6cb0', fontSize: 12 },
  paymentError: { color: '#c53030', width: '100%' },
  // ── Confirmation modal ───────────────────────────────────────────────────
  overlay: {
    position: 'fixed',
    inset: 0,
    background: 'rgba(0,0,0,0.4)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 1000,
  },
  modal: {
    background: '#fff',
    borderRadius: 10,
    padding: 24,
    maxWidth: 400,
    width: '90%',
    boxShadow: '0 8px 32px rgba(0,0,0,0.18)',
  },
  modalTitle: { fontSize: 17, fontWeight: 700, color: '#1a202c', marginBottom: 10 },
  modalBody: { fontSize: 14, color: '#2d3748', marginBottom: 8 },
  modalNote: {
    fontSize: 13,
    color: '#744210',
    background: '#fefcbf',
    border: '1px solid #f6e05e',
    borderRadius: 6,
    padding: '8px 10px',
    marginBottom: 18,
  },
  modalActions: { display: 'flex', justifyContent: 'flex-end', gap: 10 },
  cancelBtn: {
    background: 'none',
    border: '1px solid #cbd5e0',
    borderRadius: 6,
    padding: '7px 16px',
    fontSize: 14,
    cursor: 'pointer',
    color: '#4a5568',
  },
  confirmBtn: {
    background: '#e53e3e',
    color: '#fff',
    border: 'none',
    borderRadius: 6,
    padding: '7px 16px',
    fontSize: 14,
    fontWeight: 600,
    cursor: 'pointer',
  },
};
