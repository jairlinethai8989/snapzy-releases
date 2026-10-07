const annotationEffects = (() => {
  const layer = document.createElement('canvas');
  const mask = document.createElement('canvas');
  function wrap(context, text, width) {
    const lines = [];
    const segmenter = typeof Intl.Segmenter === 'function' ? new Intl.Segmenter('th', { granularity: 'word' }) : null;
    const graphemes = typeof Intl.Segmenter === 'function' ? new Intl.Segmenter('th', { granularity: 'grapheme' }) : null;
    for (const paragraph of text.split('\n')) {
      let line = '';
      const words = segmenter ? [...segmenter.segment(paragraph)].map(s => s.segment) : paragraph.match(/\s+|\S+/gu) || [];
      for (const word of words) {
        if (!word.trim()) { if (line && context.measureText(line + word).width <= width) line += word; continue; }
        if (line && context.measureText(line + word).width > width) { lines.push(line.trimEnd()); line = ''; }
        if (context.measureText(word).width <= width) { line += word; continue; }
        const characters = graphemes ? [...graphemes.segment(word)].map(s => s.segment) : [...word];
        for (const character of characters) {
          if (line && context.measureText(line + character).width > width) { lines.push(line); line = ''; }
          line += character;
        }
      }
      lines.push(line.trimEnd());
    }
    return lines;
  }
  function bubbleLayout(context, item) {
    const fontSize = Math.max(12, item.size * 4);
    const lineHeight = fontSize * 1.25;
    const width = Math.max(20, item.bubbleWidth || 260);
    const padding = Math.min(14, width / 5);
    context.save(); context.font = `600 ${fontSize}px SnapSans, sans-serif`;
    const lines = wrap(context, item.text || '', Math.max(1, width - padding * 2));
    context.restore();
    const bodyHeight = Math.max(item.bubbleHeight || 0, lines.length * lineHeight + padding * 2);
    return { width, bodyHeight, height: bodyHeight + 16, padding, fontSize, lineHeight, lines };
  }
  function bubbleGeometry(context, item) {
    const layout = bubbleLayout(context, item);
    const { width, bodyHeight: height } = layout;
    const x = item.x1, y = item.y1;
    const radius = Math.min(12, width / 6, height / 6);
    const legacy = Math.min(24, width / 4);
    let tip = { x: x + (item.tail?.x ?? legacy), y: y + (item.tail?.y ?? height + 16) };
    let edge = 'bottom', start, end;
    if (!item.tail) {
      start = { x: x + legacy + Math.min(18, width / 4), y: y + height };
      end = { x: x + legacy, y: y + height };
    } else {
      const outside = [
        ['left', (x - tip.x) / width], ['right', (tip.x - x - width) / width],
        ['top', (y - tip.y) / height], ['bottom', (tip.y - y - height) / height]
      ].sort((a, b) => b[1] - a[1]);
      edge = outside[0][0];
      if (outside[0][1] < 0) {
        edge = [['left', tip.x - x], ['right', x + width - tip.x],
          ['top', tip.y - y], ['bottom', y + height - tip.y]].sort((a, b) => a[1] - b[1])[0][0];
        if (edge === 'left') tip.x = x - 16;
        if (edge === 'right') tip.x = x + width + 16;
        if (edge === 'top') tip.y = y - 16;
        if (edge === 'bottom') tip.y = y + height + 16;
      }
      const horizontal = edge === 'top' || edge === 'bottom';
      const length = horizontal ? width : height;
      const half = Math.min(9, (length - radius * 2) / 4);
      const origin = horizontal ? x : y;
      const center = Math.max(origin + radius + half,
        Math.min(origin + length - radius - half, horizontal ? tip.x : tip.y));
      if (edge === 'top') { start = { x: center - half, y }; end = { x: center + half, y }; }
      if (edge === 'right') { start = { x: x + width, y: center - half }; end = { x: x + width, y: center + half }; }
      if (edge === 'bottom') { start = { x: center + half, y: y + height }; end = { x: center - half, y: y + height }; }
      if (edge === 'left') { start = { x, y: center + half }; end = { x, y: center - half }; }
    }
    const left = Math.min(x, tip.x), top = Math.min(y, tip.y);
    return { layout, body: { x, y, width, height }, radius, tip, edge, start, end,
      bounds: { x: left, y: top, width: Math.max(x + width, tip.x) - left, height: Math.max(y + height, tip.y) - top } };
  }
  function bubblePath(context, x, y, layout, geometry) {
    const { width, bodyHeight } = layout;
    const radius = Math.min(12, width / 6, bodyHeight / 6);
    const tail = Math.min(24, width / 4);
    const pointer = edge => {
      if (geometry?.edge !== edge) return;
      context.lineTo(geometry.start.x, geometry.start.y);
      context.lineTo(geometry.tip.x, geometry.tip.y);
      context.lineTo(geometry.end.x, geometry.end.y);
    };
    context.beginPath();
    context.moveTo(x + radius, y); pointer('top'); context.lineTo(x + width - radius, y);
    context.quadraticCurveTo(x + width, y, x + width, y + radius);
    pointer('right');
    context.lineTo(x + width, y + bodyHeight - radius);
    context.quadraticCurveTo(x + width, y + bodyHeight, x + width - radius, y + bodyHeight);
    if (geometry) pointer('bottom');
    else {
      context.lineTo(x + tail + Math.min(18, width / 4), y + bodyHeight); context.lineTo(x + tail, y + bodyHeight + 16);
      context.lineTo(x + tail, y + bodyHeight);
    }
    context.lineTo(x + radius, y + bodyHeight);
    context.quadraticCurveTo(x, y + bodyHeight, x, y + bodyHeight - radius);
    pointer('left');
    context.lineTo(x, y + radius); context.quadraticCurveTo(x, y, x + radius, y);
    context.closePath();
  }
  function drawText(context, item, metrics, x, y) {
    context.font = `600 ${metrics.fontSize}px SnapSans, sans-serif`;
    context.textBaseline = 'top'; context.fillStyle = item.color;
    const outline = item.outline;
    metrics.lines.forEach((line, index) => {
      const top = y + index * metrics.lineHeight;
      if (outline?.enabled) {
        context.strokeStyle = outline.color; context.lineWidth = outline.width * 2;
        context.setLineDash([]); context.strokeText(line, x, top);
      }
      context.fillText(line, x, top);
    });
  }
  // Composite a complete silhouette once, rather than shadowing every paint pass.
  function withShadow(context, item, box, clip, paint) {
    const shadow = item.shadow;
    if (!shadow?.enabled) { paint(context); return; }
    const margin = Math.ceil(Math.max(item.size * 4 + 20, (item.outline?.width || 0) + 8));
    const reach = Math.ceil(shadow.blur * 3 + Math.max(Math.abs(shadow.x), Math.abs(shadow.y)));
    const left = Math.floor(Math.max(clip.x - reach, box.x - margin));
    const top = Math.floor(Math.max(clip.y - reach, box.y - margin));
    const right = Math.ceil(Math.min(clip.x + clip.width + reach, box.x + box.width + margin));
    const bottom = Math.ceil(Math.min(clip.y + clip.height + reach, box.y + box.height + margin));
    if (right <= left || bottom <= top) return;
    layer.width = right - left; layer.height = bottom - top;
    const target = layer.getContext('2d'); target.translate(-left, -top);
    paint(target);
    context.save();
    context.globalAlpha = item.opacity ?? 1;
    if (item.tool === 'highlight') context.globalCompositeOperation = 'multiply';
    context.shadowColor = shadow.color; context.shadowBlur = shadow.blur;
    context.shadowOffsetX = shadow.x; context.shadowOffsetY = shadow.y;
    context.drawImage(layer, left, top); context.restore();
  }
  function blurShadow(context, item, box, clip) {
    if (!item.shadow?.enabled) return;
    const s = item.shadow;
    const reach = Math.ceil(s.blur * 3 + Math.max(Math.abs(s.x), Math.abs(s.y)) + 2);
    const left = Math.floor(Math.max(clip.x, box.x - reach));
    const top = Math.floor(Math.max(clip.y, box.y - reach));
    const right = Math.ceil(Math.min(clip.x + clip.width, box.x + box.width + reach));
    const bottom = Math.ceil(Math.min(clip.y + clip.height, box.y + box.height + reach));
    if (right <= left || bottom <= top) return;
    mask.width = right - left; mask.height = bottom - top;
    const target = mask.getContext('2d'); target.translate(-left, -top);
    target.shadowColor = s.color; target.shadowBlur = s.blur;
    target.shadowOffsetX = s.x; target.shadowOffsetY = s.y;
    target.fillRect(box.x, box.y, box.width, box.height);
    target.shadowColor = 'transparent'; target.globalCompositeOperation = 'destination-out';
    target.fillRect(box.x, box.y, box.width, box.height);
    context.save(); context.globalAlpha = item.opacity ?? 1;
    context.drawImage(mask, left, top); context.restore();
  }
  return { bubbleLayout, bubbleGeometry, bubblePath, drawText, withShadow, blurShadow };
})();
