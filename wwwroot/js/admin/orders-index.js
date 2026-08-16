// ConceptFactory — Admin: Order Management (oIndex.cshtml)
// Loads the read-only Order Details modal via fetch, and intercepts any
// [data-order-form] inside it (currently just "Cancel Order") so the admin
// stays on the same filtered/paged list afterward instead of the browser
// following the form's own redirect.

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

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && orderModalBackdrop.classList.contains('open')) closeOrderModal();
});

// Forms inside the modal (currently just "Cancel Order") submit via fetch
// so we control what happens next ourselves, instead of the browser
// navigating to wherever the form's own action would otherwise redirect.
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (form.matches('[data-order-form]')) {
        e.preventDefault();
        const confirmMsg = form.getAttribute('data-confirm');
        if (confirmMsg && !confirm(confirmMsg)) return;

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
});

// Re-open the modal for the relevant order after navigating back here with
// ?open=<id> (e.g. after cancelling from within it).
(function () {
    const params = new URLSearchParams(window.location.search);
    const openId = params.get('open');
    if (openId) loadOrderDetails(openId);
})();
