// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import PaymentForm from './PaymentForm';

const approvedWarning = 'Initial payment approved. Recurring billing was not set up. Do not submit this payment again.';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

async function submitSaveCard(response) {
  const getPaymentKey = vi.fn().mockResolvedValue('fixture-single-use-key');
  class PayJsClient {
    getPaymentKey = getPaymentKey;

    createPaymentCardEntry() {
      return { generateHTML() {}, addHTML() {}, addEventListener() {} };
    }
  }

  vi.stubGlobal('usaepay', { Client: PayJsClient });
  const fetch = vi.fn().mockResolvedValue(Response.json(response, { status: 422 }));
  vi.stubGlobal('fetch', fetch);

  render(<PaymentForm publicApiKey="fixture-public-key" />);
  fireEvent.click(screen.getByRole('checkbox', { name: /save card/i }));
  fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'Test' } });
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Customer' } });
  fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'test@example.com' } });
  fireEvent.click(screen.getByRole('button', { name: /save card & pay/i }));

  const alert = await screen.findByRole('alert');
  expect(fetch).toHaveBeenCalledWith('/api/payment/customers', expect.objectContaining({ method: 'POST' }));
  return { alert, fetch, getPaymentKey };
}

it('shows the approved receipt and dismisses it without submitting again', async () => {
  const { alert, fetch, getPaymentKey } = await submitSaveCard({
    error: 'Saving the payment method failed.',
    initialCharge: { success: true, transactionKey: 'fixture-receipt' },
  });
  expect(alert.textContent).toContain(approvedWarning);
  expect(alert.textContent).toContain('Transaction reference: fixture-receipt');

  const dismiss = screen.getByRole('button', { name: 'Dismiss error' });
  expect(dismiss.type).toBe('button');
  await act(async () => dismiss.click());
  expect(screen.queryByRole('alert')).toBeNull();
  expect(getPaymentKey).toHaveBeenCalledTimes(1);
  expect(fetch).toHaveBeenCalledTimes(1);
});

it('shows the warning even if the approved charge lacks a reference', async () => {
  const { alert } = await submitSaveCard({ error: 'Saving failed.', initialCharge: { success: true } });
  expect(alert.textContent).toContain(approvedWarning);
  expect(alert.textContent).not.toContain('Transaction reference');
});

it('displays a decline without claiming the initial charge was approved', async () => {
  const { alert } = await submitSaveCard({ error: 'Declined' });
  expect(alert.textContent).toContain('Declined');
  expect(alert.textContent).not.toContain(approvedWarning);
});
