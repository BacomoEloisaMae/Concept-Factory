/* ============================================================
   CART-PAGE.JS
   Page logic for Views/Home/hCart.cshtml
   Depends on: storefront.js (window.CF), window.CF_CART_PAGE (page data:
   homeUrl, detailsUrl, customizeUrl, customizeIndexUrl)
============================================================ */
(function () {
    "use strict";

    const SELECTED_KEY = "cf_cart_selected";

    // ── Selection state (which lines are checked for checkout) ──────────
    function readSelected() {
        try {
            const raw = localStorage.getItem(SELECTED_KEY);
            return raw ? JSON.parse(raw) : null; // null = "not set yet"
        } catch (e) {
            return null;
        }
    }
    function writeSelected(ids) {
        try { localStorage.setItem(SELECTED_KEY, JSON.stringify(ids)); } catch (e) { /* ignore */ }
    }

    // Returns the current set of selected lineIds, pruning any that no
    // longer exist in the cart. New/unseen lines start unchecked — only
    // the user ticking the box adds them to the selection.
    // Print locations are stored as slugs (e.g. "left-chest", "front-back")
    // — this turns them into a friendlier label for display: "Left Chest".
    function prettyLocation(slug) {
        return String(slug).split("-").map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(" ");
    }

    function getSelectedIds(cart) {
        const cartIds = cart.map(c => c.lineId);
        let stored = readSelected();
        if (stored === null) {
            stored = [];
        } else {
            stored = stored.filter(id => cartIds.includes(id)); // drop stale ids
        }
        writeSelected(stored);
        return new Set(stored);
    }

    function isSelected(lineId) {
        return getSelectedIds(CF.getCart()).has(lineId);
    }

    function setSelected(lineId, checked) {
        const cart = CF.getCart();
        const ids = getSelectedIds(cart);
        if (checked) ids.add(lineId); else ids.delete(lineId);
        writeSelected(Array.from(ids));
    }

    function selectAll(checked) {
        const cart = CF.getCart();
        writeSelected(checked ? cart.map(c => c.lineId) : []);
    }

    // ── Rendering ─────────────────────────────────────────────────────
    function renderCart() {
        const cart = CF.getCart();
        const listEl = document.getElementById("cartList");
        const layout = document.getElementById("cartLayout");
        const empty = document.getElementById("cartEmpty");
        const label = document.getElementById("cartCountLabel");
        const selectAllRow = document.querySelector(".cf-select-all-row");

        const totalQty = cart.reduce((s, c) => s + (c.qty || 1), 0);
        label.textContent = totalQty + (totalQty === 1 ? " item in your cart" : " items in your cart");

        if (cart.length === 0) {
            layout.style.display = "none";
            empty.style.display = "block";
            return;
        }
        layout.style.display = "grid";
        empty.style.display = "none";
        if (selectAllRow) selectAllRow.style.display = "flex";

        const selectedIds = getSelectedIds(cart);

        listEl.innerHTML = cart.map(c => {
            const unitPrice = Number(c.price) + Number(c.servicePrice || 0);
            const checked = selectedIds.has(c.lineId);
            const stock = CF.toStockLimit(c.stockQuantity);
            const otherQty = cart.filter(x => x.productId === c.productId && x.lineId !== c.lineId)
                                  .reduce((s, x) => s + (x.qty || 1), 0);
            const roomLeft = Math.max(0, stock - otherQty);
            const designNote = (c.hasFrontDesign && c.hasBackDesign)
                ? "Design: Front + Back"
                : c.hasFrontDesign ? "Design: Front"
                : c.hasBackDesign ? "Design: Back"
                : "";
            const opts = [
                c.size ? `Size: ${CF.escapeHtml(c.size)}` : "",
                c.color ? `Color: ${CF.escapeHtml(CF.colorName(c.color))}` : "",
                c.service ? `Service: ${CF.escapeHtml(c.service)}${c.servicePrice ? ` (+${CF.peso(c.servicePrice)})` : ""}` : "",
                c.printLocation ? `Location: ${CF.escapeHtml(prettyLocation(c.printLocation))}` : "",
                designNote,
                c.locationNote ? `Note: ${CF.escapeHtml(c.locationNote)}` : ""
            ].filter(Boolean).join(" · ");

            return `
            <div class="cf-cart-item">
                <label class="cf-checkbox-wrap cf-cart-check">
                    <input type="checkbox" ${checked ? "checked" : ""} onchange="cfToggleLine('${c.lineId}', this.checked)" />
                </label>
                <div class="cf-cart-clickable" onclick="cfViewCartItem(event, '${c.lineId}')" title="View item details">
                    <div class="cf-cart-thumb">
                        ${c.image ? `<img src="${c.image}" alt="${CF.escapeHtml(c.name)}" />` : `<i class="fa-solid fa-shirt"></i>`}
                    </div>
                    <div class="cf-cart-info">
                        ${c.isCustomOrder ? `<div class="cf-cart-custom-tag">Custom Order</div>` : ""}
                        <div class="cf-cart-name">${CF.escapeHtml(c.name)}</div>
                        ${opts ? `<div class="cf-cart-opts">${opts}</div>` : ""}
                        <div class="cf-cart-price">${CF.peso(unitPrice)}</div>
                    </div>
                </div>
                <div class="cf-cart-right">
                    <div class="cf-qty-stepper">
                        <button onclick="cfChangeQty('${c.lineId}', -1)">−</button>
                        <span>${c.qty || 1}</span>
                        <button onclick="cfChangeQty('${c.lineId}', 1)" ${(c.qty || 1) >= roomLeft ? "disabled" : ""}>+</button>
                    </div>
                    <button class="cf-cart-remove" onclick="cfRemoveLine('${c.lineId}')">
                        <i class="fa-solid fa-trash"></i> Remove
                    </button>
                </div>
            </div>`;
        }).join("");

        updateSummary(cart, selectedIds);
    }

    // Order summary reflects only the checked (selected) lines. Each cart
    // item card shows its own unit price only; the added-up total lives
    // here in the summary panel.
    function updateSummary(cart, selectedIds) {
        const selectedLines = cart.filter(c => selectedIds.has(c.lineId));
        const subtotal = selectedLines.reduce((s, c) => s + (Number(c.price) + Number(c.servicePrice || 0)) * (c.qty || 1), 0);

        document.getElementById("cartTotal").textContent = CF.peso(subtotal);

        const selectAllBox = document.getElementById("cartSelectAll");
        if (selectAllBox) {
            selectAllBox.checked = cart.length > 0 && selectedLines.length === cart.length;
            selectAllBox.indeterminate = selectedLines.length > 0 && selectedLines.length < cart.length;
        }

        const checkoutBtn = document.getElementById("cartCheckoutBtn");
        if (checkoutBtn) checkoutBtn.disabled = selectedLines.length === 0;
    }

    function cfToggleLine(lineId, checked) {
        setSelected(lineId, checked);
        renderCart();
    }

    function cfToggleSelectAll(checked) {
        selectAll(checked);
        renderCart();
    }

    function cfChangeQty(lineId, delta) {
        const cart = CF.getCart();
        const line = cart.find(c => c.lineId === lineId);
        if (!line) return;
        const stock = CF.toStockLimit(line.stockQuantity);
        const otherQty = cart.filter(c => c.productId === line.productId && c.lineId !== lineId)
                              .reduce((s, c) => s + (c.qty || 1), 0);
        const roomLeft = Math.max(0, stock - otherQty);
        const desired = (line.qty || 1) + delta;
        if (delta > 0 && desired > roomLeft) {
            CF.toast(otherQty > 0
                ? `Only ${stock} in stock — ${otherQty} already in your cart in another option`
                : `Only ${stock} in stock`,
                { icon: "fa-triangle-exclamation", type: "info" });
        }
        CF.updateCartQty(lineId, desired);
        renderCart();
    }

    function cfRemoveLine(lineId) {
        CF.removeFromCart(lineId);
        renderCart();
        CF.toast("Item removed from cart", { icon: "fa-trash", type: "info" });
    }

    // Clicking a cart item's photo/info (NOT the checkbox, qty stepper, or
    // Remove button — those are separate elements, not inside the
    // clickable area, so clicks on them never reach here) takes the
    // shopper back to the same page it was added from:
    //   - Custom orders: productId is "custom-{categoryId}" (set in
    //     custom.cshtml's window.CF_PRODUCT.id) -> Customize/Custom for
    //     that category.
    //   - Regular products: productId is the numeric ProductID -> the
    //     regular product details page (hDetails).
    function cfViewCartItem(event, lineId) {
        event.preventDefault();
        const line = CF.getCart().find(c => c.lineId === lineId);
        if (!line) return;
        const pageData = window.CF_CART_PAGE || {};

        const customMatch = String(line.productId).match(/^custom-(\d+)$/);
        if (customMatch) {
            const template = pageData.customizeUrl || "/Customize/Custom?categoryId=0";
            window.location.href = template.replace(/categoryId=0(&|$)/, "categoryId=" + customMatch[1] + "$1");
            return;
        }
        if (String(line.productId).startsWith("custom-")) {
            // No specific category on this line (syntheticId fell back to
            // "custom-general") — nothing to open directly, so send them
            // to the category picker instead.
            window.location.href = pageData.customizeIndexUrl || "/Customize/Index";
            return;
        }

        const template = pageData.detailsUrl || "/Home/hDetails/0";
        window.location.href = template.replace(/\/0(\/?$|\?)/, "/" + line.productId + "$1");
    }

    function cfClearCart() {
        if (!CF.getCart().length) return;
        if (confirm("Remove all items from your cart?")) {
            CF.clearCart();
            writeSelected([]);
            renderCart();
        }
    }

    function cfCheckout() {
        const cart = CF.getCart();
        const selectedIds = getSelectedIds(cart);
        if (selectedIds.size === 0) {
            CF.toast("Select at least one item to checkout", { icon: "fa-circle-info", type: "info" });
            return;
        }
        CF.toast("Checkout is coming soon!", { icon: "fa-circle-info", type: "info" });
    }

    function toggleAccountMenu() {
        document.getElementById("navAccountMenu").classList.toggle("open");
    }
    document.addEventListener("click", function (e) {
        const wrap = document.getElementById("navAccountWrap");
        if (wrap && !wrap.contains(e.target)) {
            document.getElementById("navAccountMenu").classList.remove("open");
        }
    });

    document.addEventListener("DOMContentLoaded", function () {
        const pageData = window.CF_CART_PAGE || {};
        CF.wireSearchBar(document.getElementById("navSearchInput"), pageData.homeUrl || "/", null);
        renderCart();
    });

    // Expose handlers referenced by inline onclick=/onchange= attributes in the view
    window.toggleAccountMenu = toggleAccountMenu;
    window.cfChangeQty = cfChangeQty;
    window.cfRemoveLine = cfRemoveLine;
    window.cfClearCart = cfClearCart;
    window.cfCheckout = cfCheckout;
    window.cfToggleLine = cfToggleLine;
    window.cfToggleSelectAll = cfToggleSelectAll;
    window.cfViewCartItem = cfViewCartItem;

})();
