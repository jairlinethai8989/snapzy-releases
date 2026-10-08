(() => {
  const button = document.querySelector('#workToolsButton');
  const ocr = document.querySelector('#ocrButton');
  function language() {
    button.title = document.documentElement.lang === 'th' ? 'เครื่องมือทำงาน' : 'Work tools';
    button.setAttribute('aria-label', button.title);
    button.querySelector('span').textContent = button.title;
    ocr.title = document.documentElement.lang === 'th' ? 'อ่านข้อความ OCR' : 'Scan Text';
    ocr.setAttribute('aria-label', ocr.title);
  }
  button.onclick = () => {
    if (!baseImage) return;
    if (window.chrome?.webview) window.chrome.webview.postMessage({ action: 'productivity' });
    else showToast(document.documentElement.lang === 'th' ? 'เครื่องมือทำงานใช้ในโปรแกรม Windows' : 'Work tools are available in the Windows app');
  };
  ocr.onclick = () => {
    if (!baseImage) return;
    if (window.chrome?.webview) window.chrome.webview.postMessage({ action: 'ocr' });
    else showToast(document.documentElement.lang === 'th' ? 'OCR ใช้ในโปรแกรม Windows' : 'OCR is available in the Windows app');
  };
  neoSnapEditor.exportRegion = () => {
    commitPendingEdit();
    const area = cropDraft || (selected >= 0 ? bounds(objects[selected]) : cropRect);
    const x = Math.max(0, Math.floor(area.x - cropRect.x));
    const y = Math.max(0, Math.floor(area.y - cropRect.y));
    const right = Math.min(canvas.width, Math.ceil(area.x + area.width - cropRect.x));
    const bottom = Math.min(canvas.height, Math.ceil(area.y + area.height - cropRect.y));
    if (right <= x || bottom <= y) throw new Error('Select a region inside the image');
    const output = document.createElement('canvas'); output.width = right - x; output.height = bottom - y;
    render(false);
    try { output.getContext('2d').drawImage(canvas, x, y, output.width, output.height, 0, 0, output.width, output.height); return { base64: output.toDataURL('image/png').split(',')[1] }; }
    finally { render(); }
  };
  document.addEventListener('snapcraft:language', language); language();
})();
