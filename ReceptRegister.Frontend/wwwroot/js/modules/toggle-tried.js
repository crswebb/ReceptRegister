// toggle-tried.js (#39) - progressive enhancement for toggling tried status with accessible feedback
// Uses POST /recipes/{id}/tried with JSON body: { "id": number, "tried": true|false }
// Updates button state, aria-pressed, and provides a polite status message for assistive tech.
document.addEventListener('click', async e => {
  const btn = e.target.closest('[data-toggle-tried]');
  if (!btn) return;
  e.preventDefault();
  const id = btn.getAttribute('data-id');
  const tried = btn.getAttribute('data-tried') === 'true';
  const apiBase = document.querySelector('meta[name="api-base"]')?.content || '';

  // Reusable status region (polite) appended lazily.
  let statusRegion = document.getElementById('toggle-tried-status');
  if (!statusRegion) {
    statusRegion = document.createElement('div');
    statusRegion.id = 'toggle-tried-status';
    statusRegion.setAttribute('role','status');
    statusRegion.setAttribute('aria-live','polite');
    statusRegion.className = 'visually-hidden toggle-tried-status';
    document.body.appendChild(statusRegion);
  }
  const showStatus = (msg, isError = false) => {
    statusRegion.textContent = msg;
    statusRegion.classList.toggle('error', !!isError);
    statusRegion.classList.remove('visually-hidden');
    if (!isError) {
      setTimeout(() => {
        statusRegion.textContent = '';
        statusRegion.classList.add('visually-hidden');
      }, 4000);
    }
  };

  try {
    const newValue = !tried;
    const res = await fetch(`${apiBase}/api/recipes/${id}/tried`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ id: Number(id), tried: newValue })
    });
    if (!res.ok) {
      showStatus('Failed to update recipe status. Try again.', true);
      return;
    }
    // Update UI only on success
    btn.setAttribute('data-tried', String(newValue));
    btn.setAttribute('aria-pressed', String(newValue));
    btn.classList.toggle('is-tried', newValue);
    const icon = btn.querySelector('.tried-indicator');
    if (icon) icon.textContent = newValue ? '✔' : '';
    // If button itself shows text instead of nested icon
    if (!icon) btn.textContent = newValue ? '✔' : '';
    showStatus(`Recipe marked as ${newValue ? 'tried' : 'untried'}.`);
  } catch (err) {
    showStatus('Network error updating recipe.', true);
  }
});