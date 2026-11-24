export default function ErrorBanner({ error, onDismiss }) {
  if (!error) return null;

  return (
    <div style={styles.banner} role="alert">
      <span style={styles.icon} aria-hidden="true">⚠</span>
      <span style={styles.text}>{error}</span>
      {onDismiss && (
        <button type="button" style={styles.close} onClick={onDismiss} aria-label="Dismiss error">
          ✕
        </button>
      )}
    </div>
  );
}

const styles = {
  banner: {
    display: 'flex',
    alignItems: 'center',
    gap: 10,
    background: '#fff5f5',
    border: '1px solid #fc8181',
    borderRadius: 8,
    padding: '12px 16px',
    marginBottom: 16,
    color: '#c53030',
  },
  icon: { fontSize: 18, flexShrink: 0 },
  text: { flex: 1, fontSize: 14, lineHeight: 1.5 },
  close: {
    background: 'none',
    border: 'none',
    cursor: 'pointer',
    color: '#c53030',
    fontSize: 16,
    padding: 4,
    lineHeight: 1,
  },
};
