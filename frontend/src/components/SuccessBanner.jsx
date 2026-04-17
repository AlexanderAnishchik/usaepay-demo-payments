export default function SuccessBanner({ data }) {
  if (!data) return null;

  return (
    <div style={styles.banner} role="status">
      <div style={styles.header}>✓ Payment Successful</div>
      {data.transactionKey && (
        <div style={styles.row}>
          <span style={styles.label}>Transaction Key</span>
          <code style={styles.value}>{data.transactionKey}</code>
        </div>
      )}
      {data.authCode && (
        <div style={styles.row}>
          <span style={styles.label}>Auth Code</span>
          <code style={styles.value}>{data.authCode}</code>
        </div>
      )}
      {data.resultText && (
        <div style={styles.row}>
          <span style={styles.label}>Result</span>
          <span style={styles.value}>{data.resultText}</span>
        </div>
      )}
    </div>
  );
}

const styles = {
  banner: {
    background: '#c6f6d5',
    border: '1px solid #276749',
    borderRadius: 8,
    padding: '14px 16px',
    marginBottom: 16,
    color: '#1a4731',
  },
  header: { fontWeight: 700, marginBottom: 8, fontSize: 15 },
  row: { display: 'flex', gap: 12, marginTop: 4, fontSize: 13 },
  label: { color: '#22543d', minWidth: 120, fontWeight: 600 },
  value: { fontFamily: 'monospace', color: '#1a4731' },
};
