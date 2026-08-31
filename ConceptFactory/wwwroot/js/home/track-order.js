// ConceptFactory — Track Order (hTrackOrder.cshtml). Clicking a row
// expands/collapses the production-stages panel rendered directly under
// it (server-rendered per order into a hidden <tr>, see
// _ProductionStagesReadonly.cshtml) — no AJAX needed, the content's
// already on the page.

function toggleTrackOrderRow(orderId) {
    const detailRow = document.getElementById('track-detail-' + orderId);
    const chevron = document.getElementById('track-chevron-' + orderId);
    if (!detailRow) return;

    const opening = detailRow.style.display === 'none' || detailRow.style.display === '';

    // Only one order's detail open at a time, accordion-style.
    document.querySelectorAll('.track-order-detail-row').forEach(row => { row.style.display = 'none'; });
    document.querySelectorAll('.track-row-chevron').forEach(c => c.classList.remove('open'));

    if (opening) {
        detailRow.style.display = 'table-row';
        if (chevron) chevron.classList.add('open');
    }
}
window.toggleTrackOrderRow = toggleTrackOrderRow;
