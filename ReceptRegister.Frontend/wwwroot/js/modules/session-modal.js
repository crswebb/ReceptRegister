// session-modal.js (#70 #76) - background session watcher + 60s expiry modal
import { getCsrf, authFetch, refreshSessionIfNeeded, setSessionMeta } from './auth-client.js';

const SESSION_CHECK_INTERVAL = 60000; // reuse for status polling (issue #70)
const MODAL_WARNING_SECONDS = 60;
let expiry = null; // epoch ms
let modalShownFor = null; // expiry timestamp we already warned for
let countdownTimer = null;
let preloadTimer = null;
let pollInterval = null;
let lastActive = false; // track previous authenticated state
let focusBeforeModal = null;

function ensureModal() {
  let modal = document.getElementById('session-expiry-modal');
  if (modal) return modal;
  modal = document.createElement('div');
  modal.id = 'session-expiry-modal';
  modal.className = 'session-modal hidden';
  modal.innerHTML = `
    <div class="session-modal-backdrop" data-modal-close></div>
    <div class="session-modal-dialog" role="dialog" aria-labelledby="session-expiry-title" aria-modal="true">
      <h2 id="session-expiry-title">Session expiring soon</h2>
      <p>Your session will expire in <span id="session-expiry-countdown">60</span> seconds.</p>
      <div class="actions">
        <button type="button" class="btn btn-primary" id="stay-signed-in">Stay Signed In</button>
        <button type="button" class="btn btn-secondary" id="logout-now">Logout Now</button>
      </div>
    </div>`;
  document.body.appendChild(modal);
  modal.addEventListener('click', e => {
    if (e.target.matches('[data-modal-close]')) hideModal();
  });
  modal.querySelector('#stay-signed-in').addEventListener('click', renewSession);
  modal.querySelector('#logout-now').addEventListener('click', () => doLogout());
  return modal;
}

function trapFocus(e){
  const modal = document.getElementById('session-expiry-modal');
  if (!modal || modal.classList.contains('hidden')) return;
  const focusables = modal.querySelectorAll('button, [href], [tabindex]:not([tabindex="-1"])');
  if (!focusables.length) return;
  const first = focusables[0];
  const last = focusables[focusables.length-1];
  if (e.key === 'Tab') {
    if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
  } else if (e.key === 'Escape') {
    hideModal();
  }
}

function showModal() {
  const modal = ensureModal();
  if (!modal.classList.contains('hidden')) return;
  modal.classList.remove('hidden');
  focusBeforeModal = document.activeElement;
  document.addEventListener('keydown', trapFocus, true);
  const btn = modal.querySelector('#stay-signed-in');
  if (btn) btn.focus();
  startCountdown();
}

function hideModal() {
  const modal = document.getElementById('session-expiry-modal');
  if (!modal) return;
  modal.classList.add('hidden');
  stopCountdown();
  document.removeEventListener('keydown', trapFocus, true);
  if (focusBeforeModal && typeof focusBeforeModal.focus === 'function') {
    try { focusBeforeModal.focus(); } catch {}
  }
}

function startCountdown() {
  stopCountdown();
  countdownTimer = setInterval(() => {
    if (!expiry) return;
    const remain = Math.max(0, Math.floor((expiry - Date.now()) / 1000));
    const span = document.getElementById('session-expiry-countdown');
    if (span) span.textContent = remain.toString();
    if (remain <= 0) {
      stopCountdown();
      doLogout();
    }
  }, 1000);
}

function stopCountdown() { if (countdownTimer) { clearInterval(countdownTimer); countdownTimer = null; } }

async function doLogout() {
  try {
    const csrf = getCsrf();
    await fetch('/auth/logout', { method:'POST', headers: csrf ? { 'X-CSRF-TOKEN': csrf } : {} });
  } catch {}
  try { localStorage.removeItem('rr_csrf'); } catch {}
  location.href = '/Auth/Login';
}

async function renewSession() {
  try {
    const csrf = getCsrf();
    const res = await authFetch('/auth/refresh', { method:'POST', headers: csrf ? { 'X-CSRF-TOKEN': csrf } : {} });
    if (res.ok) {
      const data = await res.json();
      if (data?.expiresAt) { const exp = Date.parse(data.expiresAt); if (!Number.isNaN(exp)) { expiry = exp; setSessionMeta(data.expiresAt); modalShownFor = null; hideModal(); scheduleModal(); } }
    }
  } catch {}
}

function scheduleModal() {
  if (!expiry) return;
  if (modalShownFor === expiry) return; // already handled this expiry cycle
  const msUntilWarning = expiry - Date.now() - MODAL_WARNING_SECONDS * 1000;
  if (msUntilWarning <= 0) { showModal(); modalShownFor = expiry; return; }
  if (preloadTimer) clearTimeout(preloadTimer);
  preloadTimer = setTimeout(() => { showModal(); modalShownFor = expiry; }, msUntilWarning);
}

async function pollStatus() {
  try {
    const r = await fetch('/auth/status');
    if (!r.ok) return;
    const d = await r.json();
    const authed = !!d?.authenticated;
    if (!authed) {
      // user logged out or unauthenticated; clear timers & modal
      expiry = null; modalShownFor = null; hideModal();
      if (pollInterval) { /* keep interval but no-op until auth returns */ }
      lastActive = false;
      return;
    }
    if (!lastActive) {
      // transition to authed; schedule future polling/refresh if not already started
      lastActive = true;
    }
    if (d?.expiresAt) {
      const exp = Date.parse(d.expiresAt);
      if (!Number.isNaN(exp)) {
        expiry = exp;
        scheduleModal();
      }
    }
  } catch {}
}

// Periodic polling for adaptive nav + expiry updates (lazy start)
pollInterval = setInterval(() => { pollStatus(); refreshSessionIfNeeded(); }, SESSION_CHECK_INTERVAL);

// Initial kick
pollStatus();

export { pollStatus };
