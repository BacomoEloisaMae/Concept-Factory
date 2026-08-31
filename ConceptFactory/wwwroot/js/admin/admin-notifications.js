// ConceptFactory — Admin notification bell (sidebar badge + topbar
// dropdown, both fed by NotificationsController) and the nIndex row
// "mark read then go to that order" behavior. Loaded site-wide from
// _AdminLayout.cshtml so the badge/dropdown work on every admin page.

function cfUpdateAdminBadges(count) {
    const sidebarBadge = document.getElementById('navAdminNotifBadge');
    const topBadge = document.getElementById('adminBellBadge');
    [sidebarBadge, topBadge].forEach(el => {
        if (!el) return;
        if (count > 0) {
            el.textContent = count > 99 ? '99+' : String(count);
            el.style.display = 'flex';
        } else {
            el.style.display = 'none';
        }
    });
}

function cfRefreshAdminBadge() {
    fetch('/Notifications/nBadgeCount', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(res => res.ok ? res.json() : null)
        .then(data => { if (data) cfUpdateAdminBadges(data.count); })
        .catch(() => { });
}

function toggleAdminNotifDropdown() {
    const dropdown = document.getElementById('adminNotifDropdown');
    if (!dropdown) return;
    const opening = !dropdown.classList.contains('open');
    dropdown.classList.toggle('open');
    if (opening) {
        dropdown.innerHTML = '<div class="admin-notif-dropdown-loading"><i class="fa-solid fa-spinner fa-spin"></i> Loading…</div>';
        fetch('/Notifications/nDropdown', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(res => res.text())
            .then(html => { dropdown.innerHTML = html; })
            .catch(() => { dropdown.innerHTML = '<div class="admin-notif-dropdown-loading">Could not load notifications.</div>'; });
    }
}

function cfOpenAdminNotification(notificationId, orderId) {
    const form = new FormData();
    const tokenEl = document.querySelector('#cfAdminAntiForgeryForm input[name="__RequestVerificationToken"]');
    if (tokenEl) form.append('__RequestVerificationToken', tokenEl.value);

    fetch('/Notifications/nMarkRead/' + notificationId, {
        method: 'POST',
        body: form,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).finally(() => {
        window.location.href = '/Orders/oDetails/' + orderId;
    });
}
window.cfOpenAdminNotification = cfOpenAdminNotification;
window.toggleAdminNotifDropdown = toggleAdminNotifDropdown;

document.addEventListener('click', function (e) {
    const wrap = document.getElementById('adminBellWrap');
    const dropdown = document.getElementById('adminNotifDropdown');
    if (wrap && dropdown && !wrap.contains(e.target)) {
        dropdown.classList.remove('open');
    }
});

// nIndex.cshtml rows: clicking one marks it read (fetch), then follows
// its data-redirect to the order — same pattern as the dropdown above,
// just intercepting a real <form> instead of an onclick.
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (!form.matches('[data-notif-form]')) return;
    e.preventDefault();
    const formData = new FormData(form);
    const redirectUrl = form.getAttribute('data-redirect');
    fetch(form.getAttribute('action'), {
        method: 'POST',
        body: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).finally(() => {
        if (redirectUrl) window.location.href = redirectUrl;
        else window.location.reload();
    });
});

cfRefreshAdminBadge();
setInterval(cfRefreshAdminBadge, 30000);
