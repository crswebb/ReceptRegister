// Progressive enhancement: evaluate password strength client-side
const pwd = document.getElementById('Password');
const strength = document.getElementById('pwStrength');
if (pwd && strength) {
  function score(p) {
    let s = 0;
    if (p.length >= 8) s++;
    if (p.length >= 12) s++;
    if (/[a-z]/.test(p)) s++;
    if (/[A-Z]/.test(p)) s++;
    if (/\d/.test(p)) s++;
    if (/[^A-Za-z0-9]/.test(p)) s++;
    return s;
  }
  function label(s) {
    if (s <= 2) return 'Weak';
    if (s <= 4) return 'Fair';
    if (s === 5) return 'Good';
    return 'Strong';
  }
  function update() {
    const val = pwd.value;
    if (!val) { strength.textContent = ''; return; }
    const s = score(val);
    strength.textContent = `Strength: ${label(s)}`;
    strength.className = 'form-text pw-strength-' + label(s).toLowerCase();
  }
  pwd.addEventListener('input', update);
}
