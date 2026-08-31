/* ============================================================
   STOREFRONT.JS
   Shared client-side Cart + Wishlist logic (localStorage-based)
   Used by: hIndex, hDetails, hWishlist, hCart
============================================================ */
(function (window) {
    "use strict";

    const CART_KEY = "cf_cart";
    const WISHLIST_KEY = "cf_wishlist";

    // Turns a stock value into a usable limit. null/undefined mean "no
    // limit" (e.g. Customize items, which aren't tracked in inventory) —
    // IMPORTANT: this must check for null/undefined BEFORE calling Number()
    // on it, because Number(null) is 0 (not NaN), which would otherwise
    // make "no limit" silently become "0 in stock" and block Add to Cart.
    function toStockLimit(value) {
        if (value === null || value === undefined) return Infinity;
        const n = Number(value);
        return Number.isFinite(n) ? n : Infinity;
    }

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
            return true;
        } catch (e) {
            return false; // e.g. quota exceeded — caller decides how to surface this
        }
    }

    // ── Account sync (server-side CartItems/WishlistItems) ──────
    // Guests keep working purely off localStorage above — everything in
    // this block only runs when window.CF_CUSTOMER_LOGGED_IN is true (set
    // by _Layout.cshtml). This is what makes cart/wishlist follow the
    // Customer's account instead of being stuck in whichever browser they
    // happened to be using.
    function isAccountSyncActive() {
        return window.CF_CUSTOMER_LOGGED_IN === true;
    }

    function antiForgeryToken() {
        const el = document.querySelector('#cfSiteAntiForgeryForm input[name="__RequestVerificationToken"]');
        return el ? el.value : "";
    }

    function postSync(url, itemsJson) {
        const formData = new FormData();
        formData.append("itemsJson", itemsJson);
        formData.append("__RequestVerificationToken", antiForgeryToken());
        // Fire-and-forget — a failed sync just means this device's next
        // load re-sends the current localStorage copy; nothing here
        // blocks the user's cart/wishlist action from feeling instant.
        return fetch(url, { method: "POST", body: formData, headers: { "X-Requested-With": "XMLHttpRequest" } })
            .catch(() => { /* offline/network hiccup — silently retried on next save */ });
    }

    function serverSyncCart(cart) {
        if (!isAccountSyncActive()) return;
        postSync("/Home/hSyncCart", JSON.stringify(cart));
    }
    function serverSyncWishlist(list) {
        if (!isAccountSyncActive()) return;
        postSync("/Home/hSyncWishlist", JSON.stringify(list));
    }

    // Merges any guest-browsing cart/wishlist lines (added before this
    // login, sitting in localStorage) into the account's server-saved
    // data, then makes localStorage mirror the merged, now-authoritative
    // result. Runs once per page load for a logged-in Customer — cheap
    // (two GETs), and a no-op in effect once local and server already
    // match, which is true on every load after the first.
    function mergeGuestDataIntoAccount() {
        if (!isAccountSyncActive()) return;

        fetch("/Home/hGetCart", { headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(res => res.ok ? res.json() : [])
            .then(serverCart => {
                const localCart = getCart();
                const serverKeys = new Set(serverCart.map(c => [c.productId, c.size, c.color, c.service, c.printLocation].join("|")));
                const guestOnly = localCart.filter(c => !serverKeys.has([c.productId, c.size, c.color, c.service, c.printLocation].join("|")));
                const merged = serverCart.concat(guestOnly);
                writeJson(CART_KEY, merged);
                updateNavBadges(); syncCartButtons();
                if (guestOnly.length) serverSyncCart(merged); // push newly-merged guest lines back up
            })
            .catch(() => { /* keep whatever's already in localStorage if this fails */ });

        fetch("/Home/hGetWishlist", { headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(res => res.ok ? res.json() : [])
            .then(serverList => {
                const localList = getWishlist();
                const serverIds = new Set(serverList.map(w => String(w.productId)));
                const guestOnly = localList.filter(w => !serverIds.has(String(w.productId)));
                const merged = serverList.concat(guestOnly);
                writeJson(WISHLIST_KEY, merged);
                updateNavBadges(); syncWishlistHearts();
                if (guestOnly.length) serverSyncWishlist(merged);
            })
            .catch(() => { /* keep whatever's already in localStorage if this fails */ });
    }

    // ── Cart ────────────────────────────────────────────────────
    function getCart() {
        return readJson(CART_KEY, []);
    }
    function saveCart(cart) {
        const ok = writeJson(CART_KEY, cart);
        updateNavBadges();
        syncCartButtons();
        serverSyncCart(cart);
        return ok;
    }

    // Stock is tracked per product, not per size/color, so a customer could
    // add "Red, Size M" up to the stock limit, then separately add "Blue,
    // Size L" and blow past it — each line looks fine on its own. This sums
    // every OTHER line of the same product currently in the cart so the cap
    // can be enforced across variants, not just within one line.
    function otherLinesQtyForProduct(cart, productId, excludeLineId) {
        return cart.filter(c => c.productId === productId && c.lineId !== excludeLineId)
                    .reduce((s, c) => s + (c.qty || 1), 0);
    }
    function cartQtyForProduct(productId, excludeLineId) {
        return otherLinesQtyForProduct(getCart(), productId, excludeLineId || null);
    }

    // item: { productId, name, price, image, qty, size, color, service, servicePrice, printLocation }
    function addToCart(item) {
        const cart = getCart();

        // Merge quantity if identical product + options already exist
        const lineKey = [item.productId, item.size, item.color, item.service, item.printLocation].join("|");
        const existing = cart.find(c =>
            [c.productId, c.size, c.color, c.service, c.printLocation].join("|") === lineKey
        );

        const stock = toStockLimit(item.stockQuantity);
        let affectedLineId;

        if (existing) {
            existing.stockQuantity = stock;
            const otherQty = otherLinesQtyForProduct(cart, item.productId, existing.lineId);
            const roomLeft = Math.max(0, stock - otherQty);
            existing.qty = Math.min(existing.qty + item.qty, roomLeft || existing.qty);
            affectedLineId = existing.lineId;
        } else {
            const otherQty = otherLinesQtyForProduct(cart, item.productId, null);
            const roomLeft = Math.max(0, stock - otherQty);
            if (roomLeft <= 0) {
                toast(`Only ${stock} in stock — you already have that many in your cart`,
                      { icon: "fa-triangle-exclamation", type: "info" });
                return cart;
            }
            item.qty = Math.min(item.qty, roomLeft);
            item.lineId = "line_" + Date.now() + "_" + Math.floor(Math.random() * 10000);
            cart.push(item);
            affectedLineId = item.lineId;
        }
        const saved = saveCart(cart);
        if (!saved) return null; // save failed (e.g. storage quota)

        // Every item that lands in the cart — whether via "Add to Cart" or
        // "Buy Now"/"Check Out", predesigned or custom — starts pre-selected
        // for checkout. Nobody should have to tick a checkbox in the Cart
        // page just to pay for something they just added; a shopper can
        // still uncheck a line there if they want to leave it for later.
        setSelectedId(affectedLineId, true);
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
            const stock = toStockLimit(line.stockQuantity);
            const otherQty = otherLinesQtyForProduct(cart, line.productId, lineId);
            const roomLeft = Math.max(0, stock - otherQty);
            line.qty = Math.min(Math.max(1, qty), roomLeft || 1);
            saveCart(cart);
        }
        return cart;
    }

    function clearCart() {
        saveCart([]);
    }

    // ── Selected lines (which cart lines are checked for checkout) ──────
    // Shared between hCart (checking items) and hBilling (reading what was
    // checked). Lives here instead of duplicated in each page's JS file.
    const SELECTED_KEY = "cf_cart_selected";
    function readSelectedIds() {
        try {
            const raw = localStorage.getItem(SELECTED_KEY);
            return raw ? JSON.parse(raw) : null; // null = "not set yet"
        } catch (e) {
            return null;
        }
    }
    function writeSelectedIds(ids) {
        try { localStorage.setItem(SELECTED_KEY, JSON.stringify(ids)); } catch (e) { /* ignore */ }
    }
    // Returns the current set of selected lineIds, pruning any that no
    // longer exist in the cart. First-ever visit (nothing recorded yet)
    // defaults to everything in the cart selected — see addToCart() above,
    // which also explicitly selects each line as it's added — so a
    // shopper is never forced to tick a checkbox just to check out what's
    // already sitting in their cart.
    function getSelectedIds(cart) {
        cart = cart || getCart();
        const cartIds = cart.map(c => c.lineId);
        let stored = readSelectedIds();
        if (stored === null) {
            stored = cartIds.slice();
        } else {
            stored = stored.filter(id => cartIds.includes(id)); // drop stale ids
        }
        writeSelectedIds(stored);
        return new Set(stored);
    }
    function setSelectedId(lineId, checked) {
        const cart = getCart();
        const ids = getSelectedIds(cart);
        if (checked) ids.add(lineId); else ids.delete(lineId);
        writeSelectedIds(Array.from(ids));
    }
    function selectAllIds(checked) {
        const cart = getCart();
        writeSelectedIds(checked ? cart.map(c => c.lineId) : []);
    }
    // The actual cart line objects currently checked for checkout.
    function getSelectedCartItems() {
        const cart = getCart();
        const ids = getSelectedIds(cart);
        return cart.filter(c => ids.has(c.lineId));
    }
    // Removes specific lineIds from both the cart and the selection —
    // used after an order is successfully placed.
    function removeLines(lineIds) {
        const idSet = new Set(lineIds);
        const cart = getCart().filter(c => !idSet.has(c.lineId));
        saveCart(cart);
        writeSelectedIds(Array.from(getSelectedIds(cart)));
        return cart;
    }

    function cartCount() {
        return getCart().reduce((sum, c) => sum + (c.qty || 1), 0);
    }

    // Build a comparable key for "same line" matching (product + chosen options)
    function lineKeyOf(o) {
        return [o.productId, o.size || "", o.color || "", o.service || "", o.printLocation || ""].join("|");
    }

    // Is a matching product+options combo currently in the cart?
    function isInCart(match) {
        const key = lineKeyOf(match);
        return getCart().some(c => lineKeyOf(c) === key);
    }

    // Toggle add/remove: if the exact product+options combo is already in the
    // cart, remove it; otherwise add it with qty 1. Returns true if now in cart.
    function toggleCartItem(item) {
        const cart = getCart();
        const key = lineKeyOf(item);
        const idx = cart.findIndex(c => lineKeyOf(c) === key);
        let nowActive;
        if (idx >= 0) {
            cart.splice(idx, 1);
            nowActive = false;
        } else {
            item.qty = 1;
            item.lineId = "line_" + Date.now() + "_" + Math.floor(Math.random() * 10000);
            cart.push(item);
            nowActive = true;
        }
        saveCart(cart);
        return nowActive;
    }

    // ── Wishlist ────────────────────────────────────────────────
    function getWishlist() {
        return readJson(WISHLIST_KEY, []);
    }
    function saveWishlist(list) {
        writeJson(WISHLIST_KEY, list);
        updateNavBadges();
        serverSyncWishlist(list);
    }

    function isWishlisted(productId) {
        return getWishlist().some(w => String(w.productId) === String(productId));
    }

    function clearWishlist() {
        saveWishlist([]);
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

    // Sync quick-add cart buttons on any page listing products (data-cf-cart on button, no options)
    function syncCartButtons() {
        document.querySelectorAll("[data-cf-cart]").forEach(btn => {
            const id = btn.getAttribute("data-cf-cart");
            const icon = btn.querySelector("i");
            const inCart = isInCart({ productId: id, size: "", color: "", service: "", printLocation: "" });
            if (inCart) {
                btn.classList.add("active");
                if (icon) { icon.classList.remove("fa-cart-plus"); icon.classList.add("fa-check"); }
            } else {
                btn.classList.remove("active");
                if (icon) { icon.classList.remove("fa-check"); icon.classList.add("fa-cart-plus"); }
            }
        });
    }
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

    // ── Color name lookup ───────────────────────────────────────
    // Admin picks colors with a native <input type="color">, which only ever
    // gives back hex codes, so anything stored on a product/cart line is a
    // hex string like "#e53e3e". This maps that hex to the closest common
    // color name (nearest match by RGB distance) so shoppers see "Red"
    // instead of "#e53e3e". Non-hex values (already a name) pass through.
    const NAMED_COLORS = {
        "Black": "#000000", "White": "#ffffff", "Gray": "#808080", "Silver": "#c0c0c0",
        "Red": "#e53e3e", "Maroon": "#742a2a", "Crimson": "#dc143c", "Pink": "#d53f8c",
        "Hot Pink": "#ff69b4", "Orange": "#dd6b20", "Coral": "#ff7f50", "Gold": "#ffd700",
        "Yellow": "#d69e2e", "Khaki": "#c3b091", "Beige": "#fef3c7", "Tan": "#d2b48c",
        "Brown": "#744210", "Chocolate": "#7b3f00", "Green": "#38a169", "Lime": "#32cd32",
        "Olive": "#808000", "Teal": "#2c7a7b", "Turquoise": "#40e0d0", "Cyan": "#00ffff",
        "Sky Blue": "#87ceeb", "Blue": "#3182ce", "Navy": "#1a365d", "Indigo": "#4b0082",
        "Purple": "#805ad5", "Violet": "#8f00ff", "Plum": "#dda0dd", "Magenta": "#ff00ff",
        "Charcoal": "#36454f"
    };
    function hexToRgb(hex) {
        const h = hex.replace("#", "").trim();
        const full = h.length === 3 ? h.split("").map(c => c + c).join("") : h;
        const num = parseInt(full, 16);
        if (isNaN(num) || full.length !== 6) return null;
        return { r: (num >> 16) & 255, g: (num >> 8) & 255, b: num & 255 };
    }
    function colorName(value) {
        if (!value) return "";
        const v = String(value).trim();
        const isHex = /^#?[0-9a-f]{3}$|^#?[0-9a-f]{6}$/i.test(v);
        if (!isHex) return v; // already a readable name
        const rgb = hexToRgb(v.startsWith("#") ? v : "#" + v);
        if (!rgb) return v;
        let best = null, bestDist = Infinity;
        for (const name in NAMED_COLORS) {
            const nrgb = hexToRgb(NAMED_COLORS[name]);
            const dist = Math.pow(rgb.r - nrgb.r, 2) + Math.pow(rgb.g - nrgb.g, 2) + Math.pow(rgb.b - nrgb.b, 2);
            if (dist < bestDist) { bestDist = dist; best = name; }
        }
        return best || v;
    }

    // ── Logout cleanup ──────────────────────────────────────────
    // Cart/Wishlist are stored in localStorage, which is shared by the
    // browser regardless of which account is logged in — logging out
    // does NOT clear it on its own. This is the one place that knows
    // every guest-data key, so the "log out" link only has to call this
    // instead of listing keys inline (which is how the wishlist got left
    // out before: cart was cleared on logout, wishlist wasn't).
    function clearGuestData() {
        clearCart();
        try { localStorage.removeItem(SELECTED_KEY); } catch (e) { /* ignore */ }
        clearWishlist();
    }

    // ── Public API ──────────────────────────────────────────────
    window.CF = {
        getCart, saveCart, addToCart, removeFromCart, updateCartQty, clearCart, cartCount,
        isInCart, toggleCartItem, cartQtyForProduct, toStockLimit,
        getSelectedIds, setSelectedId, selectAllIds, getSelectedCartItems, removeLines,
        getWishlist, saveWishlist, isWishlisted, toggleWishlist, removeFromWishlist, wishlistCount, clearWishlist,
        clearGuestData,
        updateNavBadges, syncWishlistHearts, syncCartButtons, toast, wireSearchBar, escapeHtml, peso, colorName
    };

    document.addEventListener("DOMContentLoaded", () => {
        updateNavBadges();
        syncWishlistHearts();
        syncCartButtons();
        mergeGuestDataIntoAccount();
    });

})(window);
