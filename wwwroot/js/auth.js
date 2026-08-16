// ConceptFactory — Auth (Login & Register)
// Shared password-visibility toggle. checkStrength is only wired up on the
// Register page's markup, but is harmless to include on Login as well.

function togglePw(inputId, btn) {
    const input = document.getElementById(inputId);
    const icon = btn.querySelector('i');
    if (input.type === 'password') {
        input.type = 'text';
        icon.className = 'fa-regular fa-eye-slash';
    } else {
        input.type = 'password';
        icon.className = 'fa-regular fa-eye';
    }
}

function checkStrength(val) {
    const bar = document.getElementById('pwStrength');
    if (!bar) return;
    bar.className = 'pw-strength';
    if (!val) return;
    const strong = val.length >= 8 && /[A-Z]/.test(val) && /[0-9]/.test(val) && /[^A-Za-z0-9]/.test(val);
    const medium = val.length >= 6 && (/[A-Z]/.test(val) || /[0-9]/.test(val));
    if (strong) bar.classList.add('strong');
    else if (medium) bar.classList.add('medium');
    else bar.classList.add('weak');
}
