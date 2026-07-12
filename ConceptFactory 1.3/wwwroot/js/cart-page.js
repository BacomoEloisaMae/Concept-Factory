/* ============================================================
   CART-PAGE.JS
   Page logic for Views/Home/hCart.cshtml
   Depends on: storefront.js (window.CF), window.CF_CART_PAGE (page data)
============================================================ */
(function () {
    "use strict";

    function renderCart() {
        const cart = CF.getCart();
        const listEl = document.getElementById("cartList");
        const layout = document.getElementById("cartLayout");
        const empty = document.getElementById("cartEmpty");
        const label = document.getElementById("cartCountLabel");

        const totalQty = cart.reduce((s, c) => s + (c.qty || 1), 0);
        label.textContent = totalQty + (totalQty === 1 ? " item in your cart" : " items in your cart");

        if (cart.length === 0) {
            layout.style.display = "none";
            empty.style.display = "block";
            return;
        }
        layout.style.display = "grid";
        empty.style.display = "none";

        listEl.innerHTML = cart.map(c => {
            const lineTotal = (Number(c.price) + Number(c.servicePrice || 0)) * (c.qty || 1);
            const opts = [
                c.size ? `Size: ${CF.escapeHtml(c.size)}` : "",
                c.color ? `Color: ${CF.escapeHtml(c.color)}` : "",
                c.service ? `Service: ${CF.escapeHtml(c.service)}` : "",
                c.printLocation ? `Location: ${CF.escapeHtml(c.printLocation)}` : ""
            ].filter(Boolean).join(" · ");

            return `
            <div class="cf-cart-item">
                <div class="cf-cart-thumb">
                    ${c.image ? `<img src="${c.image}" alt="${CF.escapeHtml(c.name)}" />` : `<i class="fa-solid fa-shirt"></i>`}
                </div>
                <div class="cf-cart-info">
                    <div class="cf-cart-name">${CF.escapeHtml(c.name)}</div>
                    ${opts ? `<div class="cf-cart-opts">${opts}</div>` : ""}
                    <div class="cf-cart-price">${CF.peso(lineTotal)}</div>
                </div>
                <div class="cf-cart-right">
                    <div class="cf-qty-stepper">
                        <button onclick="cfChangeQty('${c.lineId}', -1)">−</button>
                        <span>${c.qty || 1}</span>
                        <button onclick="cfChangeQty('${c.lineId}', 1)">+</button>
                    </div>
                    <button class="cf-cart-remove" onclick="cfRemoveLine('${c.lineId}')">
                        <i class="fa-solid fa-trash"></i> Remove
                    </button>
                </div>
            </div>`;
        }).join("");

        const subtotal = cart.reduce((s, c) => s + (Number(c.price) + Number(c.servicePrice || 0)) * (c.qty || 1), 0);
        document.getElementById("cartSubtotal").textContent = CF.peso(subtotal);
        document.getElementById("cartItemCount").textContent = totalQty;
        document.getElementById("cartTotal").textContent = CF.peso(subtotal);
    }

    function cfChangeQty(lineId, delta) {
        const cart = CF.getCart();
        const line = cart.find(c => c.lineId === lineId);
        if (!line) return;
        CF.updateCartQty(lineId, (line.qty || 1) + delta);
        renderCart();
    }

    function cfRemoveLine(lineId) {
        CF.removeFromCart(lineId);
        renderCart();
        CF.toast("Item removed from cart", { icon: "fa-trash", type: "info" });
    }

    function cfClearCart() {
        if (!CF.getCart().length) return;
        if (confirm("Remove all items from your cart?")) {
            CF.clearCart();
            renderCart();
        }
    }

    function cfCheckout() {
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

    // Expose handlers referenced by inline onclick= attributes in the view
    window.toggleAccountMenu = toggleAccountMenu;
    window.cfChangeQty = cfChangeQty;
    window.cfRemoveLine = cfRemoveLine;
    window.cfClearCart = cfClearCart;
    window.cfCheckout = cfCheckout;

})();
