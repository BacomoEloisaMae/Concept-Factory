/* ============================================================
   STOREFRONT.JS
   Shared client-side Cart + Wishlist logic (localStorage-based)
   Used by: hIndex, hDetails, hWishlist, hCart
============================================================ */
(function (window) {
    "use strict";

    const CART_KEY = "cf_cart";
    const WISHLIST_KEY = "cf_wishlist";

    // ── Storage helpers ─────────────────────────────────────────
    function readJson(key, fallback) {
        try {
            const raw = localStorage.getItem(key);
            return raw ? JSON.parse(raw) : fallback;
        } catch (e) {
            return fallback;
        }
    }
    function writeJson(key, value) {
        try {
            localStorage.setItem(key, JSON.stringify(value));
        } catch (e) { /* storage unavailable, fail silently */ }
    }

    // ── Cart ────────────────────────────────────────────────────
    function getCart() {
        return readJson(CART_KEY, []);
    }
    function saveCart(cart) {
        writeJson(CART_KEY, cart);
        updateNavBadges();
    }

    // item: { productId, name, price, image, qty, size, color, service, servicePrice, printLocation }
    function addToCart(item) {
        const cart = getCart();

        // Merge quantity if identical product + options already exist
        const lineKey = [item.productId, item.size, item.color, item.service, item.printLocation].join("|");
        const existing = cart.find(c =>
            [c.productId, c.size, c.color, c.service, c.printLocation].join("|") === lineKey
        );

        if (existing) {
            existing.qty += item.qty;
        } else {
            item.lineId = "line_" + Date.now() + "_" + Math.floor(Math.random() * 10000);
            cart.push(item);
        }
        saveCart(cart);
        return cart;
    }

    function removeFromCart(lineId) {
        const cart = getCart().filter(c => c.lineId !== lineId);
        saveCart(cart);
        return cart;
    }

    function updateCartQty(lineId, qty) {
        const cart = getCart();
        const line = cart.find(c => c.lineId === lineId);
        if (line) {
            line.qty = Math.max(1, qty);
            saveCart(cart);
        }
        return cart;
    }

    function clearCart() {
        saveCart([]);
    }

    function cartCount() {
        return getCart().reduce((sum, c) => sum + (c.qty || 1), 0);
    }

    // ── Wishlist ────────────────────────────────────────────────
    function getWishlist() {
        return readJson(WISHLIST_KEY, []);
    }
    function saveWishlist(list) {
        writeJson(WISHLIST_KEY, list);
        updateNavBadges();
    }

    function isWishlisted(productId) {
        return getWishlist().some(w => String(w.productId) === String(productId));
    }

    // product: { productId, name, price, image, category }
    // returns true if now wishlisted, false if removed
    function toggleWishlist(product) {
        let list = getWishlist();
        const idx = list.findIndex(w => String(w.productId) === String(product.productId));
        let nowActive;
        if (idx >= 0) {
            list.splice(idx, 1);
            nowActive = false;
        } else {
            list.push(product);
            nowActive = true;
        }
        saveWishlist(list);
        return nowActive;
    }

    function removeFromWishlist(productId) {
        const list = getWishlist().filter(w => String(w.productId) !== String(productId));
        saveWishlist(list);
        return list;
    }

    function wishlistCount() {
        return getWishlist().length;
    }

    // ── Nav badges ──────────────────────────────────────────────
    function updateNavBadges() {
        document.querySelectorAll("[data-cf-cart-badge]").forEach(el => {
            const n = cartCount();
            el.textContent = n;
            el.style.display = n > 0 ? "flex" : "none";
        });
        document.querySelectorAll("[data-cf-wishlist-badge]").forEach(el => {
            const n = wishlistCount();
            el.textContent = n;
            el.style.display = n > 0 ? "flex" : "none";
        });
    }

    // Sync heart icons on any page that lists products (data-product-id on .catalog-card-fav)
    function syncWishlistHearts() {
        document.querySelectorAll("[data-cf-fav]").forEach(btn => {
            const id = btn.getAttribute("data-cf-fav");
            const icon = btn.querySelector("i");
            if (isWishlisted(id)) {
                btn.classList.add("active");
                if (icon) { icon.classList.remove("fa-regular"); icon.classList.add("fa-solid"); }
            } else {
                btn.classList.remove("active");
                if (icon) { icon.classList.remove("fa-solid"); icon.classList.add("fa-regular"); }
            }
        });
    }

    // ── Toast notifications ────────────────────────────────────
    function toast(message, opts) {
        opts = opts || {};
        let host = document.getElementById("cfToastHost");
        if (!host) {
            host = document.createElement("div");
            host.id = "cfToastHost";
            host.className = "cf-toast-host";
            document.body.appendChild(host);
        }
        const el = document.createElement("div");
        el.className = "cf-toast" + (opts.type ? " cf-toast-" + opts.type : "");
        el.innerHTML = `<i class="fa-solid ${opts.icon || 'fa-circle-check'}"></i><span>${message}</span>`;
        host.appendChild(el);
        requestAnimationFrame(() => el.classList.add("show"));
        setTimeout(() => {
            el.classList.remove("show");
            setTimeout(() => el.remove(), 250);
        }, 2200);
    }

    // ── Search bar wiring ───────────────────────────────────────
    // baseUrl: the hIndex action URL (e.g. "/") ; categoryId: currently selected category (nullable)
    function wireSearchBar(inputEl, baseUrl, categoryId) {
        if (!inputEl) return;
        const go = () => {
            const val = inputEl.value.trim();
            const params = new URLSearchParams();
            if (val) params.set("search", val);
            if (categoryId) params.set("categoryId", categoryId);
            const qs = params.toString();
            window.location.href = baseUrl + (qs ? "?" + qs : "") + "#products";
        };
        inputEl.addEventListener("keydown", e => {
            if (e.key === "Enter") { e.preventDefault(); go(); }
        });
        const icon = inputEl.parentElement ? inputEl.parentElement.querySelector("i.fa-magnifying-glass") : null;
        if (icon) {
            icon.style.cursor = "pointer";
            icon.addEventListener("click", go);
        }
    }

    // ── Formatting helpers ──────────────────────────────────────
    function escapeHtml(s) {
        return String(s ?? "").replace(/[&<>"']/g, c => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
        }[c]));
    }
    function peso(n) {
        return "₱" + Number(n || 0).toLocaleString("en-PH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    // ── Public API ──────────────────────────────────────────────
    window.CF = {
        getCart, saveCart, addToCart, removeFromCart, updateCartQty, clearCart, cartCount,
        getWishlist, saveWishlist, isWishlisted, toggleWishlist, removeFromWishlist, wishlistCount,
        updateNavBadges, syncWishlistHearts, toast, wireSearchBar, escapeHtml, peso
    };

    document.addEventListener("DOMContentLoaded", () => {
        updateNavBadges();
        syncWishlistHearts();
    });

})(window);
