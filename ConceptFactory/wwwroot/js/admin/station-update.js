// ConceptFactory — Admin: Production Station "Mark as Done" popup
// (pStation.cshtml). Replaces the old shared stepper modal
// (production-progress.js) on the per-station pages only — the Overview
// page (pIndex.cshtml) still uses that one for its full 6-stage view.
//
// Quantity is tracked PER LINE ITEM (.station-item-qty-input, one input
// per color/size variant), not as a single lumped total — an order
// combining 5x Red-Small and 3x Blue-Large needs staff to confirm each
// separately, not just "8 pcs" somewhere. See _StationUpdateModal.cshtml
// and ProductionController.pSaveStationProgress/pCompleteStation.

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
    // Flush any pending autosave immediately rather than losing the last
    // edit to the debounce timer when the modal closes right after typing.
    if (document.querySelector('.station-item-qty-input')) {
        clearTimeout(stationSaveTimer);
        saveStationProgress();
    }
    stationModalBackdrop.classList.remove('open');
    document.body.style.overflow = '';
}

// Reads every per-item quantity input currently in the modal and returns
// both the {orderDetailId: quantity} map (for the server) and the
// aggregate sum/target/completeness (for the progress bar + submit button).
function collectItemQuantities() {
    const inputs = document.querySelectorAll('.station-item-qty-input');
    const itemQuantities = {};
    let sumValue = 0, sumTarget = 0, allComplete = true;

    inputs.forEach(input => {
        const itemTarget = parseInt(input.max, 10) || 0;
        let value = parseInt(input.value, 10);
        if (isNaN(value)) value = 0;
        if (value > itemTarget) { value = itemTarget; input.value = itemTarget; }
        if (value < 0) { value = 0; input.value = 0; }

        itemQuantities[input.dataset.detailId] = value;
        sumValue += value;
        sumTarget += itemTarget;
        if (value < itemTarget) allComplete = false;
    });

    return { itemQuantities, sumValue, sumTarget, allComplete, count: inputs.length };
}

// ── Autosave the in-progress quantities as the staff member types, WITHOUT
// advancing the station — that still only happens via the "Mark as Done"
// submit below. Debounced so it doesn't fire a request per keystroke;
// closeStationModal() flushes it immediately so a quick edit-then-close
// isn't lost. Silent on failure — worst case the next edit or "Mark as
// Done" click saves it instead. ──
let stationSaveTimer = null;

function saveStationProgress() {
    const form = document.getElementById('stationForm');
    if (!form || form.dataset.mode !== 'complete-station') return;

    const { itemQuantities } = collectItemQuantities();
    const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
    const fd = new FormData();
    fd.append('id', form.dataset.id);
    fd.append('stage', form.dataset.stage);
    fd.append('itemQuantitiesJson', JSON.stringify(itemQuantities));
    if (tokenInput) fd.append('__RequestVerificationToken', tokenInput.value);

    fetch('/Production/pSaveStationProgress', {
        method: 'POST',
        body: fd,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
        .then(res => res.ok ? res.json() : null)
        .then(data => handleQuantityChange(form, data))
        .catch(() => { /* silent — see comment above */ });
}

// Row badge (Cutting only) + "In Progress" / "Pending from Previous" stat
// cards all read straight off the DB at page load, so they go stale the
// moment this autosave changes what those queries would return. Mirrors
// ProductionController's pendingFromPreviousQuery: every station now keys
// "pending" off whether any quantity has been entered yet. Cutting's badge
// additionally flips off Status, since that's the one field the backend
// actually changes there (pSaveStationProgress).
function handleQuantityChange(form, data) {
    if (!data) return;
    const row = document.getElementById('station-row-' + form.dataset.id);
    if (!row) return;

    const stage = parseInt(form.dataset.stage, 10);
    const isCuttingStage = stage === 0;
    const hasProgress = (data.quantity || 0) > 0;

    if (hasProgress && row.dataset.pending === 'true') {
        row.dataset.pending = 'false';
        bumpStat('stationStatPending', -1);
        bumpStat('stationStatInProgress', 1);
    } else if (!hasProgress && row.dataset.pending === 'false') {
        // "Pending" is purely quantity-driven at every station now, so
        // clearing the field back to 0 genuinely moves it back — separate
        // from the badge below, which only ever moves forward because the
        // backend's Status flip (Cutting only) doesn't reverse either.
        row.dataset.pending = 'true';
        bumpStat('stationStatPending', 1);
        bumpStat('stationStatInProgress', -1);
    }

    if (isCuttingStage && data.status && data.status !== 'Confirmed') {
        const badge = row.querySelector('.status-badge');
        if (badge && !badge.classList.contains('status-inproduction')) {
            badge.textContent = 'In ' + (form.dataset.stationName || 'Production');
            badge.classList.remove('status-confirmed');
            badge.classList.add('status-inproduction');
        }
    }
}

function bumpStat(elementId, delta) {
    const el = document.getElementById(elementId);
    if (!el) return;
    el.textContent = Math.max(0, (parseInt(el.textContent, 10) || 0) + delta);
}

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && stationModalBackdrop.classList.contains('open')) closeStationModal();
});

// ── Live aggregate progress bar + submit-button lock + char counter
// (event-delegated since the fields are injected into the modal after
// the page has already loaded) ──
document.addEventListener('input', function (e) {
    if (e.target.classList && e.target.classList.contains('station-item-qty-input')) {
        const { sumValue, sumTarget, allComplete, count } = collectItemQuantities();
        if (count > 0) {
            const pct = sumTarget > 0 ? Math.round((sumValue / sumTarget) * 100) : 0;
            const fill = document.getElementById('stationProgressFill');
            const pctLabel = document.getElementById('stationProgressPct');
            if (fill) fill.style.width = pct + '%';
            if (pctLabel) pctLabel.textContent = pct + '%';

            const submitBtn = document.getElementById('stationSubmitBtn');
            if (submitBtn) submitBtn.disabled = !allComplete;
        }

        clearTimeout(stationSaveTimer);
        stationSaveTimer = setTimeout(saveStationProgress, 600);
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
        const { itemQuantities } = collectItemQuantities();
        const remarksInput = document.getElementById('stationRemarks');
        fd.append('stage', form.dataset.stage);
        fd.append('itemQuantitiesJson', JSON.stringify(itemQuantities));
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
