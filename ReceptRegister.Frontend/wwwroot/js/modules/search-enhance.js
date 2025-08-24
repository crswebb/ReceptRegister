// search-enhance.js (#38) - progressive enhancement for recipe search (debounced)
const form = document.querySelector('form[role="search"]');
if (form) {
  const input = form.querySelector('input[name="Search"]');
  const tableBody = document.querySelector('.recipes-table tbody');
  const countEl = document.querySelector('.result-count');
  let timer;
  const apiBase = document.querySelector('meta[name="api-base"]')?.content || '';
  // Create (or reuse) a lightweight status region for errors (aria-live polite)
  let statusRegion = document.querySelector('#search-status');
  if (!statusRegion) {
    statusRegion = document.createElement('div');
    statusRegion.id = 'search-status';
    statusRegion.setAttribute('role','status');
    statusRegion.setAttribute('aria-live','polite');
    statusRegion.className = 'search-status visually-hidden';
    form.appendChild(statusRegion);
  }

  const renderError = (msg) => {
    if (!tableBody) return;
    tableBody.innerHTML = `<tr><td colspan="6" class="error-message">${msg}</td></tr>`;
    if (countEl) countEl.textContent = 'Search failed.';
    if (statusRegion) {
      statusRegion.classList.remove('visually-hidden');
      statusRegion.textContent = msg;
    }
  };

  const doSearch = async () => {
  const q = input.value.trim();
  const url = apiBase + '/api/recipes/?query=' + encodeURIComponent(q);
    try {
      const res = await fetch(url);
      if (!res.ok) {
        renderError('Search unavailable. Please try again later.');
        return;
      }
  const data = await res.json();
  // Support either new paged { items, totalItems } or legacy array
  let items = [];
  if (Array.isArray(data)) {
    items = data;
  } else if (Array.isArray(data.items)) {
    items = data.items;
  } else if (Array.isArray(data.Items)) { // defensive in case of PascalCase
    items = data.Items;
  }
  tableBody.innerHTML = '';
  for (const r of items) {
        const tr = document.createElement('tr');
        tr.innerHTML = `<td><a href="/Recipes/Detail/${r.id}">${r.name}</a></td>`+
          `<td>${r.book}</td><td>${r.page}</td>`+
          `<td>${r.categories.length? `<ul class='inline-list'>${r.categories.map(c=>`<li>${c}</li>`).join('')}</ul>` : '<span class="muted">—</span>'}</td>`+
          `<td>${r.keywords.length? `<ul class='inline-list'>${r.keywords.map(k=>`<li>${k}</li>`).join('')}</ul>` : '<span class="muted">—</span>'}</td>`+
          `<td>${r.tried? '✔': ''}</td>`;
        tableBody.appendChild(tr);
      }
      if (countEl) {
        countEl.textContent = `${items.length} of ${data.totalItems ?? items.length} recipe${(data.totalItems ?? items.length)===1?'':'s'} shown.`;
        const live = document.getElementById('search-results-count');
        if (live) live.textContent = (countEl.textContent || '').replace(' shown.', ' listed.');
      }
      // Clear prior error status if any
      if (statusRegion) {
        statusRegion.textContent = '';
        statusRegion.classList.add('visually-hidden');
      }
    } catch (err) {
      renderError('Network error. Please check your connection.');
    }
  };
  input?.addEventListener('input', () => {
    clearTimeout(timer);
    timer = setTimeout(doSearch, 300);
  });
}