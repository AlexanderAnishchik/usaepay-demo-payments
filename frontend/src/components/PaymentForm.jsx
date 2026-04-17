import { useEffect, useRef, useState } from 'react';
import { chargeCard, saveCustomer } from '../services/api';
import ErrorBanner from './ErrorBanner';
import SuccessBanner from './SuccessBanner';

// ── pay.js card-entry style ──────────────────────────────────────────────────

const CARD_STYLE = {
  base: {
    color: '#1a202c',
    fontSize: '15px',
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
    '::placeholder': { color: '#a0aec0' },
  },
  invalid: { color: '#c53030' },
};

// ── Component ────────────────────────────────────────────────────────────────

export default function PaymentForm({ publicApiKey }) {
  const cardContainerRef = useRef(null);
  const payClientRef = useRef(null);
  const paymentCardRef = useRef(null);

  const [amount, setAmount] = useState('10.00');
  const [saveCard, setSaveCard] = useState(false);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');

  const [cardError, setCardError] = useState('');
  const [submitError, setSubmitError] = useState('');
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(null);

  // ── Initialise pay.js when the public key is available ───────────────────

  useEffect(() => {
    if (!publicApiKey || !cardContainerRef.current) return;

    // Avoid double-mounting in React strict mode
    if (payClientRef.current) return;

    // The script tag fires onload before the global is always readable —
    // poll briefly to ensure window.usaepay is fully defined.
    let attempts = 0;
    const init = () => {
      if (!window.usaepay) {
        if (++attempts < 20) { setTimeout(init, 50); return; }
        setCardError('pay.js failed to load (window.usaepay not defined after 1 s).');
        return;
      }
      try {
        const client = new window.usaepay.Client(publicApiKey);
        const card = client.createPaymentCardEntry();

        card.generateHTML(CARD_STYLE);
        card.addHTML('payjs-card-container');

        card.addEventListener('error', (msg) => {
          setCardError(msg);
        });

        payClientRef.current = client;
        paymentCardRef.current = card;
      } catch (err) {
        setCardError(`Failed to initialise pay.js: ${err.message}`);
      }
    };
    init();
  }, [publicApiKey]);

  // ── Form submit ───────────────────────────────────────────────────────────

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitError('');
    setSuccess(null);
    setCardError('');

    if (!payClientRef.current || !paymentCardRef.current) {
      setSubmitError('Card entry is not ready. Please wait and try again.');
      return;
    }

    if (Number(amount) <= 0) {
      setSubmitError('Amount must be greater than zero.');
      return;
    }

    if (saveCard) {
      if (!firstName.trim() || !lastName.trim()) {
        setSubmitError('First name and last name are required to save a card.');
        return;
      }
      if (!email.trim()) {
        setSubmitError('Email is required to save a card.');
        return;
      }
    }

    setLoading(true);

    try {
      // Tokenise the card – card data is sent directly to USAePay servers;
      // our backend only ever receives the single-use payment_key.
      const result = await payClientRef.current.getPaymentKey(paymentCardRef.current);

      if (result.error) {
        setCardError(result.error.message || 'Tokenisation failed');
        return;
      }

      const paymentKey = result; // result is the payment_key string

      if (saveCard) {
        // Save card to vault + enrol in recurring billing
        const data = await saveCustomer({
          paymentKey,
          firstName,
          lastName,
          email,
          phone,
          chargeAmount: Number(amount),
        });

        setSuccess({
          transactionKey: data.initialCharge?.transactionKey,
          authCode: data.initialCharge?.authCode,
          resultText: data.initialCharge?.resultText
            ?? `Card saved. Customer key: ${data.customer?.customerKey}`,
        });
      } else {
        // One-off charge
        const data = await chargeCard({ paymentKey, amount: Number(amount) });
        setSuccess(data);
      }
    } catch (err) {
      setSubmitError(err.message);
    } finally {
      setLoading(false);
    }
  };

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <form onSubmit={handleSubmit} style={styles.form} noValidate>
      <h2 style={styles.heading}>Payment</h2>

      {/* Card entry hosted by pay.js */}
      <div style={styles.field}>
        <label style={styles.label}>Card Details</label>
        <div
          id="payjs-card-container"
          ref={cardContainerRef}
          style={styles.cardContainer}
        />
        {cardError && <p style={styles.fieldError}>{cardError}</p>}
      </div>

      {/* Amount */}
      <div style={styles.field}>
        <label style={styles.label} htmlFor="amount">Amount (USD)</label>
        <input
          id="amount"
          type="number"
          min="0.01"
          step="0.01"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          style={styles.input}
          required
        />
      </div>

      {/* Save-card toggle */}
      <div style={styles.checkboxRow}>
        <input
          id="saveCard"
          type="checkbox"
          checked={saveCard}
          onChange={(e) => setSaveCard(e.target.checked)}
          style={styles.checkbox}
        />
        <label htmlFor="saveCard" style={styles.checkboxLabel}>
          Save card for recurring billing ($1 every 10 minutes)
        </label>
      </div>

      {/* Customer fields – shown only when saving */}
      {saveCard && (
        <fieldset style={styles.fieldset}>
          <legend style={styles.legend}>Customer Information</legend>

          <div style={styles.row}>
            <div style={{ ...styles.field, flex: 1 }}>
              <label style={styles.label} htmlFor="firstName">First Name *</label>
              <input
                id="firstName"
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
                style={styles.input}
                required
              />
            </div>
            <div style={{ ...styles.field, flex: 1 }}>
              <label style={styles.label} htmlFor="lastName">Last Name *</label>
              <input
                id="lastName"
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
                style={styles.input}
                required
              />
            </div>
          </div>

          <div style={styles.field}>
            <label style={styles.label} htmlFor="email">Email *</label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              style={styles.input}
              required
            />
          </div>

          <div style={styles.field}>
            <label style={styles.label} htmlFor="phone">Phone (optional)</label>
            <input
              id="phone"
              type="tel"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              style={styles.input}
            />
          </div>
        </fieldset>
      )}

      {/* Errors & success */}
      <ErrorBanner error={submitError} onDismiss={() => setSubmitError('')} />
      <SuccessBanner data={success} />

      <button
        type="submit"
        disabled={loading || !publicApiKey}
        style={{
          ...styles.button,
          opacity: loading || !publicApiKey ? 0.6 : 1,
          cursor: loading || !publicApiKey ? 'not-allowed' : 'pointer',
        }}
      >
        {loading
          ? 'Processing…'
          : saveCard
          ? `Save Card & Pay $${Number(amount).toFixed(2)}`
          : `Pay $${Number(amount).toFixed(2)}`}
      </button>

      {!publicApiKey && (
        <p style={styles.hint}>
          Configure <code>USAePay:PublicApiKey</code> in{' '}
          <code>appsettings.json</code> to enable payments.
        </p>
      )}

      <p style={styles.testCardsHint}>
        Use{' '}
        <a
          href="https://help.usaepay.info/developer/reference/testcards/"
          target="_blank"
          rel="noopener noreferrer"
          style={styles.testCardsLink}
        >
          USAePay test cards
        </a>
        {' '}(e.g. <code style={styles.cardCode}>4111111111111111</code> exp{' '}
        <code style={styles.cardCode}>0924</code> CVV <code style={styles.cardCode}>123</code>)
      </p>
    </form>
  );
}

// ── Styles ───────────────────────────────────────────────────────────────────

const styles = {
  form: {
    background: '#fff',
    borderRadius: 12,
    padding: 28,
    boxShadow: '0 4px 24px rgba(0,0,0,.08)',
    maxWidth: 520,
    width: '100%',
  },
  heading: {
    fontSize: 22,
    fontWeight: 700,
    marginBottom: 20,
    color: '#1a202c',
  },
  field: { marginBottom: 14 },
  row: { display: 'flex', gap: 12 },
  label: {
    display: 'block',
    fontSize: 13,
    fontWeight: 600,
    marginBottom: 5,
    color: '#4a5568',
  },
  input: {
    width: '100%',
    padding: '9px 12px',
    border: '1px solid #cbd5e0',
    borderRadius: 6,
    fontSize: 15,
    outline: 'none',
    color: '#1a202c',
    background: '#fff',
  },
  cardContainer: {
    border: '1px solid #cbd5e0',
    borderRadius: 6,
    padding: '9px 12px',
    minHeight: 40,
    background: '#fff',
  },
  fieldError: {
    color: '#c53030',
    fontSize: 13,
    marginTop: 4,
  },
  checkboxRow: {
    display: 'flex',
    alignItems: 'center',
    gap: 8,
    marginBottom: 16,
  },
  checkbox: { width: 16, height: 16, cursor: 'pointer', accentColor: '#4299e1' },
  checkboxLabel: { fontSize: 14, cursor: 'pointer', color: '#2d3748' },
  fieldset: {
    border: '1px solid #e2e8f0',
    borderRadius: 8,
    padding: '14px 16px',
    marginBottom: 16,
  },
  legend: {
    fontSize: 13,
    fontWeight: 600,
    color: '#4a5568',
    padding: '0 6px',
  },
  button: {
    width: '100%',
    padding: '12px 20px',
    background: '#4299e1',
    color: '#fff',
    border: 'none',
    borderRadius: 8,
    fontSize: 16,
    fontWeight: 600,
    transition: 'background .15s',
  },
  testCardsHint: {
    marginTop: 14,
    fontSize: 12,
    color: '#718096',
    textAlign: 'center',
    lineHeight: 1.6,
  },
  testCardsLink: {
    color: '#2b6cb0',
    textDecoration: 'none',
    fontWeight: 600,
  },
  cardCode: {
    fontFamily: 'monospace',
    fontSize: 11,
    background: '#edf2f7',
    padding: '1px 4px',
    borderRadius: 3,
    color: '#2d3748',
  },
  hint: {
    fontSize: 12,
    color: '#718096',
    marginTop: 10,
    textAlign: 'center',
  },
};
