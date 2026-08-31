// ConceptFactory — customer navbar notification bell (_Layout.cshtml).
// Same no-real-accounts-yet matching as Track Order — see
// HomeController.MyNotificationsQueryAsync.

function cfUpdateNavNotifBadge(count) {
    const badge = document.getElementById('navNotifBadge');
    if (!badge) return;
    if (count > 0) {
        badge.textContent = count > 99 ? '99+' : String(count);
        badge.style.display = 'flex';
    } else {
        badge.style.display = 'none';
    }
}

function cfRefreshNavNotifBadge() {
    fetch('/Home/hNotificationsBadge', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(res => res.ok ? res.json() : null)
        .then(data => { if (data) cfUpdateNavNotifBadge(data.count); })
        .catch(() => { });
}

function toggleNavNotifDropdown() {
    const dropdown = document.getElementById('navNotifDropdown');
    if (!dropdown) return;
    const opening = !dropdown.classList.contains('open');
    dropdown.classList.toggle('open');
    if (opening) {
        dropdown.innerHTML = '<div class="nav-notif-dropdown-loading"><i class="fa-solid fa-spinner fa-spin"></i> Loading…</div>';
        fetch('/Home/hNotificationsDropdown', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(res => res.text())
            .then(html => { dropdown.innerHTML = html; })
            .catch(() => { dropdown.innerHTML = '<div class="nav-notif-dropdown-loading">Could not load notifications.</div>'; });
    }
}
window.toggleNavNotifDropdown = toggleNavNotifDropdown;

function cfOpenCustomerNotification(notificationId, orderId) {
    const tokenEl = document.querySelector('#cfSiteAntiForgeryForm input[name="__RequestVerificationToken"]');
    const formData = new FormData();
    if (tokenEl) formData.append('__RequestVerificationToken', tokenEl.value);

    fetch('/Home/hMarkNotificationRead/' + notificationId, {
        method: 'POST',
        body: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).finally(() => {
        window.location.href = '/Home/hTrackOrder/' + orderId;
    });
}
window.cfOpenCustomerNotification = cfOpenCustomerNotification;

document.addEventListener('click', function (e) {
    const wrap = document.getElementById('navNotifWrap');
    const dropdown = document.getElementById('navNotifDropdown');
    if (wrap && dropdown && !wrap.contains(e.target)) {
        dropdown.classList.remove('open');
    }
});

// hNotifications.cshtml full list — mark read then follow to the order.
document.addEventListener('submit', function (e) {
    const form = e.target;
    if (!form.matches('[data-cf-notif-form]')) return;
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

cfRefreshNavNotifBadge();
setInterval(cfRefreshNavNotifBadge, 30000);
