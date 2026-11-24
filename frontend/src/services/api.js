const BASE = '/api/payment';

async function request(path, options = {}) {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json', ...options.headers },
    ...options,
  });

  const text = await res.text();
  let data;
  try { data = JSON.parse(text); } catch { data = { error: text }; }

  if (!res.ok) {
    const message =
      data?.error ||
      data?.title ||
      `HTTP ${res.status}: ${res.statusText}`;
    const error = new Error(message);
    if (res.status === 422 && data?.initialCharge?.success === true) {
      error.initialCharge = data.initialCharge;
    }
    throw error;
  }

  return data;
}

export async function fetchConfig() {
  return request('/config');
}

export async function chargeCard({ paymentKey, amount, description }) {
  return request('/charge', {
    method: 'POST',
    body: JSON.stringify({ paymentKey, amount: Number(amount), description }),
  });
}

export async function saveCustomer({ paymentKey, firstName, lastName, email, phone, chargeAmount }) {
  return request('/customers', {
    method: 'POST',
    body: JSON.stringify({ paymentKey, firstName, lastName, email, phone, chargeAmount }),
  });
}

export async function fetchRecurring() {
  return request('/recurring');
}

export async function forceRecurring() {
  return request('/recurring/run', { method: 'POST' });
}

export async function deleteCustomer(customerKey) {
  return request(`/customers/${customerKey}`, { method: 'DELETE' });
}
