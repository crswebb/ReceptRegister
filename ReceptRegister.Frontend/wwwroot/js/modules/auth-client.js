// auth-client.js (#57) - shared CSRF + session helper
// Exports: getCsrf, authFetch, refreshSessionIfNeeded, configure(options)

const state = {
  lastRefresh: 0,
  expiry: null,
  ttl: null,
  thresholdPortion: 0.25,
  refreshUrl: '/auth/refresh'
};

function getCsrf() {
  try { return localStorage.getItem('rr_csrf'); } catch { return null; }
}

async function authFetch(url, options = {}) {
  const opts = { ...options }; opts.headers = { ...(options.headers||{}) };
  if (/^(POST|PUT|PATCH|DELETE)$/i.test(opts.method || '')) {
    const csrf = getCsrf();
    if (csrf) opts.headers['X-CSRF-TOKEN'] = csrf;
  }
  const res = await fetch(url, opts);
  if (res.headers.has('X-New-CSRF')) {
    const token = res.headers.get('X-New-CSRF');
    if (token) { try { localStorage.setItem('rr_csrf', token); } catch {} }
  }
  return res;
}

function configure(o) {
  if (!o) return; if (typeof o.thresholdPortion === 'number') state.thresholdPortion = o.thresholdPortion; if (o.refreshUrl) state.refreshUrl = o.refreshUrl;
}

async function refreshSessionIfNeeded() {
  if (!state.expiry) return;
  const now = Date.now();
  const remainingMs = state.expiry - now;
  if (state.ttl && remainingMs / state.ttl <= state.thresholdPortion) {
    // Debounce: avoid multiple refreshes in quick succession
    if (now - state.lastRefresh < 5000) return;
    state.lastRefresh = now;
    try {
      const csrf = getCsrf();
      const res = await fetch(state.refreshUrl, { method: 'POST', headers: csrf ? { 'X-CSRF-TOKEN': csrf } : {} });
      if (res.ok) {
        const data = await res.json();
        if (data?.expiresAt) {
          const newExp = Date.parse(data.expiresAt);
          if (!Number.isNaN(newExp)) {
            state.expiry = newExp;
            if (state.ttl) state.ttl = Math.max(state.ttl, newExp - now); // keep original ttl
          }
        }
        if (data?.csrf) {
          try { localStorage.setItem('rr_csrf', data.csrf); } catch {}
        }
      }
    } catch {}
  }
}

// Initialize by reading status once (lazy consumers can call setSessionMeta directly)
async function init() {
  try {
    const r = await fetch('/auth/status');
    if (!r.ok) return;
    const d = await r.json();
    if (d?.expiresAt) {
      const exp = Date.parse(d.expiresAt);
      if (!Number.isNaN(exp)) {
        state.expiry = exp; state.ttl = exp - Date.now();
      }
    }
    if (d?.csrf) { try { localStorage.setItem('rr_csrf', d.csrf); } catch {} }
  } catch {}
}

function setSessionMeta(expiresAtIso) {
  const exp = Date.parse(expiresAtIso);
  if (!Number.isNaN(exp)) { state.expiry = exp; if (!state.ttl) state.ttl = exp - Date.now(); }
}

// Periodic check
setInterval(refreshSessionIfNeeded, 60000); // every minute
init();

export { getCsrf, authFetch, refreshSessionIfNeeded, configure, setSessionMeta };