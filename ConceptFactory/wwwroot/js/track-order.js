// ConceptFactory — Storefront: My Orders / Track Order (hTrackOrder.cshtml)

const myOrderModalBackdrop = document.getElementById('myOrderModalBackdrop');
const myOrderModal = document.getElementById('myOrderModal');

function loadMyOrderDetails(orderId) {
    myOrderModal.innerHTML = '<div class="cot-modal-loading"><i class="fa-solid fa-spinner fa-spin"></i> Loading order details…</div>';
    myOrderModalBackdrop.classList.add('open');
    document.body.style.overflow = 'hidden';

    fetch('/Home/hOrderDetailsPanel/' + orderId, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(res => {
            if (!res.ok) throw new Error('Failed to load order');
            return res.text();
        })
        .then(html => {
            myOrderModal.innerHTML =
                '<button type="button" class="cot-modal-close" onclick="closeMyOrderModal()"><i class="fa-solid fa-xmark"></i></button>'
                + html;
        })
        .catch(() => {
            myOrderModal.innerHTML = '<div class="cot-modal-loading"><i class="fa-solid fa-triangle-exclamation"></i> Could not load this order. Please try again.</div>';
        });
}

function closeMyOrderModal() {
    myOrderModalBackdrop.classList.remove('open');
    document.body.style.overflow = '';
}

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && myOrderModalBackdrop.classList.contains('open')) closeMyOrderModal();
});
