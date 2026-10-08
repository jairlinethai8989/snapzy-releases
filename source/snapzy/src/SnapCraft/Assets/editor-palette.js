function bindColorPalette(onPick) {
  const primaryInput = document.querySelector('#colorInput');
  let input = primaryInput, pick = onPick, anchorButton;
  const button = document.querySelector('#paletteButton');
  const popover = document.querySelector('#colorPalette');
  const grid = document.querySelector('#paletteGrid');
  const recent = document.querySelector('#recentColors');
  const colors = [
    '#ffffff','#4f7dff','#00bfff','#00ccb8','#22c92b','#c3df00','#fff258','#ff8700','#ef3340','#ed3fa2','#8233f5',
    '#f5f6f8','#e0e9ff','#d9f5ff','#d7f7f0','#e0f7d9','#f0f7cc','#fff9cc','#ffe6cc','#ffdee0','#ffdeef','#eedfff',
    '#dfe2e5','#b9ceff','#9ee7fc','#95e6d4','#afe794','#d7ec77','#ffed87','#ffc994','#ffa5ad','#ffade0','#c8a7f5',
    '#9ba1ab','#4f7dff','#00b7e4','#00ad92','#28a62b','#abc200','#ffdc00','#ec8200','#e83140','#d82490','#8333df',
    '#3d4248','#245bd5','#08799f','#005f59','#156716','#6b7600','#c59900','#a35100','#a61e31','#8c155b','#541592',
    '#20252c','#163a91','#00435c','#003b36','#084109','#424900','#756100','#663300','#6e1020','#600b3d','#350763'
  ];
  let saved = [];
  try { saved = JSON.parse(localStorage.getItem('neo-snap-recent-colors') || '[]'); } catch {}
  if (!Array.isArray(saved)) saved = [];
  saved = saved.filter((color) => /^#[0-9a-f]{6}$/i.test(color)).slice(0, 11);
  const close = () => { popover.hidden = true; button.setAttribute('aria-expanded', 'false'); };
  const makeSwatch = (color) => {
    const swatch = document.createElement('button');
    swatch.type = 'button'; swatch.dataset.color = color; swatch.style.setProperty('--c', color);
    swatch.title = color.toUpperCase(); swatch.setAttribute('aria-label', color.toUpperCase());
    swatch.onclick = () => { pick(color); remember(color); close(); (anchorButton || button).focus(); };
    return swatch;
  };
  function remember(color) {
    saved = [color, ...saved.filter((entry) => entry !== color)].slice(0, 11);
    try { localStorage.setItem('neo-snap-recent-colors', JSON.stringify(saved)); } catch {}
    recent.replaceChildren(...saved.map(makeSwatch)); update();
  }
  function update() {
    document.querySelector('#colorPreview').style.background = primaryInput.value;
    popover.querySelectorAll('[data-color]').forEach((swatch) => {
      const active = swatch.dataset.color.toLowerCase() === input.value.toLowerCase();
      swatch.classList.toggle('active', active); swatch.setAttribute('aria-pressed', String(active));
    });
  }
  grid.replaceChildren(...colors.map(makeSwatch));
  recent.replaceChildren(...saved.map(makeSwatch));
  function openFor(target, onColor, anchorElement) {
    input = target; pick = onColor; anchorButton = anchorElement;
    popover.hidden = false; button.setAttribute('aria-expanded', String(target === primaryInput)); update();
    const anchor = anchorElement.getBoundingClientRect();
    const left = Math.max(8, Math.min(innerWidth - popover.offsetWidth - 8, anchor.left));
    const top = anchor.bottom + popover.offsetHeight + 8 <= innerHeight ? anchor.bottom + 6 : Math.max(8, anchor.top - popover.offsetHeight - 6);
    popover.style.left = `${left}px`; popover.style.top = `${top}px`;
    (grid.querySelector('.active') || grid.querySelector('button')).focus();
  }
  button.onclick = () => {
    if (!popover.hidden && input === primaryInput) { close(); return; }
    openFor(primaryInput, onPick, button);
  };
  popover.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close(); (anchorButton || button).focus(); return; }
    const keys = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -11, ArrowDown: 11 };
    if (!(event.key in keys) || !event.target.closest('#paletteGrid')) return;
    event.preventDefault();
    const swatches = [...grid.children], index = swatches.indexOf(document.activeElement);
    swatches[(index + keys[event.key] + swatches.length) % swatches.length].focus();
  });
  document.addEventListener('pointerdown', (event) => { if (!event.target.closest('#colorPalette, #paletteButton, [data-effect-color]')) close(); });
  window.addEventListener('resize', close); document.querySelector('#stage').addEventListener('scroll', close);
  document.querySelector('#customColor').onclick = () => { close(); if (input.showPicker) input.showPicker(); else input.click(); };
  document.querySelectorAll('input[type=color]').forEach(target => target.addEventListener('change', () => remember(target.value)));
  update();
  return { update, close, openFor };
}
