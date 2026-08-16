// ConceptFactory — Admin: Production Station "Mark as Done" popup
// (pStation.cshtml). Replaces the old shared stepper modal
// (production-progress.js) on the per-station pages only — the Overview
// page (pIndex.cshtml) still uses that one for its full 6-stage view.

const stationModalBackdrop = document.getElementById('stationModalBackdrop');
const stationModal = document.getElementById('stationModal');

function openStationModal(orderId, stage) {
    stationModal.innerHTML = '<div class="billing-modal-loading"><i class="fa-solid fa-spinner fa-spin"></i> Loading…</div>';
    stationModalBackdrop.classList.add('open');
    document.body.style.overflow = 'hidden';

    fetch('/Production/pStationModal?id=' + orderId + '&stage=' + stage, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(res => {
            if (!res.ok) throw new Error('Failed to load order');
            return res.text();
        })
        .then(html => { stationModal.innerHTML = html; })
        .catch(() => {
            stationModal.innerHTML = '<div class="billing-modal-loading"><i class="fa-solid fa-triangle-exclamation"></i> Could not load this order. Please try again.</div>';
        });
}

function closeStationModal() {
    stationModalBackdrop.classList.remove('open');
    document.body.style.overflow = '';
}

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && stationModalBackdrop.classList.contains('open')) closeStationModal();
});

// ── Live progress bar + char counter (event-delegated since the fields
// are injected into the modal after the page has already loaded) ──
document.addEventListener('input', function (e) {
    if (e.target.id === 'stationQtyInput') {
        const input = e.target;
        const target = parseInt(input.max, 10) || 0;
        let value = parseInt(input.value, 10);
        if (isNaN(value)) value = 0;
        if (value > target) { value = target; input.value = target; }
        if (value < 0) { value = 0; input.value = 0; }

        const pct = target > 0 ? Math.round((value / target) * 100) : 0;
        const fill = document.getElementById('stationProgressFill');
        const pctLabel = document.getElementById('stationProgressPct');
        if (fill) fill.style.width = pct + '%';
        if (pctLabel) pctLabel.textContent = pct + '%';

        const submitBtn = document.getElementById('stationSubmitBtn');
        if (submitBtn) submitBtn.disabled = value < target;
    }

    if (e.target.id === 'stationRemarks') {
        const countEl = document.getElementById('stationRemarksCount');
        if (countEl) countEl.textContent = e.target.value.length;
    }
});

// ── Submit (both "Mark as Done" and "Mark as Picked Up" forms) ──
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (form.id !== 'stationForm') return;
    e.preventDefault();

    const mode = form.dataset.mode;
    const submitBtn = form.querySelector('button[type="submit"]');
    const fd = new FormData(form); // picks up the antiforgery token already rendered inside the form
    fd.append('id', form.dataset.id);

    let url = '/Production/pMarkCompleted';
    if (mode === 'complete-station') {
        url = '/Production/pCompleteStation';
        const qtyInput = document.getElementById('stationQtyInput');
        const remarksInput = document.getElementById('stationRemarks');
        fd.append('stage', form.dataset.stage);
        fd.append('quantity', qtyInput ? qtyInput.value : form.dataset.target);
        fd.append('remarks', remarksInput ? remarksInput.value : '');
    }

    if (submitBtn) { submitBtn.disabled = true; submitBtn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Saving…'; }

    fetch(url, {
        method: 'POST',
        body: fd,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
        .then(res => {
            if (!res.ok) return res.text().then(msg => { throw new Error(msg || 'Something went wrong.'); });
            return res.json().catch(() => ({}));
        })
        .then(() => {
            closeStationModal();
            window.location.reload();
        })
        .catch(err => {
            if (submitBtn) { submitBtn.disabled = false; submitBtn.innerHTML = mode === 'complete-station' ? '<i class="fa-solid fa-check"></i> Mark as Done' : '<i class="fa-solid fa-circle-check"></i> Mark as Picked Up'; }
            alert(err.message || 'Something went wrong. Please try again.');
        });
});
