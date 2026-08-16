// ConceptFactory — Admin: Production (pIndex.cshtml Overview + pStation.cshtml
// per-station pages both use this — same modal markup/IDs, same
// "click a stage to mark it done" / "Mark Completed" form pattern.

const prodModalBackdrop = document.getElementById('prodModalBackdrop');
const prodModal = document.getElementById('prodModal');

function loadProductionDetails(orderId, readOnly) {
    prodModal.innerHTML = '<div class="billing-modal-loading"><i class="fa-solid fa-spinner fa-spin"></i> Loading production details…</div>';
    prodModalBackdrop.classList.add('open');
    document.body.style.overflow = 'hidden';

    const url = '/Production/pDetailsPanel/' + orderId + (readOnly ? '?readOnly=true' : '');
    fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(res => {
            if (!res.ok) throw new Error('Failed to load order');
            return res.text();
        })
        .then(html => { prodModal.innerHTML = html; })
        .catch(() => {
            prodModal.innerHTML = '<div class="billing-modal-loading"><i class="fa-solid fa-triangle-exclamation"></i> Could not load this order. Please try again.</div>';
        });
}

function closeProductionModal() {
    prodModalBackdrop.classList.remove('open');
    document.body.style.overflow = '';
}

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && prodItemModalBackdrop.classList.contains('open')) { closeProdItemModal(); return; }
    if (e.key === 'Escape' && prodModalBackdrop.classList.contains('open')) closeProductionModal();
});

// ── Per-item detail popup ── built client-side from the "View" button's
// data-* attributes (see _ProductionPanel.cshtml's item list) — no server
// round trip needed, the data's already rendered into the button.
const prodItemModalBackdrop = document.getElementById('prodItemModalBackdrop');
const prodItemModal = document.getElementById('prodItemModal');

function showProdItemDetail(btn) {
    const d = btn.dataset;
    const esc = s => (s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

    const imageHtml = d.image
        ? `<img src="${esc(d.image)}" alt="${esc(d.name)}" class="prod-item-modal-img" />`
        : `<div class="prod-item-modal-img-placeholder"><i class="fa-solid fa-shirt"></i></div>`;

    const fields = [
        ['Quantity', d.qty],
        ['Size', d.size],
        ['Color', d.color],
        ['Print Location', d.location],
        ['Printing Method', d.service]
    ].filter(([, v]) => v);

    const fieldsHtml = fields.map(([label, v]) =>
        `<div class="prod-item-modal-field"><label>${esc(label)}</label><span>${esc(v)}</span></div>`
    ).join('');

    const notesHtml = d.notes
        ? `<div class="prod-item-modal-notes"><strong>Design Notes:</strong> ${esc(d.notes)}</div>`
        : '';

    prodItemModal.innerHTML = `
        ${imageHtml}
        <div class="prod-item-modal-body">
            <div class="prod-item-modal-title">${esc(d.name)}</div>
            ${fieldsHtml}
            ${notesHtml}
            <button type="button" class="btn btn-outline prod-item-modal-close" onclick="closeProdItemModal()">Close</button>
        </div>
    `;
    prodItemModalBackdrop.classList.add('open');
}

function closeProdItemModal() {
    prodItemModalBackdrop.classList.remove('open');
}

// ── Production Workflow Monitoring stepper (pIndex.cshtml Overview only —
// pStation.cshtml has no #prodMonitorStepper, so this quietly no-ops
// there). Clicking anywhere in an order row recolors this shared stepper
// to that order's progress instead of opening a popup; the "View
// Progress" button still opens the actual modal for advancing stages
// (see the onclick="event.stopPropagation()" on its <td>). Reads
// data-stage/data-status straight off the clicked <tr> — no server
// round trip needed, the data's already rendered into the row.
function selectProdRow(rowEl, orderId) {
    const stepper = document.getElementById('prodMonitorStepper');
    if (!stepper) return;

    document.querySelectorAll('.data-table tbody tr.active-row').forEach(r => r.classList.remove('active-row'));
    rowEl.classList.add('active-row');

    const stage = parseInt(rowEl.dataset.stage, 10);
    const status = rowEl.dataset.status;

    const subtitle = document.getElementById('prodMonitorSubtitle');
    if (subtitle) subtitle.textContent = 'Order #ORD-' + String(orderId).padStart(5, '0') + ' — ' + status;

    const steps = Array.from(stepper.querySelectorAll('.prod-monitor-step'));
    const lines = Array.from(stepper.querySelectorAll('.prod-monitor-line'));

    steps.forEach(function (stepEl) {
        const node = stepEl.dataset.node;
        stepEl.classList.remove('neutral', 'done', 'current', 'pending');

        let state;
        if (node === 'confirm') {
            // Reaching Production at all means the order was confirmed.
            state = 'done';
        } else {
            const idx = parseInt(node, 10);
            const started = status !== 'Confirmed';
            const reached = started && idx <= stage;
            const isCurrent = reached && idx === stage && status === 'In Production';
            state = isCurrent ? 'current' : (reached ? 'done' : 'pending');
        }
        stepEl.classList.add(state);

        const sub = stepEl.querySelector('.prod-monitor-sublabel');
        if (sub) sub.textContent = state === 'done' ? 'Completed' : state === 'current' ? 'In Progress' : 'Waiting';
    });

    lines.forEach(function (lineEl, i) {
        lineEl.classList.toggle('done', steps[i].classList.contains('done'));
    });
}


// Forms inside the modal (set stage / Mark Completed) submit via fetch,
// then the page reloads (preserving tab/search/station, staying wherever
// the admin currently is) with ?open=<id> so the modal re-opens showing
// fresh state.
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (form.matches('[data-prod-form]')) {
        e.preventDefault();
        const confirmMsg = form.getAttribute('data-confirm');
        if (confirmMsg && !confirm(confirmMsg)) return;

        const idInput = form.querySelector('input[name="id"]');
        const orderId = idInput ? idInput.value : null;
        const formData = new FormData(form);
        fetch(form.getAttribute('action'), {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).finally(() => {
            const url = new URL(window.location.href);
            if (orderId) url.searchParams.set('open', orderId);
            window.location.href = url.toString();
        });
    }
});

(function () {
    const params = new URLSearchParams(window.location.search);
    const openId = params.get('open');
    if (openId) {
        loadProductionDetails(openId);
        // Also sync the top stepper (pIndex.cshtml only) so it reflects
        // the just-updated stage once the modal closes.
        const row = document.getElementById('prod-row-' + openId);
        if (row) selectProdRow(row, openId);
    }
})();
