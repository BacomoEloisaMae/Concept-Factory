/* ============================================================
   PRODUCT-DETAILS.JS
   Shared logic for both product pages:
     - Views/Home/hDetails.cshtml (finished-product shop page)
     - Views/Customize/custom.cshtml (build-your-own page)
   Both pages need size/color selection, quantity + bulk discount,
   and Add to Cart / Buy Now — so that logic lives here once.

   Anything that's ONLY relevant to Customize (design upload,
   print-location overlay, silhouette masking, the color-gallery
   thumb strip, printing services) lives directly in custom.cshtml's
   own <script> block instead of here, so hDetails.cshtml — the
   highest-traffic page — never has to download or parse code it
   doesn't use. The two sides talk through a couple of small global
   hooks (window.onMainImageChanged, window.getCustomizeCartExtras)
   that custom.cshtml defines and this file calls IF they exist.

   Depends on: storefront.js (window.CF), window.CF_PRODUCT
============================================================ */
(function () {
    "use strict";

    const product = window.CF_PRODUCT || {};
    const unitPrice = Number(product.price) || 0;
    // True stock for the product, independent of what's already in the cart
    const trueStock = CF.toStockLimit(product.stockQuantity);
    // How much of THIS product (any size/color) is already sitting in the cart
    const alreadyInCart = (window.CF && CF.cartQtyForProduct) ? CF.cartQtyForProduct(product.id) : 0;
    // Fall back to Infinity so products without a stock value aren't capped
    const maxQty = Number.isFinite(trueStock) ? Math.max(0, trueStock - alreadyInCart) : Infinity;

    let qty = 1;
    let discountPercent = 0;

    function formatPeso(n) {
        return "₱" + Number(n || 0).toLocaleString("en-PH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function unitPriceAfterDiscount() {
        return unitPrice * (1 - discountPercent / 100);
    }

    function updateTotal() {
        const pdQty = document.getElementById("pdQty");
        const pdSummaryQty = document.getElementById("pdSummaryQty");
        const pdSummaryUnitPrice = document.getElementById("pdSummaryUnitPrice");
        const totalPrice = document.getElementById("totalPrice");
        if (pdQty) pdQty.textContent = qty;
        if (pdSummaryQty) pdSummaryQty.textContent = qty + (qty === 1 ? " pc" : " pcs");
        if (pdSummaryUnitPrice) pdSummaryUnitPrice.textContent = formatPeso(unitPriceAfterDiscount());
        if (totalPrice) totalPrice.textContent = formatPeso(unitPriceAfterDiscount() * qty);
    }

    // Clears bulk-badge highlighting whenever the qty no longer matches a preset
    function clearBulkBadgesIfMismatch() {
        document.querySelectorAll(".pd-qty-badge").forEach(b => {
            if (Number(b.dataset.qty) !== qty) b.classList.remove("active");
        });
    }

    function stockLimitMessage() {
        return alreadyInCart > 0
            ? `Only ${trueStock} in stock — you already have ${alreadyInCart} in your cart`
            : `Only ${trueStock} in stock`;
    }

    function changeQty(delta) {
        const next = Math.max(1, qty + delta);
        if (next > maxQty) {
            qty = Math.max(1, maxQty);
            CF.toast(stockLimitMessage(), { icon: "fa-triangle-exclamation", type: "info" });
        } else {
            qty = next;
        }
        discountPercent = 0;
        clearBulkBadgesIfMismatch();
        updateTotal();
    }

    function setBulkQty(el, presetQty) {
        if (presetQty > maxQty) {
            CF.toast(stockLimitMessage(), { icon: "fa-triangle-exclamation", type: "info" });
            return;
        }
        qty = presetQty;
        document.querySelectorAll(".pd-qty-badge").forEach(b => b.classList.remove("active"));
        el.classList.add("active");
        updateTotal();
    }

    // ── Shared by both pages: size/color chip selection, thumbnail swap ──
    function selectGroup(el, containerSel, itemSel) {
        el.closest(containerSel).querySelectorAll(itemSel)
          .forEach(c => c.classList.remove("active"));
        el.classList.add("active");
    }

    function switchMainImage(el, src, viewType) {
        document.querySelectorAll(".pd-thumb").forEach(t => t.classList.remove("active"));
        el.classList.add("active");
        if (src) {
            document.getElementById("mainImg").src = src;
            // Customize page only: keeps the design-clipping mask and the
            // design-overlay visibility rule (front/back only) in sync.
            // Defined in custom.cshtml's own script; simply absent on hDetails.
            if (window.onMainImageChanged) window.onMainImageChanged(src, viewType);
        }
    }

    // hDetails.cshtml only: swaps to a specific color's own uploaded photo
    // (set via the admin's per-color "Add Color" upload on a real Product).
    function swapImageForColor(colorImagePath) {
        const mainImg = document.getElementById("mainImg");
        if (!mainImg) return;
        if (mainImg.dataset) mainImg.dataset.fallback = ""; // allow one fresh fallback attempt for the new src
        mainImg.src = colorImagePath || product.defaultImage || mainImg.src;
    }

    // hDetails.cshtml only: image zoom lightbox
    function zoomMainImage() {
        const src = document.getElementById("mainImg")?.getAttribute("src");
        if (!src) return;
        document.getElementById("pdZoomImg").src = src;
        document.getElementById("pdZoomOverlay").style.display = "flex";
    }
    function closeZoom() {
        document.getElementById("pdZoomOverlay").style.display = "none";
    }

    // hDetails.cshtml only: wishlist toggle
    function cfDetailToggleFav(btn) {
        btn = btn || document.getElementById("pdWishlistBtn");
        const item = {
            productId: btn.dataset.cfFav,
            name: btn.dataset.name,
            price: parseFloat(btn.dataset.price) || 0,
            image: btn.dataset.image || "",
            category: btn.dataset.category || ""
        };
        const nowActive = CF.toggleWishlist(item);
        CF.syncWishlistHearts();
        CF.toast(nowActive ? "Added to wishlist" : "Removed from wishlist",
                 { icon: nowActive ? "fa-heart" : "fa-heart-crack", type: nowActive ? "" : "info" });
    }

    function currentSelection() {
        const size = document.querySelector(".pd-size.active")?.textContent.trim() || "";
        const color = document.querySelector(".pd-color.active")?.getAttribute("title") || "";
        return { size, color };
    }

    // Builds the cart line item for the current selection, baking the bulk
    // discount straight into the stored unit price so the Cart page (which
    // simply multiplies price * qty) shows the correct discounted total
    // without needing any changes itself.
    function buildCartItem() {
        const { size, color } = currentSelection();
        const image = document.getElementById("mainImg")?.getAttribute("src") || "";
        const item = {
            productId: product.id,
            name: product.name,
            price: unitPriceAfterDiscount(),
            image: image,
            qty: qty,
            size: size,
            color: color,
            service: "",
            servicePrice: 0,
            printLocation: "",
            stockQuantity: Number.isFinite(trueStock) ? trueStock : null
        };

        // Customize page only: services chosen, print location, location
        // note, active uploaded design. Defined in custom.cshtml's own
        // script; simply absent (so this no-ops) on hDetails.cshtml.
        if (window.getCustomizeCartExtras) {
            Object.assign(item, window.getCustomizeCartExtras());
        }

        return item;
    }

    // Checks that every option this product actually offers (size/color)
    // has been picked. Returns true if OK, otherwise toasts and returns false.
    // Called by cfDetailAddToCart() and cfDetailBuyNow() below, before
    // either is allowed to proceed.
    // Connected elements: #pdSizeStep / .pd-size (hDetails.cshtml + custom.cshtml),
    // #pdServicesStep / .pd-svc-grid (Views/Customize/custom.cshtml only).
    // Note: Print Location's description field (#pdLocationText) is
    // optional and intentionally NOT validated here.
    function validateSelection() {
        if (product.hasSizes && !document.querySelector(".pd-size.active")) {
            flagInvalid("pdSizeStep");
            CF.toast("Please select a size", { icon: "fa-triangle-exclamation", type: "info" });
            return false;
        }
        if (product.hasColors && !document.querySelector(".pd-color.active")) {
            CF.toast("Please select a color", { icon: "fa-triangle-exclamation", type: "info" });
            return false;
        }

        // Customize page only ("3. Select Printing Services") — this
        // section doesn't exist on hDetails.cshtml, so the check simply
        // no-ops there. Print Location's description field ("4. Print
        // Location") is intentionally optional, so it's not checked here.
        const svcGrid = document.querySelector(".pd-svc-grid");
        if (svcGrid && !document.querySelector(".pd-svc.active")) {
            flagInvalid("pdServicesStep");
            CF.toast("Please select a printing service", { icon: "fa-triangle-exclamation", type: "info" });
            return false;
        }

        return true;
    }

    // Highlights an unfinished step (red title + outline) so it's obvious
    // which one still needs attention, on top of the toast message.
    function flagInvalid(stepId) {
        const step = document.getElementById(stepId);
        if (step) step.classList.add("pd-invalid");
    }
    function clearInvalid(stepId) {
        const step = document.getElementById(stepId);
        if (step) step.classList.remove("pd-invalid");
    }

    // ── Add to Cart: always adds/merges the current selection + quantity ──
    function cfDetailAddToCart() {
        if (!validateSelection()) return;
        if (maxQty <= 0) {
            CF.toast(stockLimitMessage(), { icon: "fa-triangle-exclamation", type: "info" });
            return;
        }
        const result = CF.addToCart(buildCartItem());
        if (result === null) {
            CF.toast("Couldn't save to cart — your browser's storage may be full", { icon: "fa-triangle-exclamation", type: "info" });
            return;
        }
        CF.toast("Added to cart", { icon: "fa-cart-plus" });
    }

    // ── Buy Now / Check Out: full checkout isn't built yet anywhere in the
    // app, so this adds the item to cart (so nothing is lost) and sends the
    // shopper to the Cart page, matching the same "coming soon" messaging
    // used there ──
    function cfDetailBuyNow() {
        if (!validateSelection()) return;
        if (maxQty <= 0) {
            CF.toast(stockLimitMessage(), { icon: "fa-triangle-exclamation", type: "info" });
            return;
        }
        const result = CF.addToCart(buildCartItem());
        if (result === null) {
            CF.toast("Couldn't save to cart — your browser's storage may be full", { icon: "fa-triangle-exclamation", type: "info" });
            return;
        }
        CF.toast("Added to cart — taking you there now", { icon: "fa-cart-plus" });
        if (product.cartUrl) {
            setTimeout(() => { window.location.href = product.cartUrl; }, 700);
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        updateTotal();

        // Clear a step's "please fill this in" highlight the moment the
        // customer acts on it — doesn't need to re-validate everything,
        // just stop nagging about the one thing they just did.
        document.getElementById("pdSizeStep")?.addEventListener("click", e => {
            if (e.target.closest(".pd-size")) clearInvalid("pdSizeStep");
        });
        document.getElementById("pdServicesStep")?.addEventListener("click", e => {
            if (e.target.closest(".pd-svc")) clearInvalid("pdServicesStep");
        });
    });

    // Expose handlers referenced by inline onclick= attributes in the view
    window.selectGroup = selectGroup;
    window.switchMainImage = switchMainImage;
    window.swapImageForColor = swapImageForColor;
    window.zoomMainImage = zoomMainImage;
    window.closeZoom = closeZoom;
    window.changeQty = changeQty;
    window.setBulkQty = setBulkQty;
    window.cfDetailToggleFav = cfDetailToggleFav;
    window.cfDetailAddToCart = cfDetailAddToCart;
    window.cfDetailBuyNow = cfDetailBuyNow;

})();
