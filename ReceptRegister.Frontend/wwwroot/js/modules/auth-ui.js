// auth-ui.js - manage login/logout link visibility and CSRF handling
(function(){
  const metaCsrf = document.querySelector('meta[name="csrf-token"]');
  if (metaCsrf && metaCsrf.content) {
    // Persist for other modules
    try { localStorage.setItem('rr_csrf', metaCsrf.content); } catch {}
  }

  async function refreshStatus(){
    try {
      const r = await fetch('/auth/status',{headers:{'Accept':'application/json'}});
      if(!r.ok) return;
      const data = await r.json();
      if (data && data.csrf) {
        try { localStorage.setItem('rr_csrf', data.csrf); } catch {}
      }
      const authed = !!data?.authenticated;
      for (const el of document.querySelectorAll('[data-auth-visible]')) {
        const mode = el.getAttribute('data-auth-visible');
        el.style.display = (mode === 'authed' ? authed : !authed) ? '' : 'none';
      }
    } catch {}
  }

  document.addEventListener('submit', async e => {
    const form = e.target;
    if (form && form.matches('#logout-form')) {
      const csrf = localStorage.getItem('rr_csrf');
      e.preventDefault();
      const res = await fetch('/auth/logout', { method:'POST', headers: csrf ? {'X-CSRF-TOKEN': csrf} : {} });
      // Clear stored token regardless
      try { localStorage.removeItem('rr_csrf'); } catch {}
      location.href = '/Auth/Login';
    }
  });

  // Initial adjust after DOM ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', refreshStatus);
  } else {
    refreshStatus();
  }
})();
