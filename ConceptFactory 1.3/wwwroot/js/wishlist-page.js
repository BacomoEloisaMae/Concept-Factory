/* ============================================================
   WISHLIST-PAGE.JS
   Page logic for Views/Home/hWishlist.cshtml
   Depends on: storefront.js (window.CF), window.CF_WISHLIST_PAGE (page data)
============================================================ */
(function () {
    "use strict";

    function renderWishlist() {
        const list = CF.getWishlist();
        const grid = document.getElementById("wishlistGrid");
        const empty = document.getElementById("wishlistEmpty");
        const label = document.getElementById("wishlistCountLabel");

        label.textContent = list.length + (list.length === 1 ? " item saved" : " items saved");

        if (list.length === 0) {
            grid.style.display = "none";
            empty.style.display = "block";
            return;
        }
        grid.style.display = "grid";
        empty.style.display = "none";

        grid.innerHTML = list.map(p => `
            <div class="catalog-card" style="position:relative;">
                <button class="cf-wish-remove" title="Remove" onclick="cfRemoveWish(event, '${p.productId}')">
                    <i class="fa-solid fa-xmark"></i>
                </button>
                <div class="catalog-card-thumb">
                    ${p.image
                        ? `<img src="${p.image}" alt="${CF.escapeHtml(p.name)}" />`
                        : `<div class="catalog-card-placeholder"><i class="fa-solid fa-shirt"></i></div>`}
                </div>
                <div class="catalog-card-body">
                    <span class="catalog-card-category">${CF.escapeHtml(p.category || "Item")}</span>
                    <span class="catalog-card-name">${CF.escapeHtml(p.name)}</span>
                    <span class="catalog-card-price">${CF.peso(p.price)}</span>
                    <button class="cf-wish-addcart" onclick="cfWishAddCart(event, '${p.productId}')">
                        <i class="fa-solid fa-cart-plus"></i> Add to Cart
                    </button>
                </div>
            </div>
        `).join("");
    }

    function cfRemoveWish(event, id) {
        event.preventDefault();
        CF.removeFromWishlist(id);
        renderWishlist();
        CF.toast("Removed from wishlist", { icon: "fa-heart-crack", type: "info" });
    }

    function cfWishAddCart(event, id) {
        event.preventDefault();
        const item = CF.getWishlist().find(w => String(w.productId) === String(id));
        if (!item) return;
        CF.addToCart({
            productId: item.productId, name: item.name, price: item.price, image: item.image,
            qty: 1, size: "", color: "", service: "", servicePrice: 0, printLocation: ""
        });
        CF.toast("Added to cart", { icon: "fa-cart-plus" });
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
        const pageData = window.CF_WISHLIST_PAGE || {};
        CF.wireSearchBar(document.getElementById("navSearchInput"), pageData.homeUrl || "/", null);
        renderWishlist();
    });

    // Expose handlers referenced by inline onclick= attributes in the view
    window.toggleAccountMenu = toggleAccountMenu;
    window.cfRemoveWish = cfRemoveWish;
    window.cfWishAddCart = cfWishAddCart;

})();
