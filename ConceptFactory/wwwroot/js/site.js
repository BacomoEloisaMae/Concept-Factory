// ConceptFactory – site.js

// Account dropdown ("My Account" button in the navbar) — lives here
// instead of a page-specific script because this file is the only one
// loaded on every storefront page (see Views/Shared/_Layout.cshtml).
// Previously this was only defined inside cart-page.js/home-index.js/
// wishlist-page.js, so the button silently did nothing on any other
// page (Product Details, Billing, Customize, etc.).
function toggleAccountMenu() {
    const menu = document.getElementById("navAccountMenu");
    if (menu) menu.classList.toggle("open");
}
window.toggleAccountMenu = toggleAccountMenu;
document.addEventListener("click", function (e) {
    const wrap = document.getElementById("navAccountWrap");
    const menu = document.getElementById("navAccountMenu");
    if (wrap && menu && !wrap.contains(e.target)) {
        menu.classList.remove("open");
    }
});

// Customer-side "Cancel Order" button, used inside
// _CustomerOrderTrackingBody.cshtml (shared by hOrderDetails.cshtml and
// the Track Order popup). Lives in site.js rather than an inline
// <script> in the partial because the partial can be injected via
// fetch()+innerHTML, where <script> tags don't execute — the onclick=
// attribute on the button does, though, as long as this function is
// already defined on the page. Expects a hidden antiforgery form with
// id="cfAntiForgeryForm" on the page (see hTrackOrder.cshtml /
// hOrderDetails.cshtml).
function cfCancelMyOrder(orderId) {
    if (!confirm("Cancel order #ORD-" + String(orderId).padStart(5, "0") + "? This cannot be undone.")) return;

    const tokenEl = document.querySelector("#cfAntiForgeryForm input[name='__RequestVerificationToken']");
    const formData = new FormData();
    if (tokenEl) formData.append("__RequestVerificationToken", tokenEl.value);

    fetch("/Home/hCancelOrder/" + orderId, {
        method: "POST",
        body: formData,
        headers: { "X-Requested-With": "XMLHttpRequest" }
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                window.location.reload();
            } else {
                alert(data.message || "This order can no longer be cancelled.");
            }
        })
        .catch(() => alert("Something went wrong. Please try again."));
}
window.cfCancelMyOrder = cfCancelMyOrder;

// Auto-dismiss alerts after 4s
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.alert').forEach(function (alert) {
        setTimeout(function () {
            alert.style.opacity = '0';
            alert.style.transition = 'opacity .4s';
            setTimeout(function () { alert.remove(); }, 400);
        }, 4000);
    });
});
