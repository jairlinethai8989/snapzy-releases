let viewMode = 'fit';

function fitCanvas(center) {
  if (!baseImage) return;
  const available = Math.max(100, stage.clientWidth - 48);
  const scale = viewMode === 'actual' ? 1 : Math.min(1, available / canvas.width);
  canvas.style.width = `${canvas.width * scale}px`;
  canvas.style.height = `${canvas.height * scale}px`;
  if (center) {
    const parent = stage.getBoundingClientRect(), image = canvas.getBoundingClientRect();
    stage.scrollLeft += image.left + center.x * scale - parent.left - stage.clientWidth / 2;
    stage.scrollTop += image.top + center.y * scale - parent.top - stage.clientHeight / 2;
  }
  render();
}

function setView(mode) {
  if (!baseImage || mode === viewMode) return;
  endGesture();
  const parent = stage.getBoundingClientRect(), image = canvas.getBoundingClientRect();
  const center = {
    x: (parent.left + stage.clientWidth / 2 - image.left) * canvas.width / image.width,
    y: (parent.top + stage.clientHeight / 2 - image.top) * canvas.height / image.height
  };
  viewMode = mode;
  document.querySelectorAll('[data-view]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.view === mode)));
  fitCanvas(center);
}

function updateTailHandle() {
  const handle = document.querySelector('#bubbleTailHandle');
  const item = objects[selected];
  handle.hidden = !baseImage || item?.tool !== 'bubble' || Boolean(textEditor) || activeTool === 'crop';
  if (handle.hidden) return;
  const tip = annotationEffects.bubbleGeometry(ctx, item).tip;
  const image = canvas.getBoundingClientRect(), viewport = stage.getBoundingClientRect();
  const x = image.left + (tip.x - cropRect.x) * image.width / canvas.width;
  const y = image.top + (tip.y - cropRect.y) * image.height / canvas.height;
  handle.hidden = x < viewport.left || x > viewport.left + stage.clientWidth ||
    y < viewport.top || y > viewport.top + stage.clientHeight ||
    tip.x < cropRect.x || tip.x > cropRect.x + cropRect.width || tip.y < cropRect.y || tip.y > cropRect.y + cropRect.height;
  handle.style.left = `${x}px`; handle.style.top = `${y}px`;
}

function moveTail(item, point) {
  const x = Math.max(cropRect.x, Math.min(cropRect.x + cropRect.width, point.x));
  const y = Math.max(cropRect.y, Math.min(cropRect.y + cropRect.height, point.y));
  item.tail = { x: x - item.x1, y: y - item.y1 };
  const g = annotationEffects.bubbleGeometry(ctx, item), b = g.body;
  const right = cropRect.x + cropRect.width, bottom = cropRect.y + cropRect.height;
  let tip = g.tip;
  if (tip.x < cropRect.x || tip.x > right || tip.y < cropRect.y || tip.y > bottom) {
    // Prefer an exterior edge with room in the crop when the nearest one is clipped.
    const candidates = [
      { room: b.x - cropRect.x, distance: x - b.x, x: Math.max(cropRect.x, b.x - 16), y },
      { room: right - b.x - b.width, distance: b.x + b.width - x, x: Math.min(right, b.x + b.width + 16), y },
      { room: b.y - cropRect.y, distance: y - b.y, x, y: Math.max(cropRect.y, b.y - 16) },
      { room: bottom - b.y - b.height, distance: b.y + b.height - y, x, y: Math.min(bottom, b.y + b.height + 16) }
    ];
    tip = candidates.filter(p => p.room > 0).sort((a, b) => a.distance - b.distance)[0] || { x: b.x, y };
  }
  item.tail = { x: tip.x - item.x1, y: tip.y - item.y1 };
}
