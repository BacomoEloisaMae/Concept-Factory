// ConceptFactory — Billing & Payment page
//
// Connected to:
//   View       : Views/Home/hBilling.cshtml
//   Controller : Controllers/HomeController.cs -> hBilling() / SubmitOrder()
//   Styles     : wwwroot/css/home/billing-payment.css
//   Depends on : storefront.js (window.CF), window.CF_BILLING_PAGE (page
//                data: billingName/Email/Phone, submitUrl, cartUrl, homeUrl)
//
// The page is a small 2-step wizard, both steps rendered up front and
// toggled with plain show/hide (no server round-trip between them):
//   Step 1 (#bpStepSummary) — Order Summary + Billing Information
//   Step 2 (#bpStepPayment) — Select Payment Method (Gcash / Cash)
(function () {
    "use strict";

    function prettyLocation(slug) {
        return String(slug).split("-").map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(" ");
    }

    // If the shopper arrived here via a product/customize page's "Check
    // Out" button (skipping the Cart page), this returns that page's URL
    // so "Back" sends them there instead of to the Cart. Falls back to
    // the Cart page for the normal multi-item checkout flow.
    function resolveBackTarget() {
        let backUrl = null;
        try { backUrl = sessionStorage.getItem("cf_checkout_back_url"); } catch (e) { /* ignore */ }
        const cartUrl = (window.CF_BILLING_PAGE || {}).cartUrl || "/Home/hCart";
        return backUrl
            ? { url: backUrl, label: "Back to Item" }
            : { url: cartUrl, label: "Back to Cart" };
    }

    function wireBackLink() {
        const backBtn = document.getElementById("bpBackBtn");
        if (!backBtn) return;
        const target = resolveBackTarget();
        backBtn.href = target.url;
        backBtn.textContent = target.label;
    }

    function currentSubtotal() {
        const lines = CF.getSelectedCartItems();
        return lines.reduce((s, c) => s + (Number(c.price) + Number(c.servicePrice || 0)) * (c.qty || 1), 0);
    }
    function currentDownPayment() {
        return Math.round(currentSubtotal() * 0.5 * 100) / 100;
    }

    // ── "Amount you paid" boxes (Gcash & Cash) ───────────────────────────
    // Customers aren't locked to exactly 50% anymore — they can enter any
    // amount at or above the 50% minimum (e.g. 70%). This live-computes and
    // shows what percentage of the total that amount represents.
    function bpAmountInputId(method) {
        return method === "gcash" ? "bpAmountPaidGcash" : "bpAmountPaidCash";
    }
    function bpBreakdownBoxId(method) {
        return method === "gcash" ? "bpLiveBreakdownGcash" : "bpLiveBreakdownCash";
    }
    function bpPercentRowId(method) {
        return method === "gcash" ? "bpPercentRowGcash" : "bpPercentRowCash";
    }
    function bpPercentLabelId(method) {
        return method === "gcash" ? "bpPercentGcash" : "bpPercentCash";
    }
    function bpRemainingLabelId(method) {
        return method === "gcash" ? "bpRemainingLiveGcash" : "bpRemainingLiveCash";
    }

    // Recomputes and shows the live "Percentage of total / Remaining
    // balance" box under the Amount box as the shopper types — e.g.
    // typing 210 on a ₱300 order shows "70.0% of total" and "₱90.00
    // remaining", updated on every keystroke.
    function bpOnAmountInput(method) {
        const input = document.getElementById(bpAmountInputId(method));
        const box = document.getElementById(bpBreakdownBoxId(method));
        const percentRow = document.getElementById(bpPercentRowId(method));
        const percentEl = document.getElementById(bpPercentLabelId(method));
        const remainingEl = document.getElementById(bpRemainingLabelId(method));
        if (!input || !box || !percentEl || !remainingEl) return;

        const subtotal = currentSubtotal();
        const amount = parseFloat(input.value);

        if (!subtotal || isNaN(amount) || amount <= 0) {
            box.style.display = "none";
            return;
        }

        const pct = (amount / subtotal) * 100;
        const remaining = Math.max(subtotal - amount, 0);
        const minRequired = currentDownPayment();

        percentEl.textContent = `${pct.toFixed(1)}%`;
        remainingEl.textContent = CF.peso(remaining);
        if (percentRow) percentRow.classList.toggle("bp-live-warning", amount < minRequired);
        // Explicit "block" (not "") so this reliably shows even though the
        // element also carries the static .js-hidden class in its initial
        // HTML — an empty string here would just fall through to that
        // class's display:none instead of actually revealing the box.
        box.style.display = "block";
    }

    // Reads + validates the amount typed into the given method's box.
    // Returns the numeric amount, or null (after showing a toast) if it's
    // missing or below the 50% minimum.
    function bpValidateAmountPaid(method) {
        const input = document.getElementById(bpAmountInputId(method));
        const amount = input ? parseFloat(input.value) : NaN;

        if (!input || isNaN(amount) || amount <= 0) {
            CF.toast("Please enter the amount you paid", { icon: "fa-triangle-exclamation", type: "info" });
            return null;
        }

        const minRequired = currentDownPayment();
        if (amount < minRequired) {
            CF.toast(`Amount paid must be at least ${CF.peso(minRequired)} (50% of the total)`, { icon: "fa-triangle-exclamation", type: "info" });
            return null;
        }

        return Math.round(amount * 100) / 100;
    }

    // ── Step navigation ──────────────────────────────────────────────────
    function bpGoToPaymentStep() {
        const lines = CF.getSelectedCartItems();
        if (lines.length === 0) {
            CF.toast("Select at least one item in your cart first", { icon: "fa-circle-info", type: "info" });
            return;
        }
        const downPayment = currentDownPayment();
        document.getElementById("bpAmountToPayGcash").textContent = CF.peso(downPayment);
        document.getElementById("bpAmountToPayCash").textContent = CF.peso(downPayment);

        // Pre-fill the "amount you paid" boxes with the 50% minimum, but
        // only if the shopper hasn't already typed something in (e.g. they
        // went Back and returned to this step) — never stomp on a value
        // they've entered.
        const gcashInput = document.getElementById("bpAmountPaidGcash");
        const cashInput = document.getElementById("bpAmountPaidCash");
        if (gcashInput && !gcashInput.value) gcashInput.value = downPayment.toFixed(2);
        if (cashInput && !cashInput.value) cashInput.value = downPayment.toFixed(2);
        bpOnAmountInput("gcash");
        bpOnAmountInput("cash");

        document.getElementById("bpStepSummary").style.display = "none";
        // Explicit "flex" (not "") — bpStepPayment's own .bp-wizard-col
        // class sets display:flex, but the element also starts with a
        // static .js-hidden class in its HTML, so an empty string would
        // just fall through to that class's display:none.
        document.getElementById("bpStepPayment").style.display = "flex";
        window.scrollTo({ top: 0, behavior: "smooth" });
    }

    function bpGoToSummaryStep() {
        document.getElementById("bpStepPayment").style.display = "none";
        document.getElementById("bpStepSummary").style.display = "";
        window.scrollTo({ top: 0, behavior: "smooth" });
    }

    // ── Payment method switching (presentational) ───────────────────────
    function bpSelectMethod(method) {
        document.querySelectorAll(".bp-method").forEach(function (el) {
            el.classList.toggle("active", el.dataset.method === method);
        });

        const isGcash = method === "gcash";
        document.getElementById("bpPanelGcash").style.display = isGcash ? "" : "none";
        // Explicit "block" (not "") for the same reason as bpStepPayment
        // above — bpPanelCash starts with a static .js-hidden class.
        document.getElementById("bpPanelCash").style.display = isGcash ? "none" : "block";
    }

    function bpCopyGcashNumber(btn) {
        const number = "09512388661";
        const original = btn.textContent;
        const done = () => { btn.textContent = "Copied!"; setTimeout(() => { btn.textContent = original; }, 1500); };

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(number).then(done).catch(done);
        } else {
            done();
        }
    }

    function bpShowFileName(input, labelId) {
        const label = document.getElementById(labelId);
        if (!label) return;
        label.textContent = (input.files && input.files.length > 0) ? input.files[0].name : "";
    }

    // ── Order summary — built from whichever cart lines were checked on
    //    the Cart page (CF.getSelectedCartItems()) ───────────────────────
    function renderOrderSummary() {
        const lines = CF.getSelectedCartItems();
        const itemsHost = document.getElementById("bpSummaryItems");
        const toPaymentBtn = document.getElementById("bpToPaymentBtn");

        if (lines.length === 0) {
            const target = resolveBackTarget();
            const linkText = target.label === "Back to Item" ? "go back" : "go back to your cart";
            itemsHost.innerHTML = `<div class="bp-summary-empty">No items selected. <a href="${target.url}">${linkText}</a> and select at least one item.</div>`;
            document.getElementById("bpSubtotal").textContent = CF.peso(0);
            document.getElementById("bpDownPayment").textContent = CF.peso(0);
            document.getElementById("bpRemaining").textContent = CF.peso(0);
            document.getElementById("bpTotal").textContent = CF.peso(0);
            if (toPaymentBtn) toPaymentBtn.disabled = true;
            return;
        }

        if (toPaymentBtn) toPaymentBtn.disabled = false;

        // Each item card carries its own Product Price / Printing fee
        // breakdown and its own line total, since different items in the
        // same order can have different services, quantities, and unit
        // prices — a single combined "Printing / Services" row can't
        // represent that honestly once more than one item is selected.
        itemsHost.innerHTML = lines.map(c => {
            const opts = [
                c.size ? `Size: <span>${CF.escapeHtml(c.size)}</span>` : "",
                c.color ? `Color: <span>${CF.escapeHtml(CF.colorName(c.color))}</span>` : "",
                c.service ? `Printing: <span>${CF.escapeHtml(c.service)}</span>` : "",
                c.printLocation ? `Print Location: <span>${CF.escapeHtml(prettyLocation(c.printLocation))}</span>` : ""
            ].filter(Boolean).map(row => `<div class="bp-summary-item-row">${row}</div>`).join("");

            const qty = c.qty || 1;
            const unitPrice = Number(c.price) || 0;
            const unitService = Number(c.servicePrice) || 0;
            const itemProductTotal = unitPrice * qty;
            const itemServiceTotal = unitService * qty;
            const itemLineTotal = itemProductTotal + itemServiceTotal;

            return `
            <div class="bp-summary-item">
                <div class="bp-summary-item-top">
                    <div class="bp-summary-thumb">
                        ${c.image ? `<img src="${c.image}" alt="${CF.escapeHtml(c.name)}" onerror="this.style.display='none'" />` : `<i class="fa-solid fa-shirt"></i>`}
                    </div>
                    <div class="bp-summary-item-info">
                        <div class="bp-summary-item-header">
                            <div class="bp-summary-item-name">${CF.escapeHtml(c.name)}</div>
                        </div>
                        ${opts}
                        <div class="bp-summary-item-row">Quantity: <span>${qty}</span></div>
                        <div class="bp-summary-price">
                           <div class="bp-price-row">
                            <div class="bp-price-row-label">
                                <span>Product Price</span>
                                <span class="bp-price-row-sub">(${qty} x ${CF.peso(unitPrice)})</span>
                            </div>
                            <span>${CF.peso(itemProductTotal)}</span>
                            </div>
                            ${unitService > 0 ? `
                            <div class="bp-price-row">
                                <div class="bp-price-row-label">
                                    <span>${CF.escapeHtml(c.service || "Printing fee")}</span>
                                    <span class="bp-price-row-sub">(${qty} x ${CF.peso(unitService)})</span>
                                </div>
                                <span>${CF.peso(itemServiceTotal)}</span>
                            </div>` : ""}
                        </div>
                    </div>
                </div>
            </div>`;
        }).join("");

        const productTotal = lines.reduce((s, c) => s + Number(c.price) * (c.qty || 1), 0);
        const serviceTotal = lines.reduce((s, c) => s + Number(c.servicePrice || 0) * (c.qty || 1), 0);
        const subtotal = productTotal + serviceTotal;
        const downPayment = Math.round(subtotal * 0.5 * 100) / 100;
        const remaining = subtotal - downPayment;

        document.getElementById("bpSubtotal").textContent = CF.peso(subtotal);
        document.getElementById("bpDownPayment").textContent = CF.peso(downPayment);
        document.getElementById("bpRemaining").textContent = CF.peso(remaining);
        document.getElementById("bpTotal").textContent = CF.peso(subtotal);
    }

    // ── Submit ────────────────────────────────────────────────────────
    function getAntiForgeryToken() {
        const el = document.querySelector('#bpAntiForgeryForm input[name="__RequestVerificationToken"]');
        return el ? el.value : "";
    }

    function selectedMethod() {
        const methodInput = document.querySelector('input[name="paymentMethod"]:checked');
        return methodInput ? methodInput.value : "gcash";
    }

    // Entry point for the "Submit Payment" button on Step 2. Gcash goes
    // straight to bpSubmitOrder() (after its own validation there). Cash
    // needs an extra confirmation step first — there's no reference number
    // to validate, so this modal is the moment the customer explicitly
    // commits to visiting the shop and paying the down payment.
    function bpConfirmOrderClick() {
        const lines = CF.getSelectedCartItems();
        if (lines.length === 0) {
            CF.toast("Select at least one item in your cart first", { icon: "fa-circle-info", type: "info" });
            return;
        }

        if (selectedMethod() === "cash") {
            const amount = bpValidateAmountPaid("cash");
            if (amount === null) return;

            const subtotal = currentSubtotal();
            const pct = subtotal ? (amount / subtotal) * 100 : 0;
            document.getElementById("bpCashModalAmount").textContent = CF.peso(amount);
            document.getElementById("bpCashModalPercent").textContent = `(${pct.toFixed(1)}% of Total Order)`;
            document.getElementById("bpCashUnderstandCheck").checked = false;
            document.getElementById("bpCashConfirmBtn").disabled = true;
            document.getElementById("bpCashModalOverlay").style.display = "flex";
            return;
        }

        bpSubmitOrder();
    }

    function bpCloseCashModal() {
        document.getElementById("bpCashModalOverlay").style.display = "none";
    }

    function bpConfirmCashOrder() {
        bpCloseCashModal();
        bpSubmitOrder();
    }

    function bpSubmitOrder() {
        const lines = CF.getSelectedCartItems();
        if (lines.length === 0) {
            CF.toast("Select at least one item in your cart first", { icon: "fa-circle-info", type: "info" });
            return;
        }

        const isGcash = selectedMethod() === "gcash";
        const referenceNumber = (document.getElementById(isGcash ? "bpRefInputGcash" : "bpRefInputCash") || {}).value || "";

        if (isGcash && !referenceNumber.trim()) {
            CF.toast("Please enter your Gcash reference number", { icon: "fa-triangle-exclamation", type: "info" });
            return;
        }

        const amountPaid = bpValidateAmountPaid(isGcash ? "gcash" : "cash");
        if (amountPaid === null) return;

        const pageData = window.CF_BILLING_PAGE || {};
        const fileInput = isGcash ? document.getElementById("bpUploadInputGcash") : null;

        const formData = new FormData();
        formData.append("__RequestVerificationToken", getAntiForgeryToken());
        formData.append("fullName", pageData.billingName || "");
        formData.append("email", pageData.billingEmail || "");
        formData.append("phone", pageData.billingPhone || "");
        formData.append("address", pageData.billingAddress || "");
        formData.append("paymentMethod", isGcash ? "Gcash" : "Cash");
        formData.append("referenceNumber", referenceNumber);
        formData.append("cartLinesJson", JSON.stringify(lines));
        formData.append("amountPaid", String(amountPaid));
        if (fileInput && fileInput.files && fileInput.files[0]) {
            formData.append("proofFile", fileInput.files[0]);
        }

        const submitBtn = document.getElementById("bpSubmitBtn");
        const originalText = submitBtn.textContent;
        submitBtn.disabled = true;
        submitBtn.textContent = "Submitting…";

        fetch(pageData.submitUrl || "/Home/SubmitOrder", { method: "POST", body: formData })
            .then(r => r.text().then(text => ({ status: r.status, ok: r.ok, text })))
            .then(({ status, ok, text }) => {
                let data;
                try {
                    data = JSON.parse(text);
                } catch (e) {
                    // The server responded, but not with JSON — usually means
                    // a server-side error page (or a login redirect) came
                    // back instead of the expected { success, ... } payload.
                    console.error("SubmitOrder returned non-JSON response", { status, text });
                    CF.toast(`Server error (${status}). Please try again or contact us.`, { icon: "fa-triangle-exclamation", type: "info" });
                    submitBtn.disabled = false;
                    submitBtn.textContent = originalText;
                    return;
                }

                if (!ok || !data.success) {
                    CF.toast(data.message || "Something went wrong. Please try again.", { icon: "fa-triangle-exclamation", type: "info" });
                    submitBtn.disabled = false;
                    submitBtn.textContent = originalText;
                    return;
                }
                // Order placed — remove only the ordered lines from the cart.
                // Percentage computed from THIS order's lines before the
                // cart is mutated below — currentSubtotal() would read the
                // post-removal (possibly emptied) cart otherwise.
                const orderSubtotal = lines.reduce((s, c) => s + (Number(c.price) + Number(c.servicePrice || 0)) * (c.qty || 1), 0);
                const amountPaidPct = orderSubtotal ? (amountPaid / orderSubtotal) * 100 : 0;

                CF.removeLines(lines.map(l => l.lineId));
                try { sessionStorage.removeItem("cf_checkout_back_url"); } catch (e) { /* ignore */ }
                renderConfirmation(data.orderId, isGcash, amountPaid, amountPaidPct);
            })
            .catch((err) => {
                // A TRUE network failure (offline, DNS, server unreachable) —
                // this is the only case that should say "could not reach".
                console.error("SubmitOrder network error", err);
                CF.toast("Could not reach the server. Please check your connection and try again.", { icon: "fa-triangle-exclamation", type: "info" });
                submitBtn.disabled = false;
                submitBtn.textContent = originalText;
            });
    }

    function renderConfirmation(orderId, isGcash, amountPaid, amountPaidPct) {
        document.getElementById("bpFormWrap").style.display = "none";

        const year = new Date().getFullYear();
        document.getElementById("bpConfirmOrderId").textContent = "#ORD-" + String(orderId).padStart(3, "0");
        document.getElementById("bpConfirmMethod").textContent = isGcash ? "GCash" : "Cash";
        document.getElementById("bpConfirmAmountPaid").textContent =
            CF.peso(amountPaid) + (amountPaidPct ? ` (${amountPaidPct.toFixed(1)}% of total)` : "");

        const badge = document.getElementById("bpConfirmMethodBadge");
        badge.className = "bp-confirm-method-badge " + (isGcash ? "bp-confirm-method-badge-gcash" : "bp-confirm-method-badge-cash");
        badge.innerHTML = isGcash ? "G" : '<i class="fa-solid fa-money-bill-wave"></i>';

        const pill = document.getElementById("bpConfirmStatusPill");
        pill.innerHTML = isGcash
            ? '<i class="fa-solid fa-hourglass-half"></i> Waiting for Verification'
            : '<i class="fa-solid fa-hourglass-half"></i> Waiting for 50% Down Payment';

        document.getElementById("bpConfirmNextGcash").style.display = isGcash ? "" : "none";
        // Explicit "block" (not "") — bpConfirmNextCash starts with a
        // static .js-hidden class, same reasoning as above.
        document.getElementById("bpConfirmNextCash").style.display = isGcash ? "none" : "block";

        document.getElementById("bpConfirmSecureNote").textContent = isGcash
            ? "Your payment is secure with GCash."
            : "Your order details are safely recorded — pay in person at the shop.";

        document.getElementById("bpConfirmWrap").style.display = "flex";
    }

    // Drag & drop visual feedback only — the drop still just populates
    // the same hidden file input.
    document.addEventListener("DOMContentLoaded", function () {
        renderOrderSummary();
        wireBackLink();

        const box = document.getElementById("bpUploadBoxGcash");
        const input = document.getElementById("bpUploadInputGcash");
        if (!box || !input) return;

        ["dragenter", "dragover"].forEach(evt =>
            box.addEventListener(evt, e => { e.preventDefault(); box.classList.add("bp-dragover"); }));
        ["dragleave", "drop"].forEach(evt =>
            box.addEventListener(evt, e => { e.preventDefault(); box.classList.remove("bp-dragover"); }));

        box.addEventListener("drop", function (e) {
            if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                input.files = e.dataTransfer.files;
                bpShowFileName(input, "bpUploadFileNameGcash");
            }
        });
    });

    window.bpGoToPaymentStep = bpGoToPaymentStep;
    window.bpGoToSummaryStep = bpGoToSummaryStep;
    window.bpOnAmountInput = bpOnAmountInput;
    window.bpSelectMethod = bpSelectMethod;
    window.bpCopyGcashNumber = bpCopyGcashNumber;
    window.bpShowFileName = bpShowFileName;
    window.bpConfirmOrderClick = bpConfirmOrderClick;
    window.bpCloseCashModal = bpCloseCashModal;
    window.bpConfirmCashOrder = bpConfirmCashOrder;
    window.bpSubmitOrder = bpSubmitOrder;
})();
