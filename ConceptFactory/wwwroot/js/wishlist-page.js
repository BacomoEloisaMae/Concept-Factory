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
                    <button class="cf-wish-addcart" onclick="cfWishViewItem(event, '${p.productId}')">
                        <i class="fa-solid fa-eye"></i> View Item
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

    function cfWishViewItem(event, id) {
        event.preventDefault();
        const pageData = window.CF_WISHLIST_PAGE || {};
        const template = pageData.detailsUrl || "/Home/hDetails/0";
        // Swap the trailing placeholder id (0) in the generated route for the real product id
        window.location.href = template.replace(/\/0(\/?$|\?)/, "/" + id + "$1");
    }

    document.addEventListener("DOMContentLoaded", function () {
        const pageData = window.CF_WISHLIST_PAGE || {};
        CF.wireSearchBar(document.getElementById("navSearchInput"), pageData.homeUrl || "/", null);
        renderWishlist();
    });

    // Expose handlers referenced by inline onclick= attributes in the view
    window.cfRemoveWish = cfRemoveWish;
    window.cfWishViewItem = cfWishViewItem;

})();
