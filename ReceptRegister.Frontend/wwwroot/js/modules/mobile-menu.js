// mobile-menu.js - feature #139
// Enhances the header nav with a collapsible toggle on small screens.
(function(){
  const toggle = document.getElementById('nav-toggle');
  const nav = document.getElementById('main-nav');
  if(!toggle || !nav) return;
  const list = nav.querySelector('ul');
  if(!list) return;

  // Utility to animate between heights for smoother open/close without fixed max-height.
  function animateHeight(open){
    if(!window.matchMedia('(max-width:700px)').matches){
      list.style.maxHeight = '';
      return;
    }
    if(open){
      // Measure natural height
      list.style.maxHeight = list.scrollHeight + 'px';
    } else {
      list.style.maxHeight = '0px';
    }
  }

  function applyState(expanded){
    toggle.setAttribute('aria-expanded', String(expanded));
    nav.dataset.collapsed = expanded ? 'false' : 'true';
    const openText = toggle.getAttribute('data-text-open') || 'Menu';
    const closeText = toggle.getAttribute('data-text-close') || 'Close menu';
    toggle.textContent = expanded ? closeText : openText;
    animateHeight(expanded);
  }

  const mq = window.matchMedia('(max-width:700px)');
  let expanded = !mq.matches; // expanded on larger screens
  applyState(expanded);

  toggle.addEventListener('click', () => {
    expanded = !expanded;
    applyState(expanded);
  });

  mq.addEventListener('change', e => {
    expanded = !e.matches; // when switching breakpoints re-sync
    // Remove inline height to allow normal layout on desktop
    if(!e.matches){ list.style.maxHeight = ''; }
    applyState(expanded);
  });

  // Recompute height if window resizes while open (content count could change)
  window.addEventListener('resize', () => { if(expanded && mq.matches) animateHeight(true); });
})();
