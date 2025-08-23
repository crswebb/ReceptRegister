// search-enhance.js (#38) - progressive enhancement for recipe search (debounced)
const form = document.querySelector('form[role="search"]');
if (form) {
  const input = form.querySelector('input[name="Search"]');
  const tableBody = document.querySelector('.recipes-table tbody');
  const countEl = document.querySelector('.result-count');
  let timer;
  const apiBase = document.querySelector('meta[name="api-base"]')?.content || '';
  const doSearch = async () => {
    const q = input.value.trim();
    const url = apiBase + '/recipes/?search=' + encodeURIComponent(q);
    const res = await fetch(url);
    if (!res.ok) return; // silent fail
    const data = await res.json();
    tableBody.innerHTML = '';
    for (const r of data) {
      const tr = document.createElement('tr');
      tr.innerHTML = `<td><a href="/Recipes/Detail/${r.id}">${r.name}</a></td>`+
        `<td>${r.book}</td><td>${r.page}</td>`+
        `<td>${r.categories.length? `<ul class='inline-list'>${r.categories.map(c=>`<li>${c}</li>`).join('')}</ul>` : '<span class="muted">—</span>'}</td>`+
        `<td>${r.keywords.length? `<ul class='inline-list'>${r.keywords.map(k=>`<li>${k}</li>`).join('')}</ul>` : '<span class="muted">—</span>'}</td>`+
        `<td>${r.tried? '✔': ''}</td>`;
      tableBody.appendChild(tr);
    }
    if (countEl) countEl.textContent = `${data.length} recipe${data.length===1?'':'s'} found.`;
  };
  input?.addEventListener('input', () => {
    clearTimeout(timer);
    timer = setTimeout(doSearch, 300);
  });
}