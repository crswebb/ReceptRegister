// Enhanced progressive password strength meter (mirrors server PasswordStrength.Evaluate)
// Score scale 0-6: length(8), length(12), lower, upper, digit, symbol
const pwd = document.getElementById('Password');
const strengthEl = document.getElementById('pwStrength');
const suggestionsEl = document.getElementById('pwSuggestions');
const barFill = document.getElementById('pwBarFill');

if (pwd && strengthEl && suggestionsEl && barFill) {
  const unmetMessages = {
    len8: 'Use at least 8 characters',
    len12: 'Use 12+ characters',
    lower: 'Add a lowercase letter',
    upper: 'Add an uppercase letter',
    digit: 'Add a digit',
    symbol: 'Add a symbol'
  };

  function evaluate(p) {
    if (!p) return { score: 0, label: '', suggestions: [] };
    let score = 0;
    const suggestions = [];
    if (p.length >= 8) score++; else suggestions.push(unmetMessages.len8);
    if (p.length >= 12) score++; else suggestions.push(unmetMessages.len12);
    if (/[a-z]/.test(p)) score++; else suggestions.push(unmetMessages.lower);
    if (/[A-Z]/.test(p)) score++; else suggestions.push(unmetMessages.upper);
    if (/\d/.test(p)) score++; else suggestions.push(unmetMessages.digit);
    if (/[^A-Za-z0-9]/.test(p)) score++; else suggestions.push(unmetMessages.symbol);
    let label;
    if (score <= 2) label = 'Weak';
    else if (score <= 4) label = 'Fair';
    else if (score === 5) label = 'Good';
    else label = 'Strong';
    return { score, label, suggestions };
  }

  let debounceTimer = 0;
  function render() {
    const v = pwd.value;
    const { score, label, suggestions } = evaluate(v);
    if (!v) {
      strengthEl.textContent = '';
      suggestionsEl.textContent = '';
      barFill.style.width = '0%';
      barFill.className = 'pw-meter-fill';
      return;
    }
    strengthEl.textContent = `Strength: ${label}`;
    strengthEl.className = 'form-text pw-strength-' + label.toLowerCase();
    // Bar: width proportion (score/6)
    const pct = (score / 6) * 100;
    barFill.style.width = pct + '%';
    barFill.className = 'pw-meter-fill pw-score-' + score;
    // Suggestions: show first 3 unmet to avoid overwhelm, hide if strong/good (score>=5)
    if (score >= 5) {
      suggestionsEl.textContent = '';
    } else {
      const trimmed = suggestions.slice(0, 3);
      suggestionsEl.textContent = trimmed.join(' • ');
    }
  }

  function queueRender() {
    window.clearTimeout(debounceTimer);
    debounceTimer = window.setTimeout(render, 150);
  }

  pwd.addEventListener('input', queueRender);
}

