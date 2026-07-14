/* ============================================================
   PRODUCT-DETAILS.JS
   Page logic for Views/Home/hDetails.cshtml
   Depends on: storefront.js (window.CF), window.CF_PRODUCT (page data)
============================================================ */
(function () {
    "use strict";

    const product = window.CF_PRODUCT || {};
    const unitPrice = Number(product.price) || 0;
    let svcPrice = 0;

    function updateTotal() {
        const total = unitPrice + svcPrice;
        document.getElementById("totalPrice").textContent =
            "₱" + total.toLocaleString("en-PH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function selectGroup(el, containerSel, itemSel) {
        el.closest(containerSel).querySelectorAll(itemSel)
          .forEach(c => c.classList.remove("active"));
        el.classList.add("active");
        refreshCartButtonState();
    }

    function selectSvc(el) {
        const wasActive = el.classList.contains("active");
        document.querySelectorAll(".pd-svc").forEach(c => c.classList.remove("active"));
        svcPrice = 0;
        if (!wasActive) {
            el.classList.add("active");
            const txt = el.querySelector(".pd-svc-price")?.textContent || "";
            svcPrice = parseFloat(txt.replace(/[^\d.]/g, "")) || 0;
        }
        updateTotal();
        refreshCartButtonState();
    }

    function switchMainImage(el, src) {
        document.querySelectorAll(".pd-thumb").forEach(t => t.classList.remove("active"));
        el.classList.add("active");
        if (src) document.getElementById("mainImg").src = src;
    }

    function handleFiles(input) {
        const wrap = document.getElementById("uploadPreviews");
        wrap.innerHTML = "";
        Array.from(input.files).forEach(file => {
            if (!file.type.startsWith("image/")) return;
            const reader = new FileReader();
            reader.onload = e => {
                const div = document.createElement("div");
                div.className = "pd-preview-thumb";
                div.innerHTML = `<img src="${e.target.result}"/><button class="pd-preview-rm" onclick="this.parentElement.remove()">✕</button>`;
                wrap.appendChild(div);
            };
            reader.readAsDataURL(file);
        });
    }

    function toggleAccountMenu() {
        document.getElementById("navAccountMenu").classList.toggle("show");
    }
    document.addEventListener("click", function (e) {
        const wrap = document.getElementById("navAccountWrap");
        if (wrap && !wrap.contains(e.target))
            document.getElementById("navAccountMenu")?.classList.remove("show");
    });

    // ── Wishlist toggle ──
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

    // Reads whatever size/color/service/print-location is currently selected
    function currentSelection() {
        const size = document.querySelector(".pd-size.active")?.textContent.trim() || "";
        const color = document.querySelector(".pd-color.active")?.getAttribute("title") || "";
        const svcEl = document.querySelector(".pd-svc.active");
        const service = svcEl ? svcEl.querySelector(".pd-svc-name")?.textContent.trim() : "";
        const location = document.querySelector(".pd-loc.active")?.textContent.trim() || "";
        return { size, color, service, location };
    }

    // ── Add to Cart toggle: click once to add (qty 1), click again to remove.
    //    Quantity itself is only ever changed from within the Cart page. ──
    function cfDetailAddToCart() {
        const { size, color, service, location } = currentSelection();
        const image = document.getElementById("mainImg")?.getAttribute("src") || "";

        const nowInCart = CF.toggleCartItem({
            productId: product.id,
            name: product.name,
            price: unitPrice,
            image: image,
            size: size,
            color: color,
            service: service,
            servicePrice: svcPrice,
            printLocation: location
        });

        refreshCartButtonState();
        CF.toast(nowInCart ? "Added to cart" : "Removed from cart",
                 { icon: nowInCart ? "fa-cart-plus" : "fa-cart-arrow-down", type: nowInCart ? "" : "info" });
    }

    // Reflects whether the currently-selected combo is already in the cart
    function refreshCartButtonState() {
        const btn = document.getElementById("pdAddToCartBtn");
        if (!btn) return;
        const { size, color, service, location } = currentSelection();
        const inCart = CF.isInCart({
            productId: product.id,
            size: size, color: color, service: service, printLocation: location
        });
        const icon = document.getElementById("pdCartIcon");
        const label = document.getElementById("pdCartLabel");
        if (inCart) {
            btn.classList.add("in-cart");
            if (icon) { icon.classList.remove("fa-cart-plus"); icon.classList.add("fa-cart-check"); }
            if (label) label.textContent = "Added to Cart";
        } else {
            btn.classList.remove("in-cart");
            if (icon) { icon.classList.remove("fa-cart-check"); icon.classList.add("fa-cart-plus"); }
            if (label) label.textContent = "Add to Cart";
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        CF.wireSearchBar(
            document.getElementById("navSearchInput"),
            product.homeUrl || "/",
            null
        );
        refreshCartButtonState();
    });

    // Expose handlers referenced by inline onclick= attributes in the view
    window.selectGroup = selectGroup;
    window.selectSvc = selectSvc;
    window.switchMainImage = switchMainImage;
    window.handleFiles = handleFiles;
    window.toggleAccountMenu = toggleAccountMenu;
    window.cfDetailToggleFav = cfDetailToggleFav;
    window.cfDetailAddToCart = cfDetailAddToCart;

})();

