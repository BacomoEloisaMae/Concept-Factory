/* ============================================================
   PRODUCTS-BROWSE.JS
   Page logic unique to Views/Home/hProducts.cshtml.
   Shared nav/account-menu/cart handlers are already
   provided by home-index.js (reused on this page too), and
   storefront.js (window.CF) is loaded globally via _Layout.
============================================================ */
function applyBrowseSort(val) {
    const url = new URL(window.location.href);
    if (val) url.searchParams.set('sort', val);
    else url.searchParams.delete('sort');
    window.location.href = url.toString();
}
