function bindAnnotationEffects({ getItem, checkpoint, render, palette, getTool, getColor }) {
  let customBorder = false;
  const presets = {
    shadow: { enabled: false, color: '#000000', blur: 6, x: 3, y: 3 },
    outline: { enabled: false, color: '#000000', width: 2 },
    bubbleFill: '#ffffff', bubbleBorder: '#ef3340', bubbleBorderWidth: 5
  };
  const fields = [
    ['shadowEnabled', 'shadow', 'enabled'], ['shadowColor', 'shadow', 'color'],
    ['shadowBlur', 'shadow', 'blur'], ['shadowOffsetX', 'shadow', 'x'], ['shadowOffsetY', 'shadow', 'y'],
    ['outlineEnabled', 'outline', 'enabled'], ['outlineColor', 'outline', 'color'], ['outlineWidth', 'outline', 'width'],
    ['bubbleFill', 'bubbleFill'], ['bubbleBorder', 'bubbleBorder'], ['bubbleBorderWidth', 'bubbleBorderWidth']
  ];
  function value(item, group, property) {
    if (item && property === 'enabled' && !item[group]) return false;
    if (group === 'bubbleBorder' && !item?.bubbleBorder && !customBorder) return getColor();
    return property ? { ...presets[group], ...item?.[group] }[property] : item?.[group] ?? presets[group];
  }
  function set(object, group, property, next) {
    if (property) object[group] = { ...presets[group], ...object[group], [property]: next };
    else object[group] = next;
  }
  fields.forEach(([id, group, property]) => {
    const input = document.getElementById(id);
    let editing = null;
    function update() {
      const next = input.type === 'checkbox' ? input.checked : input.type === 'color' ? input.value : Number(input.value);
      const item = getItem();
      if (group === 'bubbleBorder') customBorder = true;
      if (item && value(item, group, property) !== next) {
        if (editing !== item) { checkpoint(); editing = item; }
        if (property && !item[group]) item[group] = { ...presets[group], enabled: false };
        set(item, group, property, next);
      }
      set(presets, group, property, next); sync(); render();
    }
    input.addEventListener('input', update);
    input.addEventListener('change', () => { update(); editing = null; });
    input.addEventListener('blur', () => { editing = null; });
    if (input.type === 'color') {
      const swatch = document.querySelector(`[data-effect-color="${id}"]`);
      swatch.onclick = () => palette.openFor(input, color => {
        input.value = color; input.dispatchEvent(new Event('input')); input.dispatchEvent(new Event('change'));
      }, swatch);
    }
  });
  function close() {
    document.querySelectorAll('.effect-popover').forEach(p => { p.hidden = true; });
    document.querySelectorAll('[data-effect-popover]').forEach(b => b.setAttribute('aria-expanded', 'false'));
  }
  function position(button, popover) {
    const anchor = button.getBoundingClientRect();
    popover.style.left = `${Math.max(8, Math.min(innerWidth - popover.offsetWidth - 8, anchor.left))}px`;
    popover.style.top = `${Math.max(8, Math.min(innerHeight - popover.offsetHeight - 8, anchor.bottom + 6))}px`;
  }
  document.querySelectorAll('[data-effect-popover]').forEach(button => button.onclick = () => {
    const popover = document.getElementById(button.dataset.effectPopover), open = popover.hidden;
    close(); if (!open) return;
    popover.hidden = false; button.setAttribute('aria-expanded', 'true'); position(button, popover);
  });
  document.addEventListener('pointerdown', event => { if (!event.target.closest('.effect-popover, [data-effect-popover], #colorPalette')) close(); });
  document.querySelectorAll('.effect-popover').forEach(popover => popover.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
      event.preventDefault(); event.stopPropagation();
      const button = document.querySelector(`[data-effect-popover="${popover.id}"]`); close(); button.focus();
    }
  }));
  window.addEventListener('resize', close); document.querySelector('#stage').addEventListener('scroll', close);
  function sync() {
    const item = getItem(), tool = item?.tool || getTool(), text = ['text', 'bubble'].includes(tool);
    document.querySelector('#textEffects').hidden = !text;
    document.querySelector('#bubbleEffects').hidden = tool !== 'bubble';
    document.querySelector('#shadowEnabled').disabled = !item && ['select', 'crop'].includes(tool);
    fields.forEach(([id, group, property]) => {
      const input = document.getElementById(id), next = value(item, group, property);
      // Missing properties on existing annotations are legacy effects, not preset opt-ins.
      if (input.type === 'checkbox') input.checked = item && !item[group] ? false : next;
      else input.value = next;
      const output = document.querySelector(`[data-effect-value="${id}"]`);
      if (output) output.textContent = `${input.value} px`;
      const swatch = document.querySelector(`[data-effect-color="${id}"]`);
      if (swatch) swatch.style.setProperty('--effect-color', input.value);
    });
    document.querySelector('#shadowSettingsButton').classList.toggle('active', document.querySelector('#shadowEnabled').checked);
    document.querySelector('#textSettingsButton').classList.toggle('active', document.querySelector('#outlineEnabled').checked);
    if (!text) document.querySelector('#textSettings').hidden = true;
  }
  return { sync, close, style: () => ({ ...structuredClone(presets), bubbleBorder: customBorder ? presets.bubbleBorder : getColor() }) };
}
