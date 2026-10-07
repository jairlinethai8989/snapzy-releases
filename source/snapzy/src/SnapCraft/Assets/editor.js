const canvas = document.querySelector('#canvas');
const ctx = canvas.getContext('2d');
const stage = document.querySelector('#stage');
const colorInput = document.querySelector('#colorInput');
const sizeInput = document.querySelector('#sizeInput');
const opacityInput = document.querySelector('#opacityInput');
const toolStyles = new Map();
let drawingColor = '#ef3340';
const strokeTools = new Set(['pen', 'line', 'arrow', 'box', 'ellipse']);
let strokePattern = 'solid';
let objects = [];
let selected = -1;
let activeTool = 'select';
let gesture = null;
let capturedPointer = null;
let baseImage = null;
let textEditor = null;
let finishTextEdit = null;
let keptSnapshot = null;
let cropRect = null;
let cropDraft = null;
let cropPointer = null;
let cropScrollFrame = 0;
let cropImageIndex = -1;
const undoStack = [];
const redoStack = [];

function showToast(message) {
  const toast = document.querySelector('#toast');
  toast.textContent = message;
  toast.classList.add('show');
  clearTimeout(showToast.timer);
  showToast.timer = setTimeout(() => toast.classList.remove('show'), 2200);
}

function checkpoint() {
  undoStack.push(JSON.stringify(projectState()));
  if (undoStack.length > 80) undoStack.shift();
  redoStack.length = 0;
}

function loadImage(src) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('อ่านภาพไม่ได้'));
    image.src = src;
  });
}

function textMetrics(item) {
  const fontSize = Math.max(12, item.size * 4);
  const lineHeight = fontSize * 1.25;
  ctx.font = `600 ${fontSize}px SnapSans, sans-serif`;
  const lines = (item.text || '').split('\n');
  return { lines, fontSize, lineHeight, width: Math.max(12, ...lines.map((line) => ctx.measureText(line || ' ').width)), height: Math.max(lineHeight, lines.length * lineHeight) };
}

function bounds(item) {
  if (['line', 'arrow'].includes(item.tool) && item.curve) return curveBounds(item);
  if (item.tool === 'number') {
    const radius = numberRadius(item);
    return { x: item.x1 - radius, y: item.y1 - radius, width: radius * 2, height: radius * 2 };
  }
  if (item.tool === 'text') {
    const metrics = textMetrics(item);
    return { x: item.x1, y: item.y1, width: metrics.width, height: metrics.height };
  }
  if (item.tool === 'bubble') {
    return annotationEffects.bubbleGeometry(ctx, item).bounds;
  }
  if (item.tool === 'pen' || item.tool === 'highlight') {
    const xs = item.points.map((point) => point.x);
    const ys = item.points.map((point) => point.y);
    return { x: Math.min(...xs), y: Math.min(...ys), width: Math.max(1, Math.max(...xs) - Math.min(...xs)), height: Math.max(1, Math.max(...ys) - Math.min(...ys)) };
  }
  return { x: Math.min(item.x1, item.x2), y: Math.min(item.y1, item.y2), width: Math.max(1, Math.abs(item.x2 - item.x1)), height: Math.max(1, Math.abs(item.y2 - item.y1)) };
}

function drawPath(item, ctx) {
  ctx.beginPath();
  if (item.points.length === 1) {
    ctx.arc(item.points[0].x, item.points[0].y, item.size / 2, 0, Math.PI * 2);
    ctx.fill();
    return;
  }
  ctx.moveTo(item.points[0].x, item.points[0].y);
  for (let index = 1; index < item.points.length - 1; index += 1) {
    const current = item.points[index];
    const next = item.points[index + 1];
    ctx.quadraticCurveTo(current.x, current.y, (current.x + next.x) / 2, (current.y + next.y) / 2);
  }
  const last = item.points.at(-1);
  ctx.lineTo(last.x, last.y);
  ctx.stroke();
}

function numberRadius(item) { return Math.max(16, item.size * 3, String(item.number).length * item.size * 1.6); }

function drawItem(item) {
  const box = bounds(item);
  const owner = item.owner && objects.find(o => o.tool === 'image' && o.id === item.owner);
  const clip = owner ? bounds(owner) : item.clipRect;
  ctx.save();
  if (clip) { ctx.beginPath(); ctx.rect(clip.x, clip.y, clip.width, clip.height); ctx.clip(); }
  if (item.tool === 'blur') {
    drawItemBody(item, ctx);
    annotationEffects.blurShadow(ctx, item, box, cropRect);
    ctx.restore(); return;
  }
  annotationEffects.withShadow(ctx, item, box, cropRect, target => drawItemBody(item, target, target !== ctx));
  ctx.restore();
}

function drawItemBody(item, ctx, compositing = false) {
  ctx.save();
  ctx.globalAlpha = compositing ? 1 : item.opacity ?? 1;
  ctx.strokeStyle = item.color;
  ctx.fillStyle = item.color;
  ctx.lineWidth = item.size;
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  if (strokeTools.has(item.tool)) ctx.setLineDash(item.stroke === 'dashed' ? [item.size * 4, item.size * 3] : item.stroke === 'dotted' ? [0, item.size * 3] : []);
  const box = bounds(item);
  if (item.tool === 'image') {
    const source = projectAssets.get(item.assetId)?.image;
    const r = item.sourceCrop;
    if (source) ctx.drawImage(source, r.x, r.y, r.width, r.height, box.x, box.y, box.width, box.height);
  }
  else if (item.tool === 'pen' || item.tool === 'highlight') {
    if (item.tool === 'highlight') { if (!compositing) ctx.globalCompositeOperation = 'multiply'; ctx.lineCap = 'butt'; }
    drawPath(item, ctx);
  }
  else if (item.tool === 'line') strokePath(ctx, item);
  else if (item.tool === 'arrow') {
    const tangent = item.curve || { x: item.x1, y: item.y1 };
    const angle = Math.atan2(item.y2 - tangent.y, item.x2 - tangent.x);
    const head = item.size * 3 + 10;
    strokePath(ctx, item); ctx.setLineDash([]); ctx.beginPath();
    ctx.moveTo(item.x2, item.y2); ctx.lineTo(item.x2 - head * Math.cos(angle - Math.PI / 6), item.y2 - head * Math.sin(angle - Math.PI / 6));
    ctx.moveTo(item.x2, item.y2); ctx.lineTo(item.x2 - head * Math.cos(angle + Math.PI / 6), item.y2 - head * Math.sin(angle + Math.PI / 6)); ctx.stroke();
  } else if (item.tool === 'box') ctx.strokeRect(box.x, box.y, box.width, box.height);
  else if (item.tool === 'block') ctx.fillRect(box.x, box.y, box.width, box.height);
  else if (item.tool === 'ellipse' || item.tool === 'ellipseFill') {
    ctx.beginPath(); ctx.ellipse(box.x + box.width / 2, box.y + box.height / 2, box.width / 2, box.height / 2, 0, 0, Math.PI * 2);
    if (item.tool === 'ellipseFill') ctx.fill(); else ctx.stroke();
  } else if (item.tool === 'number') {
    const radius = numberRadius(item);
    ctx.beginPath(); ctx.arc(item.x1, item.y1, radius, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = '#ffffff'; ctx.lineWidth = Math.max(1, radius / 16); ctx.stroke();
    const rgb = item.color.slice(1).match(/../g).map((channel) => parseInt(channel, 16));
    ctx.fillStyle = rgb[0] * .299 + rgb[1] * .587 + rgb[2] * .114 > 160 ? '#17201d' : '#ffffff';
    ctx.font = `700 ${Math.min(radius * 1.2, radius * 2.8 / String(item.number).length)}px SnapSans, sans-serif`;
    ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(String(item.number), item.x1, item.y1 + radius * .06);
  }
  else if (item.tool === 'blur') {
    const padding = item.blur * 3;
    const x = Math.max(0, Math.floor(box.x - padding - cropRect.x));
    const y = Math.max(0, Math.floor(box.y - padding - cropRect.y));
    const width = Math.min(canvas.width - x, Math.ceil(box.width + padding * 2));
    const height = Math.min(canvas.height - y, Math.ceil(box.height + padding * 2));
    if (width > 0 && height > 0) {
      const sample = document.createElement('canvas'); sample.width = width; sample.height = height;
      sample.getContext('2d').drawImage(canvas, x, y, width, height, 0, 0, width, height);
      ctx.beginPath(); ctx.rect(box.x, box.y, box.width, box.height); ctx.clip(); ctx.filter = `blur(${item.blur}px)`; ctx.drawImage(sample, x + cropRect.x, y + cropRect.y);
    }
  } else if (item.tool === 'text') {
    annotationEffects.drawText(ctx, item, textMetrics(item), item.x1, item.y1);
  } else if (item.tool === 'bubble') {
    const layout = annotationEffects.bubbleLayout(ctx, item);
    annotationEffects.bubblePath(ctx, item.x1, item.y1, layout, annotationEffects.bubbleGeometry(ctx, item));
    ctx.fillStyle = item.bubbleFill || '#ffffff'; ctx.fill();
    ctx.strokeStyle = item.bubbleBorder || item.color;
    ctx.lineWidth = item.bubbleBorderWidth || 5; ctx.stroke();
    annotationEffects.drawText(ctx, item, layout, item.x1 + layout.padding, item.y1 + layout.padding);
  }
  ctx.restore();
}

function render(showSelection = true) {
  if (!baseImage) return;
  if (typeof syncLayerControls === 'function') syncLayerControls();
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.drawImage(baseImage, cropRect.x, cropRect.y, cropRect.width, cropRect.height, 0, 0, canvas.width, canvas.height);
  ctx.save();
  ctx.translate(-cropRect.x, -cropRect.y);
  objects.forEach(drawItem);
  if (showSelection && objects[selected] && !textEditor) {
    const box = bounds(objects[selected]);
    const scale = canvas.width / canvas.getBoundingClientRect().width;
    ctx.save(); ctx.strokeStyle = '#0b8b6d'; ctx.lineWidth = 1.5 * scale; ctx.setLineDash([6 * scale, 4 * scale]);
    ctx.strokeRect(box.x - 5 * scale, box.y - 5 * scale, box.width + 10 * scale, box.height + 10 * scale); ctx.setLineDash([]);
    const resize = resizeBounds(objects[selected]);
    ctx.fillStyle = '#fff'; ctx.fillRect(resize.x + resize.width - 6 * scale, resize.y + resize.height - 6 * scale, 12 * scale, 12 * scale); ctx.strokeRect(resize.x + resize.width - 6 * scale, resize.y + resize.height - 6 * scale, 12 * scale, 12 * scale); ctx.restore();
  }
  ctx.restore();
  if (showSelection && cropDraft) {
    const x = cropDraft.x - cropRect.x, y = cropDraft.y - cropRect.y;
    const right = x + cropDraft.width, bottom = y + cropDraft.height;
    ctx.save(); ctx.fillStyle = '#081b14aa';
    ctx.fillRect(0, 0, canvas.width, y); ctx.fillRect(0, bottom, canvas.width, canvas.height - bottom);
    ctx.fillRect(0, y, x, cropDraft.height); ctx.fillRect(right, y, canvas.width - right, cropDraft.height);
    ctx.strokeStyle = '#72e7bd'; ctx.lineWidth = Math.max(2, canvas.width / canvas.getBoundingClientRect().width * 2);
    ctx.strokeRect(x, y, cropDraft.width, cropDraft.height); ctx.restore();
  }
  updateTailHandle();
  updateCurveHandle();
}

function canvasPoint(event) {
  const rect = canvas.getBoundingClientRect();
  return { x: cropRect.x + (event.clientX - rect.left) * canvas.width / rect.width, y: cropRect.y + (event.clientY - rect.top) * canvas.height / rect.height };
}

function updateCropDraft() {
  if (gesture?.mode !== 'crop' || !cropPointer) return;
  const point = canvasPoint(cropPointer);
  const x = Math.max(cropRect.x, Math.min(cropRect.x + cropRect.width, point.x));
  const y = Math.max(cropRect.y, Math.min(cropRect.y + cropRect.height, point.y));
  cropDraft = { x: Math.min(gesture.start.x, x), y: Math.min(gesture.start.y, y), width: Math.abs(x - gesture.start.x), height: Math.abs(y - gesture.start.y) };
  render();
}

function stopCropScroll() {
  if (cropScrollFrame) cancelAnimationFrame(cropScrollFrame);
  cropScrollFrame = 0;
  cropPointer = null;
}

function scrollWhileCropping() {
  if (gesture?.mode !== 'crop' || !cropPointer) { stopCropScroll(); return; }
  const box = stage.getBoundingClientRect();
  const edge = 56;
  const upper = Math.max(0, box.top + edge - cropPointer.clientY);
  const lower = Math.max(0, cropPointer.clientY - (box.bottom - edge));
  const distance = lower - upper;
  if (distance) {
    stage.scrollTop += Math.sign(distance) * Math.min(30, Math.max(3, Math.abs(distance) * 0.5));
  }
  cropScrollFrame = requestAnimationFrame(scrollWhileCropping);
}

function setTool(tool) {
  endGesture();
  cropImageIndex = tool === 'crop' && objects[selected]?.tool === 'image' ? selected : -1;
  if (tool !== 'crop') cropDraft = null;
  if (tool !== activeTool && tool !== 'select' && tool !== 'crop') {
    if (activeTool !== 'select' && activeTool !== 'crop') toolStyles.set(activeTool, currentStyle());
    const style = toolStyles.get(tool) || { color: tool === 'highlight' ? '#fff258' : drawingColor, size: tool === 'highlight' ? 24 : strokeTools.has(tool) ? 5 : 6, opacity: tool === 'highlight' ? 0.4 : 1, stroke: 'solid' };
    colorInput.value = style.color; sizeInput.value = style.size; opacityInput.value = Math.round(style.opacity * 100);
    strokePattern = style.stroke || 'solid';
  }
  activeTool = tool;
  if (tool !== 'select') selected = -1;
  effects.close();
  closeShapeMenus();
  document.querySelectorAll('.shape-group').forEach((group) => {
    const choice = group.querySelector(`.shape-menu [data-tool="${tool}"]`);
    const primary = group.querySelector('.shape-primary');
    if (choice) {
      primary.dataset.tool = tool; primary.title = choice.title;
      primary.replaceChildren(choice.querySelector('svg').cloneNode(true));
    }
    group.classList.toggle('active', Boolean(choice));
  });
  document.querySelectorAll('[data-tool]').forEach((button) => {
    const active = button.dataset.tool === tool;
    button.classList.toggle('active', active); button.setAttribute('aria-pressed', String(active));
    button.setAttribute('aria-label', button.title);
  });
  document.querySelector('#cropActions').hidden = tool !== 'crop';
  canvas.style.cursor = tool === 'select' ? 'default' : ['text', 'bubble'].includes(tool) ? 'text' : 'crosshair';
  syncControls(false);
  render();
}

function currentStyle() { return { color: colorInput.value, size: Number(sizeInput.value), opacity: Number(opacityInput.value) / 100, blur: 16, stroke: strokePattern, ...effects.style() }; }

function segmentDistance(point, a, b) {
  const dx = b.x - a.x, dy = b.y - a.y;
  const t = Math.max(0, Math.min(1, ((point.x - a.x) * dx + (point.y - a.y) * dy) / (dx * dx + dy * dy || 1)));
  return Math.hypot(point.x - a.x - t * dx, point.y - a.y - t * dy);
}

function hitTest(point, item) {
  const owner=item.owner && objects.find(o=>o.tool==='image' && o.id===item.owner);
  const clip=owner?bounds(owner):item.clipRect;
  if(clip && (point.x<clip.x||point.y<clip.y||point.x>clip.x+clip.width||point.y>clip.y+clip.height)) return false;
  const tolerance = Math.max(10, item.size * 1.5);
  if (item.tool === 'pen' || item.tool === 'highlight') return item.points.some((p, index) => index ? segmentDistance(point, item.points[index - 1], p) <= tolerance : Math.hypot(point.x - p.x, point.y - p.y) <= tolerance);
  if (item.tool === 'line' || item.tool === 'arrow') return curveHit(point, item, tolerance);
  if (item.tool === 'bubble') {
    const g = annotationEffects.bubbleGeometry(ctx, item);
    annotationEffects.bubblePath(ctx, item.x1, item.y1, g.layout, g);
    return ctx.isPointInPath(point.x, point.y) ||
      segmentDistance(point, g.start, g.tip) <= tolerance || segmentDistance(point, g.tip, g.end) <= tolerance;
  }
  const box = bounds(item);
  if (item.tool === 'number' || item.tool === 'ellipse' || item.tool === 'ellipseFill') {
    return ((point.x - box.x - box.width / 2) / (box.width / 2 + tolerance)) ** 2 + ((point.y - box.y - box.height / 2) / (box.height / 2 + tolerance)) ** 2 <= 1;
  }
  return point.x >= box.x - tolerance && point.x <= box.x + box.width + tolerance && point.y >= box.y - tolerance && point.y <= box.y + box.height + tolerance;
}

function moveItem(item, original, dx, dy) {
  if (original.curve) item.curve = { x: original.curve.x + dx, y: original.curve.y + dy };
  if (item.tool === 'image') transformImageChildren(item, original, dx, dy);
  if (item.tool === 'pen' || item.tool === 'highlight') item.points = original.points.map((point) => ({ x: point.x + dx, y: point.y + dy }));
  else { item.x1 = original.x1 + dx; item.y1 = original.y1 + dy; if ('x2' in original) { item.x2 = original.x2 + dx; item.y2 = original.y2 + dy; } }
  if (item.tool === 'image' && typeof syncProjectControls === 'function') syncProjectControls();
}

function resizeBounds(item) { return item.tool === 'bubble' ? annotationEffects.bubbleGeometry(ctx, item).body : bounds(item); }

function resizeItem(item, original, point) {
  const box = resizeBounds(original);
  if (item.tool === 'image') {
    const scale = Math.max(.05, Math.min((cropRect.x + cropRect.width - box.x) / box.width, (cropRect.y + cropRect.height - box.y) / box.height,
      Math.max((point.x-box.x)/box.width,(point.y-box.y)/box.height)));
    item.x2=item.x1+box.width*scale; item.y2=item.y1+box.height*scale;
    transformImageChildren(item, original, 0, 0, scale); return;
  }
  if (item.tool === 'bubble') {
    item.bubbleWidth = Math.max(20, Math.min(cropRect.x + cropRect.width - box.x, point.x - box.x));
    item.bubbleHeight = Math.max(0, point.y - box.y); return;
  }
  const stroke = ['line', 'arrow', 'pen', 'highlight'].includes(item.tool);
  // Project stroke resizing onto its diagonal so a nearly zero axis cannot amplify movement.
  const scale = Math.max(0.05, stroke
    ? ((point.x - box.x) * box.width + (point.y - box.y) * box.height) / (box.width ** 2 + box.height ** 2)
    : Math.max((point.x - box.x) / box.width, (point.y - box.y) / box.height));
  if (original.curve) item.curve = { x: box.x + (original.curve.x - box.x) * scale, y: box.y + (original.curve.y - box.y) * scale };
  if (item.tool === 'pen' || item.tool === 'highlight') item.points = original.points.map((p) => ({ x: box.x + (p.x - box.x) * scale, y: box.y + (p.y - box.y) * scale }));
  else if (item.tool === 'text' || item.tool === 'number') {
    item.size = Math.max(2, Math.min(64, original.size * scale));
    if (item.tool === 'number') {
      const radius = numberRadius(item); item.x1 = box.x + radius; item.y1 = box.y + radius;
    }
  }
  else { item.x1 = box.x + (original.x1 - box.x) * scale; item.y1 = box.y + (original.y1 - box.y) * scale; item.x2 = box.x + (original.x2 - box.x) * scale; item.y2 = box.y + (original.y2 - box.y) * scale; }
}

function openTextEditor(point, event, editIndex = -1) {
  textEditor?.remove();
  const existing = objects[editIndex];
  const tool = existing?.tool || activeTool;
  const style = existing ? null : currentStyle();
  const textarea = document.createElement('textarea');
  textarea.className = 'canvas-text-editor'; textarea.placeholder = 'พิมพ์ข้อความ…'; textarea.value = existing?.text || '';
  const scale = canvas.getBoundingClientRect().width / canvas.width;
  textarea.style.left = `${Math.min(innerWidth - 280, Math.max(12, event.clientX))}px`;
  textarea.style.top = `${Math.min(innerHeight - 150, Math.max(12, event.clientY))}px`;
  textarea.style.color = existing?.color || colorInput.value;
  textarea.style.fontSize = `${Math.max(16, (existing?.size || Number(sizeInput.value)) * 4 * scale)}px`;
  document.body.append(textarea); textEditor = textarea;
  // Focus after the canvas pointer sequence finishes so pointerup cannot blur it immediately.
  setTimeout(() => { if (textEditor === textarea) { textarea.focus(); textarea.select(); } }, 0);
  let finished = false;
  const finish = (cancel = false) => {
    if (finished) return; finished = true;
    const value = textarea.value.trim(); textarea.remove(); textEditor = null; finishTextEdit = null;
    if (!cancel && value) {
      checkpoint();
      if (existing) existing.text = value;
      else {
        const item = { tool, x1: point.x, y1: point.y, text: value, ...style };
        if (tool === 'bubble') {
          item.bubbleWidth = Math.min(260, cropRect.width);
          const layout = annotationEffects.bubbleLayout(ctx, item);
          item.x1 = Math.max(cropRect.x, Math.min(point.x, cropRect.x + cropRect.width - layout.width));
          item.y1 = Math.max(cropRect.y, Math.min(point.y, cropRect.y + cropRect.height - layout.height));
        }
        objects.push(item); selected = objects.length - 1;
      }
    }
    syncControls(); render();
  };
  finishTextEdit = finish;
  textarea.addEventListener('keydown', (keyEvent) => { if (keyEvent.key === 'Escape') finish(true); if (keyEvent.key === 'Enter' && (keyEvent.ctrlKey || keyEvent.metaKey)) finish(false); });
  textarea.addEventListener('blur', () => finish(false));
}

canvas.addEventListener('pointerdown', (event) => {
  if (!baseImage || event.button !== 0 || textEditor) return;
  const point = canvasPoint(event);
  if (activeTool === 'text' || activeTool === 'bubble') { openTextEditor(point, event); return; }
  if (activeTool === 'number') {
    checkpoint();
    const number = objects.reduce((highest, item) => item.tool === 'number' ? Math.max(highest, item.number) : highest, 0) + 1;
    const item = { tool: 'number', number, x1: point.x, y1: point.y, ...currentStyle() };
    const radius = numberRadius(item);
    item.x1 = Math.max(cropRect.x + radius, Math.min(cropRect.x + cropRect.width - radius, point.x));
    item.y1 = Math.max(cropRect.y + radius, Math.min(cropRect.y + cropRect.height - radius, point.y));
    objects.push(item); selected = objects.length - 1; syncControls(); render(); return;
  }
  canvas.setPointerCapture(event.pointerId);
  capturedPointer = event.pointerId;
  if (activeTool === 'crop') {
    cropDraft = { x: point.x, y: point.y, width: 0, height: 0 };
    cropPointer = { clientX: event.clientX, clientY: event.clientY };
    gesture = { mode: 'crop', start: point };
    cropScrollFrame = requestAnimationFrame(scrollWhileCropping);
    render(); return;
  }
  if (activeTool === 'select') {
    const currentBox = objects[selected] && resizeBounds(objects[selected]);
    const scale = canvas.width / canvas.getBoundingClientRect().width;
    const resize = currentBox && Math.hypot(point.x - currentBox.x - currentBox.width, point.y - currentBox.y - currentBox.height) < 14 * scale;
    if (!resize) selected = objects.findLastIndex((item) => hitTest(point, item));
    if (selected >= 0) { checkpoint(); gesture = { mode: resize ? 'resize' : 'move', start: point, original: structuredClone(objects[selected]), children: objects[selected].tool === 'image' ? prepareImageChildren(objects[selected]) : null }; }
    syncControls();
    render(); return;
  }
  checkpoint();
  const common = { tool: activeTool, ...currentStyle() };
  const item = activeTool === 'pen' || activeTool === 'highlight' ? { ...common, points: [point] } : { ...common, x1: point.x, y1: point.y, x2: point.x, y2: point.y };
  objects.push(item); selected = objects.length - 1; gesture = { mode: 'draw', start: point }; render();
});

canvas.addEventListener('pointermove', (event) => {
  if (!gesture) return;
  const point = canvasPoint(event);
  if (gesture.mode === 'crop') {
    cropPointer = { clientX: event.clientX, clientY: event.clientY };
    updateCropDraft(); return;
  }
  const item = objects[selected];
  if (!item) { endGesture(); return; }
  if (gesture.mode === 'draw' && (item.tool === 'pen' || item.tool === 'highlight')) {
    const last = item.points.at(-1); if (Math.hypot(point.x - last.x, point.y - last.y) > 1.5) item.points.push(point);
  } else if (gesture.mode === 'draw') { item.x2 = point.x; item.y2 = point.y; }
  else if (gesture.mode === 'move') {
    const box = bounds(gesture.original); const dx = Math.max(cropRect.x - box.x, Math.min(cropRect.x + cropRect.width - box.x - box.width, point.x - gesture.start.x)); const dy = Math.max(cropRect.y - box.y, Math.min(cropRect.y + cropRect.height - box.y - box.height, point.y - gesture.start.y)); moveItem(item, gesture.original, dx, dy);
  } else if (gesture.mode === 'curve') moveCurve(item, point);
  else if (gesture.mode === 'tail') moveTail(item, point);
  else resizeItem(item, gesture.original, point);
  render();
});

function endGesture() {
  if (!gesture && capturedPointer === null) return;
  const pointer = capturedPointer;
  gesture = null; capturedPointer = null;
  stopCropScroll();
  if (pointer !== null && canvas.hasPointerCapture(pointer)) canvas.releasePointerCapture(pointer);
  syncControls(); render();
}
canvas.addEventListener('pointerup', endGesture); canvas.addEventListener('pointercancel', endGesture);
canvas.addEventListener('lostpointercapture', endGesture);
stage.addEventListener('scroll', () => { if (gesture?.mode === 'crop') updateCropDraft(); updateTailHandle(); updateCurveHandle(); });
document.querySelector('#bubbleTailHandle').addEventListener('pointerdown', event => {
  if (event.button !== 0 || objects[selected]?.tool !== 'bubble' || textEditor) return;
  event.preventDefault(); endGesture(); checkpoint();
  gesture = { mode: 'tail', start: canvasPoint(event), original: structuredClone(objects[selected]) };
  canvas.setPointerCapture(event.pointerId); capturedPointer = event.pointerId;
});
document.querySelector('#bubbleTailHandle').addEventListener('keydown', event => {
  const delta = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] }[event.key];
  if (!delta || objects[selected]?.tool !== 'bubble') return;
  event.preventDefault(); checkpoint();
  const tip = annotationEffects.bubbleGeometry(ctx, objects[selected]).tip, step = event.shiftKey ? 10 : 1;
  moveTail(objects[selected], { x: tip.x + delta[0] * step, y: tip.y + delta[1] * step }); render();
});
canvas.addEventListener('dblclick', (event) => { const point = canvasPoint(event); const index = objects.findLastIndex((item) => ['text', 'bubble'].includes(item.tool) && hitTest(point, item)); if (index >= 0) { selected = index; syncControls(); openTextEditor(point, event, index); } });

function syncControls(fromSelection = true) {
  if (typeof syncLayerControls === 'function') syncLayerControls();
  const item = objects[selected];
  if (fromSelection && item) { colorInput.value = item.color; sizeInput.value = Math.round(item.size); opacityInput.value = Math.round((item.opacity ?? 1) * 100); strokePattern = item.stroke || 'solid'; }
  if (typeof syncProjectControls === 'function') syncProjectControls();
  document.querySelectorAll('[data-stroke]').forEach(button => {
    button.disabled = !strokeTools.has(item?.tool || activeTool);
    button.setAttribute('aria-pressed', String(button.dataset.stroke === strokePattern));
  });
  document.querySelector('#colorValue').textContent = colorInput.value.toUpperCase();
  document.querySelector('#sizeValue').textContent = `${sizeInput.value} px`;
  document.querySelector('#opacityValue').textContent = `${opacityInput.value}%`;
  palette.update();
  effects.sync();
}

function closeShapeMenus() {
  document.querySelectorAll('.shape-menu').forEach((menu) => { menu.hidden = true; });
  document.querySelectorAll('.shape-toggle').forEach((button) => button.setAttribute('aria-expanded', 'false'));
}

document.querySelectorAll('[data-tool]').forEach((button) => button.onclick = () => setTool(button.dataset.tool));
document.querySelectorAll('[data-stroke]').forEach(button => button.onclick = () => {
  const item = objects[selected];
  const pattern = button.dataset.stroke;
  if (item && strokeTools.has(item.tool) && item.stroke !== pattern) { checkpoint(); item.stroke = pattern; }
  strokePattern = pattern; syncControls(false); render();
});
document.querySelectorAll('.shape-toggle').forEach((button) => button.onclick = () => {
  const menu = document.getElementById(button.getAttribute('aria-controls'));
  const open = menu.hidden; closeShapeMenus(); menu.hidden = !open;
  button.setAttribute('aria-expanded', String(open));
  if (open) {
    menu.style.left = '0px';
    const box = menu.getBoundingClientRect();
    menu.style.left = `${Math.max(8 - box.left, Math.min(0, innerWidth - 8 - box.right))}px`;
    menu.querySelector('button').focus();
  }
});
document.addEventListener('pointerdown', (event) => { if (!event.target.closest('.shape-group')) closeShapeMenus(); });
document.querySelectorAll('.shape-menu').forEach((menu) => menu.addEventListener('keydown', (event) => {
  const choices = [...menu.querySelectorAll('button')];
  const index = choices.indexOf(document.activeElement);
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault(); choices[(index + (event.key === 'ArrowDown' ? 1 : choices.length - 1)) % choices.length].focus();
  }
  if (event.key === 'Escape') {
    event.preventDefault(); event.stopPropagation(); closeShapeMenus();
    menu.closest('.shape-group').querySelector('.shape-toggle').focus();
  }
}));
for (const [input, property] of [[colorInput, 'color'], [sizeInput, 'size'], [opacityInput, 'opacity']]) {
  let editing = null;
  const update = () => {
    const item = objects[selected];
    const value = property === 'color' ? input.value : Number(input.value) / (property === 'opacity' ? 100 : 1);
    if (property === 'color' && activeTool !== 'highlight') drawingColor = value;
    if (item && item[property] !== value) {
      // One undo entry per slider drag or keyboard adjustment.
      if (editing !== item) { checkpoint(); editing = item; }
      item[property] = value;
    }
    syncControls(false);
    render();
  };
  input.addEventListener('input', update);
  input.addEventListener('change', () => { update(); editing = null; });
  input.addEventListener('blur', () => { editing = null; });
}
const palette = bindColorPalette((color) => {
  if (activeTool !== 'highlight') drawingColor = color;
  const item = objects[selected];
  if (item && item.color !== color) { checkpoint(); item.color = color; }
  colorInput.value = color; syncControls(false); render();
});
const effects = bindAnnotationEffects({ getItem: () => objects[selected], getTool: () => activeTool, getColor: () => colorInput.value, checkpoint, render, palette });
function removeSelected() { if (selected < 0) return; endGesture(); checkpoint(); const item=objects[selected]; objects=objects.filter((o,i)=>i!==selected && (!item.id || o.owner!==item.id)); selected = -1; syncControls(false); render(); }
function history(backward) { const from = backward ? undoStack : redoStack, to = backward ? redoStack : undoStack; if (!from.length) return; endGesture(); to.push(JSON.stringify(projectState())); const state = JSON.parse(from.pop()); restoreProjectState(state); canvas.width = cropRect.width; canvas.height = cropRect.height; selected = -1; cropDraft = null; updateImageInfo(); syncControls(false); fitCanvas(); }
function updateImageInfo() { document.querySelector('#imageInfo').textContent = `${canvas.width.toLocaleString()} × ${canvas.height.toLocaleString()} px`; }
document.querySelector('#applyCrop').onclick = () => {
  if (!cropDraft || cropDraft.width < 20 || cropDraft.height < 20) { showToast('ลากกรอบครอบตัดบนภาพก่อน'); return; }
  if (cropImageIndex >= 0) {
    try { cropSelectedImage(cropDraft, cropImageIndex); setTool('select'); render(); }
    catch (error) { showToast(error.message); }
    return;
  }
  const x = Math.max(cropRect.x, Math.min(cropRect.x + cropRect.width, Math.round(cropDraft.x)));
  const y = Math.max(cropRect.y, Math.min(cropRect.y + cropRect.height, Math.round(cropDraft.y)));
  const right = Math.max(x, Math.min(cropRect.x + cropRect.width, Math.round(cropDraft.x + cropDraft.width)));
  const bottom = Math.max(y, Math.min(cropRect.y + cropRect.height, Math.round(cropDraft.y + cropDraft.height)));
  const nextCrop = { x, y, width: right - x, height: bottom - y };
  if (nextCrop.width < 20 || nextCrop.height < 20) { showToast('ลากกรอบครอบตัดบนภาพก่อน'); return; }
  endGesture();
  checkpoint();
  cropRect = nextCrop;
  canvas.width = cropRect.width; canvas.height = cropRect.height;
  updateImageInfo(); setTool('select'); fitCanvas();
};
document.querySelector('#cancelCrop').onclick = () => setTool('select');
document.querySelector('#deleteButton').onclick = removeSelected;
document.querySelector('#undoButton').onclick = () => history(true);
document.querySelector('#redoButton').onclick = () => history(false);
document.querySelector('#clearButton').onclick = () => { if (objects.length) { endGesture(); checkpoint(); objects = objects.filter(o=>o.tool==='image'); selected = -1; syncControls(false); render(); } };
document.addEventListener('keydown', (event) => { if (document.querySelector('dialog[open]') || event.target.closest('input, textarea')) return; if (event.key === 'Delete' || event.key === 'Backspace') removeSelected(); if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') history(!event.shiftKey); if (event.key === 'Escape') { selected = -1; setTool('select'); } });

async function exportBlob() { render(false); try { return await new Promise((resolve, reject) => canvas.toBlob((blob) => blob ? resolve(blob) : reject(new Error('สร้าง PNG ไม่สำเร็จ')), 'image/png')); } finally { render(); } }
function imageSnapshot() { return JSON.stringify(projectState()); }
function commitPendingEdit() { finishTextEdit?.(false); endGesture(); }
window.neoSnapEditor = {
  exportProject, loadProject, combineProjects, addImages, resizeWorkspace,
  hasUnkeptChanges() { commitPendingEdit(); return !baseImage || keptSnapshot !== imageSnapshot(); },
  markKept(snapshot) { keptSnapshot = snapshot; },
  exportImage() {
    if (!baseImage) throw new Error('ภาพยังไม่พร้อมบันทึก');
    commitPendingEdit();
    const snapshot = imageSnapshot();
    render(false);
    try { return { base64: canvas.toDataURL('image/png').split(',')[1], snapshot }; }
    finally { render(); }
  }
};
document.querySelector('#downloadButton').onclick = async () => {
  if (window.chrome?.webview) { window.chrome.webview.postMessage({ action: 'saveImage' }); return; }
  commitPendingEdit();
  const url = URL.createObjectURL(await exportBlob()); const link = document.createElement('a'); link.href = url; link.download = `snapzy-${Date.now()}.png`; link.click(); setTimeout(() => URL.revokeObjectURL(url), 60000);
};
const copyButton = document.querySelector('#copyButton');
let copyReset;
copyButton.onclick = async () => {
  clearTimeout(copyReset);
  copyButton.disabled = true; copyButton.classList.add('active'); copyButton.setAttribute('aria-busy', 'true'); copyButton.title = 'กำลังคัดลอก…';
  try {
    commitPendingEdit();
    const snapshot = imageSnapshot();
    await navigator.clipboard.write([new ClipboardItem({ 'image/png': exportBlob() })]);
    neoSnapEditor.markKept(snapshot);
    copyButton.classList.add('copied'); copyButton.setAttribute('aria-pressed', 'true'); copyButton.title = 'คัดลอกแล้ว';
    copyButton.innerHTML = '<i data-lucide="check"></i>'; lucide.createIcons(); showToast('คัดลอกภาพแล้ว');
  } catch { copyButton.classList.remove('active'); copyButton.title = 'คัดลอกไม่สำเร็จ'; showToast('คัดลอกไม่ได้ กรุณาบันทึก PNG'); }
  finally {
    copyButton.disabled = false; copyButton.setAttribute('aria-busy', 'false');
    copyReset = setTimeout(() => { copyButton.classList.remove('active', 'copied'); copyButton.setAttribute('aria-pressed', 'false'); copyButton.title = 'คัดลอกภาพ'; copyButton.innerHTML = '<i data-lucide="copy"></i>'; lucide.createIcons(); }, 2000);
  }
};
document.querySelector('#homeLink').onclick = (event) => { event.preventDefault(); window.chrome?.webview?.postMessage('showLauncher'); };
document.querySelectorAll('[data-view]').forEach(button => button.onclick = () => setView(button.dataset.view));
window.addEventListener('resize', fitCanvas);

(async function init() {
  try {
    await document.fonts.ready;
    const imagePath = new URLSearchParams(location.search).get('image');
    const payload = imagePath ? { type: 'image', dataUrl: imagePath } : null;
    if (!payload && !new URLSearchParams(location.search).has('project')) throw new Error('ไม่พบภาพที่จับไว้');
    if (new URLSearchParams(location.search).has('project')) {
      const response=await fetch(new URLSearchParams(location.search).get('project'));
      if(!response.ok) throw new Error('อ่านไฟล์งานไม่ได้');
      const project=await response.json();
      if(project.format==='snapzy-combine') await combineProjects(project.projects,project.layout,project.gap);
      else await loadProject(project,true);
    } else {
      const image = await loadImage(payload.dataUrl);
      registerBaseImage(image);
      baseImage = image; cropRect = { x: 0, y: 0, width: image.width, height: image.height }; canvas.width = image.width; canvas.height = image.height; updateImageInfo(); document.querySelector('#loading').hidden = true; fitCanvas(); if (payload.notice) showToast(payload.notice);
    }
    window.chrome?.webview?.postMessage({ action: 'editorReady' });
  } catch (error) { const loading = document.querySelector('#loading'); loading.innerHTML = ''; const message = document.createElement('p'); message.textContent = error.message; loading.append(message); }
})();

lucide.createIcons();
setTool('select');
const editorQuery = new URLSearchParams(location.search);
const editorI18n = SnapCraftI18n.init({
  language: editorQuery.get('language') || document.documentElement.lang,
  productName: editorQuery.get('product') || 'SnapZy',
  onLanguageChange: language => {
    document.querySelector('#editorAboutDialog details summary').textContent = SnapCraftI18n.formatReleaseLabel(appVersion, language);
    window.chrome?.webview?.postMessage({ action: 'language', language });
  }
});
const appVersion = window.chrome?.runtime?.getManifest?.().version || new URLSearchParams(location.search).get('version') || '1.0.0';
editorI18n.setProductName(editorQuery.get('product') || 'SnapZy');
document.querySelector('#editorAboutDialog details summary').textContent = SnapCraftI18n.formatReleaseLabel(appVersion, editorQuery.get('language') || document.documentElement.lang);
document.querySelector('#appVersion').textContent = `v${appVersion}`;
document.querySelector('#editorAboutVersion').textContent = appVersion;
document.querySelector('#editorAbout').onclick = () => document.querySelector('#editorAboutDialog').showModal();
document.querySelector('#closeEditorAbout').onclick = () => document.querySelector('#editorAboutDialog').close();
