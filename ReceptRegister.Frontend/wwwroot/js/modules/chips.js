// chips.js (#40) enhance selectable chips (future association forms)
for (const container of document.querySelectorAll('[data-chip-select]')) {
  container.addEventListener('click', e => {
    const chip = e.target.closest('.chip[data-selectable="true"]');
    if (!chip) return;
    const selected = chip.getAttribute('data-selected') === 'true';
    chip.setAttribute('data-selected', String(!selected));
    // Hidden input synchronization could be added here in future iteration
  });
}