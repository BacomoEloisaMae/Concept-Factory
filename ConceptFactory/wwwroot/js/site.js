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

// "Re-upload Payment Proof" — offered on the Track Order / Order Details
// page for Gcash orders rejected by Billing (see
// HomeController.hReuploadProof). Modal is built once and reused rather
// than living in the .cshtml partial, since that partial can render once
// per order in the Track Order accordion — duplicate static markup per
// row would collide on ids.
function cfOpenReuploadModal(orderId) {
    let backdrop = document.getElementById("cfReuploadModalBackdrop");
    if (!backdrop) {
        backdrop = document.createElement("div");
        backdrop.id = "cfReuploadModalBackdrop";
        backdrop.className = "cf-reupload-backdrop";
        backdrop.innerHTML =
            '<div class="cf-reupload-modal">' +
                '<h3>Re-upload Payment Proof</h3>' +
                '<p>Update your Gcash reference number and attach a clear screenshot of your payment, then resubmit for review.</p>' +
                '<label>Gcash Reference Number</label>' +
                '<input type="text" id="cfReuploadRef" maxlength="100" placeholder="e.g. 1234567890" />' +
                '<label>Payment Screenshot</label>' +
                '<input type="file" id="cfReuploadFile" accept="image/*" />' +
                '<div class="cf-reupload-actions">' +
                    '<button type="button" class="btn btn-outline" onclick="cfCloseReuploadModal()">Cancel</button>' +
                    '<button type="button" class="btn btn-primary" id="cfReuploadSubmitBtn" onclick="cfSubmitReupload()">Resubmit</button>' +
                '</div>' +
            '</div>';
        document.body.appendChild(backdrop);
        backdrop.addEventListener("click", function (e) {
            if (e.target === backdrop) cfCloseReuploadModal();
        });
    }
    backdrop.dataset.orderId = orderId;
    backdrop.querySelector("#cfReuploadRef").value = "";
    backdrop.querySelector("#cfReuploadFile").value = "";
    const btn = backdrop.querySelector("#cfReuploadSubmitBtn");
    btn.disabled = false;
    btn.textContent = "Resubmit";
    backdrop.classList.add("open");
}
window.cfOpenReuploadModal = cfOpenReuploadModal;

function cfCloseReuploadModal() {
    const backdrop = document.getElementById("cfReuploadModalBackdrop");
    if (backdrop) backdrop.classList.remove("open");
}
window.cfCloseReuploadModal = cfCloseReuploadModal;

function cfSubmitReupload() {
    const backdrop = document.getElementById("cfReuploadModalBackdrop");
    if (!backdrop) return;
    const orderId = backdrop.dataset.orderId;
    const refInput = backdrop.querySelector("#cfReuploadRef");
    const fileInput = backdrop.querySelector("#cfReuploadFile");
    const btn = backdrop.querySelector("#cfReuploadSubmitBtn");

    if (!refInput.value.trim()) { alert("Please enter your Gcash reference number."); return; }
    if (!fileInput.files || fileInput.files.length === 0) { alert("Please attach your payment screenshot."); return; }

    const tokenEl = document.querySelector("#cfAntiForgeryForm input[name='__RequestVerificationToken']");
    const formData = new FormData();
    formData.append("id", orderId);
    formData.append("referenceNumber", refInput.value.trim());
    formData.append("proofFile", fileInput.files[0]);
    if (tokenEl) formData.append("__RequestVerificationToken", tokenEl.value);

    btn.disabled = true;
    btn.textContent = "Submitting...";

    fetch("/Home/hReuploadProof", {
        method: "POST",
        body: formData,
        headers: { "X-Requested-With": "XMLHttpRequest" }
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                window.location.reload();
            } else {
                alert(data.message || "Could not re-submit your payment proof.");
                btn.disabled = false;
                btn.textContent = "Resubmit";
            }
        })
        .catch(() => {
            alert("Something went wrong. Please try again.");
            btn.disabled = false;
            btn.textContent = "Resubmit";
        });
}
window.cfSubmitReupload = cfSubmitReupload;

// Sidebar "Production" nav dropdown (_AdminLayout.cshtml) — toggles the
// submenu of production stations (Cutting, Printing, Sewing, etc.) open
// or closed. The group opens automatically on page load if one of its
// sub-items is the active page (see the "open" class rendered server-side).
function toggleNavGroup(btn) {
    const group = btn.closest('.nav-group');
    if (group) group.classList.toggle('open');
}
window.toggleNavGroup = toggleNavGroup;

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

// Production Overview table (pIndex.cshtml) — "Target Date" column is an
// inline <input type="date">, autosaved on change rather than needing a
// separate save button. Expects #cfAdminAntiForgeryForm (_AdminLayout.cshtml).
function cfSetProductionTargetDate(orderId, inputEl) {
    const tokenEl = document.querySelector("#cfAdminAntiForgeryForm input[name='__RequestVerificationToken']");
    const formData = new FormData();
    formData.append("id", orderId);
    formData.append("date", inputEl.value || "");
    if (tokenEl) formData.append("__RequestVerificationToken", tokenEl.value);

    inputEl.disabled = true;
    fetch("/Production/pSetTargetDate", {
        method: "POST",
        body: formData,
        keepalive: true, // <-- lets the save finish even if the user navigates away right after
        headers: { "X-Requested-With": "XMLHttpRequest" }
    })
        .then(res => res.json())
        .then(data => {
            if (!data.success) alert(data.message || "Couldn't save that date. Please try again.");
        })
        .catch(() => alert("Something went wrong. Please try again."))
        .finally(() => { inputEl.disabled = false; });
}
window.cfSetProductionTargetDate = cfSetProductionTargetDate;

// Admin sidebar "Notifications" unread badge (_AdminLayout.cshtml).
// #navAdminNotifBadge only exists in the admin layout, so this is a
// no-op on every customer-facing page even though site.js loads there
// too (the customer bell badge is a separate #navNotifBadge, handled
// by storefront.js, to avoid the two colliding on this shared file).
function cfRefreshAdminNotifBadge() {
    const badge = document.getElementById("navAdminNotifBadge");
    if (!badge) return;

    fetch("/Notifications/nUnreadCount", { headers: { "X-Requested-With": "XMLHttpRequest" } })
        .then(res => res.json())
        .then(data => {
            const count = data.count || 0;
            if (count > 0) {
                badge.textContent = count > 99 ? "99+" : String(count);
                badge.style.display = "inline-block";
            } else {
                badge.style.display = "none";
            }
        })
        .catch(() => { });
}
document.addEventListener('DOMContentLoaded', function () {
    cfRefreshAdminNotifBadge();
    // Viewing the Notifications page itself marks everything read
    // server-side, but the sidebar badge on THAT same page load was
    // rendered before that happened — refresh again shortly after so it
    // clears without needing a second navigation.
    setInterval(cfRefreshAdminNotifBadge, 30000);
});
