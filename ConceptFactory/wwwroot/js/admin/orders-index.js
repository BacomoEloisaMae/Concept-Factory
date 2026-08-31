// ConceptFactory — Admin: Order Management (oIndex.cshtml)
// Loads the read-only Order Details modal via fetch, and intercepts
// "Cancel Order" inside it to first collect a required reason via the
// confirmation modal below, then submits via fetch so the admin stays on
// the same filtered/paged list afterward instead of the browser following
// the form's own redirect.

const orderModalBackdrop = document.getElementById('orderModalBackdrop');
const orderModal = document.getElementById('orderModal');

function setActiveRow(orderId) {
    document.querySelectorAll('.data-table tbody tr.active-row').forEach(r => r.classList.remove('active-row'));
    const row = document.getElementById('order-row-' + orderId);
    if (row) row.classList.add('active-row');
}

function loadOrderDetails(orderId) {
    orderModal.innerHTML = '<div class="billing-modal-loading"><i class="fa-solid fa-spinner fa-spin"></i> Loading order details…</div>';
    orderModalBackdrop.classList.add('open');
    document.body.style.overflow = 'hidden';

    fetch('/Orders/oDetailsPanel/' + orderId, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(res => {
            if (!res.ok) throw new Error('Failed to load order');
            return res.text();
        })
        .then(html => {
            orderModal.innerHTML = html;
            setActiveRow(orderId);
        })
        .catch(() => {
            orderModal.innerHTML = '<div class="billing-modal-loading"><i class="fa-solid fa-triangle-exclamation"></i> Could not load this order. Please try again.</div>';
        });
}

function closeOrderModal() {
    orderModalBackdrop.classList.remove('open');
    document.body.style.overflow = '';
}

// "Cancel Order" confirmation modal — requires a typed reason before the
// form is allowed to actually submit. Sits above the Order Details modal
// since it's triggered from a button inside it (and also from the row
// action button in oIndex.cshtml's table).
const cancelConfirmBackdrop = document.getElementById('cancelConfirmBackdrop');
let cancelPendingForm = null;

function closeCancelConfirm() {
    cancelConfirmBackdrop.classList.remove('open');
    cancelPendingForm = null;
}

function confirmCancelOrder() {
    if (!cancelPendingForm) return;
    const textarea = document.getElementById('cancelReasonInput');
    const errorEl = document.getElementById('cancelReasonError');
    const reason = textarea.value.trim();
    if (!reason) {
        errorEl.classList.add('show');
        textarea.focus();
        return;
    }
    errorEl.classList.remove('show');

    const form = cancelPendingForm;
    const hidden = form.querySelector('.cancel-reason-input');
    if (hidden) hidden.value = reason;
    closeCancelConfirm();

    const idInput = form.querySelector('input[name="id"]');
    const orderId = idInput ? idInput.value : null;
    const formData = new FormData(form);
    fetch(form.getAttribute('action'), {
        method: 'POST',
        body: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).finally(() => {
        const url = new URL(window.location.href);
        if (orderId) url.searchParams.set('open', orderId);
        window.location.href = url.toString();
    });
}

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && orderModalBackdrop.classList.contains('open')) closeOrderModal();
    if (e.key === 'Escape' && cancelConfirmBackdrop.classList.contains('open')) closeCancelConfirm();
});

// "Cancel Order" opens the reason confirmation above instead of submitting
// right away — whether it's clicked from the table row's action button or
// from inside the Order Details modal.
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (form.matches('[data-confirm-cancel]')) {
        e.preventDefault();
        cancelPendingForm = form;
        document.getElementById('cancelReasonInput').value = '';
        document.getElementById('cancelReasonError').classList.remove('show');
        cancelConfirmBackdrop.classList.add('open');
    }
});

// Re-open the modal for the relevant order after navigating back here with
// ?open=<id> (e.g. after cancelling from within it).
(function () {
    const params = new URLSearchParams(window.location.search);
    const openId = params.get('open');
    if (openId) loadOrderDetails(openId);
})();