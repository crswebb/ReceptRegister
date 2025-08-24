// toggle-tried.js (#39) - attach click handler to tried cells (progressive)
document.addEventListener('click', async e => {
  const btn = e.target.closest('[data-toggle-tried]');
  if (!btn) return;
  e.preventDefault();
  const id = btn.getAttribute('data-id');
  const tried = btn.getAttribute('data-tried') === 'true';
  const apiBase = document.querySelector('meta[name="api-base"]')?.content || '';
  const res = await fetch(`${apiBase}/recipes/${id}/tried?tried=${!tried}`, { method:'PATCH' });
  if (res.ok) {
    btn.setAttribute('data-tried', String(!tried));
    btn.textContent = !tried ? '✔' : '';
  }
});