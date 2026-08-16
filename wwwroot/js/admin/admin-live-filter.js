// ConceptFactory — Admin: Live Filters
//
// Connected to (each includes this script alongside its own page script):
//   Views/Products/pIndex.cshtml    (+ products-index.js)
//   Views/Products/pDeleted.cshtml  (+ products-deleted.js)
//   Views/Services/sIndex.cshtml    (+ services-index.js)
//   Views/Services/sDeleted.cshtml  (+ services-deleted.js)
// Targets any ".filter-form" on the page — its "select.filter-select"
// dropdowns and ".search-input" text box — so no controller changes were
// needed to add this.
//
// Shared by every admin list page that has a ".filter-form" (Items,
// Deleted Items, Services, Deleted Services): auto-submits the form as
// the admin types in the search box (debounced, so it doesn't fire on
// every keystroke) or the instant a dropdown (category/status) changes —
// so results update on their own, no "Filter" button click needed.
(function () {
    "use strict";

    const FOCUS_FLAG = "cfAdminFilterFocusSearch";

    document.querySelectorAll(".filter-form").forEach(function (form) {
        const searchInput = form.querySelector(".search-input");
        let debounceTimer = null;

        if (searchInput) {
            searchInput.addEventListener("input", function () {
                clearTimeout(debounceTimer);
                debounceTimer = setTimeout(function () {
                    sessionStorage.setItem(FOCUS_FLAG, "1");
                    form.submit();
                }, 450);
            });
        }

        form.querySelectorAll("select.filter-select").forEach(function (select) {
            select.addEventListener("change", function () { form.submit(); });
        });
    });

    // After a search-triggered reload, put focus back in the search box
    // (cursor at the end) so typing feels continuous instead of the
    // field losing focus on every page refresh.
    if (sessionStorage.getItem(FOCUS_FLAG)) {
        sessionStorage.removeItem(FOCUS_FLAG);
        const input = document.querySelector(".filter-form .search-input");
        if (input) {
            input.focus();
            const len = input.value.length;
            input.setSelectionRange(len, len);
        }
    }
})();
