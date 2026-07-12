/* ============================================================
   PRODUCT-DETAILS.JS
   Page logic for Views/Home/hDetails.cshtml
   Depends on: storefront.js (window.CF), window.CF_PRODUCT (page data)
============================================================ */
(function () {
    "use strict";

    const product = window.CF_PRODUCT || {};
    const unitPrice = Number(product.price) || 0;
    let qty = 1;
    let svcPrice = 0;

    function changeQty(d) { setQty(Math.max(1, qty + d)); }

    function setQty(n) {
        qty = n;
        document.getElementById("qtyDisplay").textContent = qty;
        updateTotal();
    }

    function updateTotal() {
        const discount = qty >= 100 ? .15 : qty >= 50 ? .10 : qty >= 20 ? .05 : 0;
        const total = (unitPrice + svcPrice) * qty * (1 - discount);
        document.getElementById("totalPrice").textContent =
            "₱" + total.toLocaleString("en-PH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function selectGroup(el, containerSel, itemSel) {
        el.closest(containerSel).querySelectorAll(itemSel)
          .forEach(c => c.classList.remove("active"));
        el.classList.add("active");
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

    // ── Add to cart (reads current selections: size, color, service, print location, qty) ──
    function cfDetailAddToCart() {
        const size = document.querySelector(".pd-size.active")?.textContent.trim() || "";
        const color = document.querySelector(".pd-color.active")?.getAttribute("title") || "";
        const svcEl = document.querySelector(".pd-svc.active");
        const service = svcEl ? svcEl.querySelector(".pd-svc-name")?.textContent.trim() : "";
        const location = document.querySelector(".pd-loc.active")?.textContent.trim() || "";
        const image = document.getElementById("mainImg")?.getAttribute("src") || "";

        CF.addToCart({
            productId: product.id,
            name: product.name,
            price: unitPrice,
            image: image,
            qty: qty,
            size: size,
            color: color,
            service: service,
            servicePrice: svcPrice,
            printLocation: location
        });
        CF.toast("Added to cart", { icon: "fa-cart-plus" });
    }

    document.addEventListener("DOMContentLoaded", function () {
        CF.wireSearchBar(
            document.getElementById("navSearchInput"),
            product.homeUrl || "/",
            null
        );
    });

    // Expose handlers referenced by inline onclick= attributes in the view
    window.changeQty = changeQty;
    window.setQty = setQty;
    window.selectGroup = selectGroup;
    window.selectSvc = selectSvc;
    window.switchMainImage = switchMainImage;
    window.handleFiles = handleFiles;
    window.toggleAccountMenu = toggleAccountMenu;
    window.cfDetailToggleFav = cfDetailToggleFav;
    window.cfDetailAddToCart = cfDetailAddToCart;

})();
