/* ============================================================
   HOME-INDEX.JS
   Page logic for Views/Home/hIndex.cshtml
   Depends on: storefront.js (window.CF), window.CF_HOME (page data)
============================================================ */
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        // Intersection Observer for scroll-reveal on category cards
        const cards = document.querySelectorAll(".cat-card");
        const observer = new IntersectionObserver((entries) => {
            entries.forEach((e, i) => {
                if (e.isIntersecting) {
                    e.target.style.opacity = "0";
                    e.target.style.transform = "translateY(20px)";
                    e.target.style.transition = `opacity .4s ${i * 0.06}s ease, transform .4s ${i * 0.06}s ease, border-color .2s, box-shadow .2s`;
                    requestAnimationFrame(() => {
                        e.target.style.opacity = "1";
                        e.target.style.transform = "translateY(0)";
                    });
                    observer.unobserve(e.target);
                }
            });
        }, { threshold: 0.15 });
        cards.forEach(c => {
            c.style.opacity = "0";
            observer.observe(c);
        });

        // Search bar
        const homeData = window.CF_HOME || {};
        CF.wireSearchBar(
            document.getElementById("navSearchInput"),
            homeData.baseUrl || "/",
            homeData.selectedCategory || null
        );
    });

    // Account dropdown
    function toggleAccountMenu() {
        document.getElementById("navAccountMenu").classList.toggle("open");
    }
    document.addEventListener("click", function (e) {
        const wrap = document.getElementById("navAccountWrap");
        if (wrap && !wrap.contains(e.target)) {
            document.getElementById("navAccountMenu").classList.remove("open");
        }
    });

    // ── Wishlist heart toggle (catalog cards) ──
    function cfToggleFav(event, btn) {
        event.preventDefault();
        event.stopPropagation();
        const product = {
            productId: btn.dataset.id || btn.dataset.cfFav,
            name: btn.dataset.name,
            price: parseFloat(btn.dataset.price) || 0,
            image: btn.dataset.image || "",
            category: btn.dataset.category || ""
        };
        const nowActive = CF.toggleWishlist(product);
        CF.syncWishlistHearts();
        CF.toast(nowActive ? "Added to wishlist" : "Removed from wishlist",
                 { icon: nowActive ? "fa-heart" : "fa-heart-crack", type: nowActive ? "" : "info" });
    }

    // ── Quick add-to-cart toggle (catalog cards) ──
    function cfQuickAdd(event, btn) {
        event.preventDefault();
        event.stopPropagation();
        const nowInCart = CF.toggleCartItem({
            productId: btn.dataset.id,
            name: btn.dataset.name,
            price: parseFloat(btn.dataset.price) || 0,
            image: btn.dataset.image || "",
            size: "", color: "", service: "", servicePrice: 0, printLocation: ""
        });
        CF.toast(nowInCart ? "Added to cart" : "Removed from cart",
                 { icon: nowInCart ? "fa-cart-plus" : "fa-cart-arrow-down", type: nowInCart ? "" : "info" });
    }

    // Expose the handlers referenced by inline onclick= attributes in the view
    window.toggleAccountMenu = toggleAccountMenu;
    window.cfToggleFav = cfToggleFav;
    window.cfQuickAdd = cfQuickAdd;

})();
