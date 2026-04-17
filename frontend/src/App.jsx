import { useEffect, useState } from 'react';
import PaymentForm from './components/PaymentForm';
import RecurringStatus from './components/RecurringStatus';
import ErrorBanner from './components/ErrorBanner';
import { fetchConfig } from './services/api';

const TABS = ['Payment', 'Recurring'];

function loadScript(src) {
  return new Promise((resolve, reject) => {
    if (document.querySelector(`script[src="${src}"]`)) {
      resolve(); // already loaded
      return;
    }
    const s = document.createElement('script');
    s.src = src;
    s.onload = resolve;
    s.onerror = () => reject(new Error(`Failed to load script: ${src}`));
    document.head.appendChild(s);
  });
}

export default function App() {
  const [activeTab, setActiveTab] = useState('Payment');
  const [publicApiKey, setPublicApiKey] = useState('');
  const [payJsReady, setPayJsReady] = useState(false);
  const [configError, setConfigError] = useState('');

  useEffect(() => {
    fetchConfig()
      .then(async (d) => {
        await loadScript(d.payJsUrl);
        setPublicApiKey(d.publicApiKey);
        setPayJsReady(true);
      })
      .catch((err) => setConfigError(`Backend unavailable: ${err.message}`));
  }, []);

  return (
    <div style={styles.page}>
      <header style={styles.header}>
        <div style={styles.logo}>
          <span style={styles.logoIcon}>💳</span>
          <span style={styles.logoText}>USAePay Portal</span>
        </div>
        <span style={styles.env}>Sandbox</span>
      </header>

      <main style={styles.main}>
        {configError && <ErrorBanner error={configError} />}

        {/* Tab bar */}
        <div style={styles.tabs}>
          {TABS.map((tab) => (
            <button
              key={tab}
              style={{
                ...styles.tab,
                ...(activeTab === tab ? styles.tabActive : {}),
              }}
              onClick={() => setActiveTab(tab)}
            >
              {tab}
            </button>
          ))}
        </div>

        {/* Tab content */}
        <div style={styles.tabContent}>
          {activeTab === 'Payment' && (
            <PaymentForm publicApiKey={payJsReady ? publicApiKey : ''} />
          )}
          {activeTab === 'Recurring' && <RecurringStatus />}
        </div>
      </main>
    </div>
  );
}

// ── Styles ────────────────────────────────────────────────────────────────────

const styles = {
  page: { display: 'flex', flexDirection: 'column', minHeight: '100vh', background: '#f0f4f8' },
  header: {
    background: '#1a202c',
    color: '#fff',
    padding: '14px 32px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  logo: { display: 'flex', alignItems: 'center', gap: 10 },
  logoIcon: { fontSize: 24 },
  logoText: { fontSize: 20, fontWeight: 700, letterSpacing: '-0.3px' },
  env: {
    background: '#f6ad55',
    color: '#744210',
    fontSize: 12,
    fontWeight: 700,
    padding: '3px 10px',
    borderRadius: 20,
    letterSpacing: '0.5px',
  },
  main: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    padding: '40px 20px',
    gap: 0,
  },
  tabs: {
    display: 'flex',
    gap: 2,
    background: '#e2e8f0',
    borderRadius: 8,
    padding: 4,
    marginBottom: 24,
  },
  tab: {
    padding: '8px 20px',
    border: 'none',
    borderRadius: 6,
    fontSize: 14,
    fontWeight: 500,
    cursor: 'pointer',
    background: 'transparent',
    color: '#4a5568',
    transition: 'all .15s',
  },
  tabActive: {
    background: '#fff',
    color: '#1a202c',
    fontWeight: 700,
    boxShadow: '0 1px 4px rgba(0,0,0,.12)',
  },
  tabContent: { width: '100%', display: 'flex', justifyContent: 'center' },
};
