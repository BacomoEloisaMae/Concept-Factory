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

// Policy (mirrors Utils/PasswordPolicy.cs on the server — server-side is
// the real gate, this is just live feedback): 8+ chars, an uppercase
// letter, a lowercase letter, a number, and NO special characters.
function checkStrength(val) {
    const bar = document.getElementById('pwStrength');
    const reqList = document.getElementById('pwRequirements');
    val = val || '';

    const hasLength = val.length >= 8;
    const hasUpper = /[A-Z]/.test(val);
    const hasLower = /[a-z]/.test(val);
    const hasNumber = /[0-9]/.test(val);
    const noSpecial = val.length === 0 || /^[A-Za-z0-9]+$/.test(val);

    if (reqList) {
        setReq('pwReqLength', hasLength);
        setReq('pwReqUpper', hasUpper);
        setReq('pwReqLower', hasLower);
        setReq('pwReqNumber', hasNumber);
        setReq('pwReqNoSpecial', noSpecial);
    }

    if (!bar) return;
    bar.className = 'pw-strength';
    if (!val) return;

    const valid = hasLength && hasUpper && hasLower && hasNumber && noSpecial;
    if (valid) bar.classList.add('strong');
    else if (val.length >= 6 && (hasUpper || hasNumber)) bar.classList.add('medium');
    else bar.classList.add('weak');
}

function setReq(id, met) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.toggle('pw-req-met', met);
    const icon = el.querySelector('i');
    if (icon) icon.className = met ? 'fa-solid fa-circle-check' : 'fa-regular fa-circle';
}
