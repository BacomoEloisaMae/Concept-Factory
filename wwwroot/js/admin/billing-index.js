// ConceptFactory — Admin: Billing / Payment Verification (bIndex.cshtml)

const paymentModalBackdrop = document.getElementById('paymentModalBackdrop');
const paymentModal = document.getElementById('paymentModal');

function openPaymentModal(orderId) {
    paymentModal.innerHTML = '<div class="billing-modal-loading"><i class="fa-solid fa-spinner fa-spin"></i> Loading payment details…</div>';
    paymentModalBackdrop.classList.add('open');
    document.body.style.overflow = 'hidden';

    fetch('/Billing/bDetailsPanel/' + orderId, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(res => {
            if (!res.ok) throw new Error('Failed to load payment');
            return res.text();
        })
        .then(html => { paymentModal.innerHTML = html; })
        .catch(() => {
            paymentModal.innerHTML = '<div class="billing-modal-loading"><i class="fa-solid fa-triangle-exclamation"></i> Could not load this payment. Please try again.</div>';
        });
}

function closePaymentModal() {
    paymentModalBackdrop.classList.remove('open');
    document.body.style.overflow = '';
}

// "Mark Fully Paid" confirmation modal — the form is captured here instead
// of submitting right away; it only actually POSTs once the admin clicks
// "Yes, Mark Fully Paid" in the confirm dialog below.
const fpConfirmBackdrop = document.getElementById('fpConfirmBackdrop');
let fpPendingForm = null;

function closeFpConfirm() {
    fpConfirmBackdrop.classList.remove('open');
    fpPendingForm = null;
}

function confirmFullyPaid() {
    if (!fpPendingForm) return;
    const form = fpPendingForm;
    closeFpConfirm();
    const formData = new FormData(form);
    fetch(form.getAttribute('action'), {
        method: 'POST',
        body: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).finally(() => window.location.reload());
}

// Approve/Reject forms inside the modal submit via fetch, then the page
// reloads (preserving tab/search/filter) to refresh both the stat cards
// and the table row.
document.addEventListener('submit', function (e) {
    const form = e.target;

    if (form.matches('[data-confirm-fully-paid]')) {
        e.preventDefault();
        fpPendingForm = form;
        fpConfirmBackdrop.classList.add('open');
        return;
    }

    if (form.matches('[data-billing-form]')) {
        e.preventDefault();
        const formData = new FormData(form);
        fetch(form.getAttribute('action'), {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).finally(() => window.location.reload());
    }
});

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && fpConfirmBackdrop.classList.contains('open')) { closeFpConfirm(); return; }
    if (e.key === 'Escape' && paymentModalBackdrop.classList.contains('open')) closePaymentModal();
});

// Copies the (optional) note textarea into the hidden "note" input
// whichever action form (Approve/Reject) the admin submits — lives here
// (not in the AJAX-loaded partial) because <script> tags injected via
// innerHTML don't execute. Also copies the Cash "Amount Received" input
// into its form's hidden field and blocks submission if it's short of the
// required down payment.
function fillNote(form, orderId) {
    const textarea = document.getElementById('bvp-note-' + orderId);
    const hidden = form.querySelector('.bvp-note-input');
    if (textarea && hidden) hidden.value = textarea.value;

    const cashInput = document.getElementById('bvp-cash-received-' + orderId);
    const cashHidden = form.querySelector('.bvp-cash-received-input');
    if (cashInput && cashHidden) {
        const received = parseFloat(cashInput.value) || 0;
        const required = parseFloat(cashHidden.dataset.required) || 0;
        if (received < required) {
            alert('Amount received (\u20B1' + received.toFixed(2) + ') is less than the required down payment (\u20B1' + required.toFixed(2) + ').');
            return false;
        }
        cashHidden.value = received.toFixed(2);
    }
    return true;
}

// Live-updates the Change / Payment Applied / Payment Summary figures on
// the Cash payment verification panel as the admin types into the
// "Amount Received" field.
function updateCashPreview(input, orderId, required) {
    const received = parseFloat(input.value) || 0;
    const applied = Math.min(received, required);
    const change = Math.max(0, received - required);
    const fmt = n => '\u20B1' + n.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    const changeEl = document.getElementById('bvp-change-' + orderId);
    const appliedEl = document.getElementById('bvp-applied-' + orderId);
    const summaryReceivedEl = document.getElementById('bvp-summary-received-' + orderId);
    const summaryChangeEl = document.getElementById('bvp-summary-change-' + orderId);

    if (changeEl) changeEl.textContent = fmt(change);
    if (appliedEl) appliedEl.textContent = fmt(applied);
    if (summaryReceivedEl) summaryReceivedEl.textContent = fmt(received);
    if (summaryChangeEl) summaryChangeEl.textContent = fmt(change);
}
