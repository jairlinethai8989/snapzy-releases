const host = window.chrome?.webview;
const previewProduct = new URLSearchParams(location.search).get('product')?.toLowerCase() === 'snapzy' ? 'SnapZy' : 'SnapZy';
const previewI18n = SnapCraftI18n.init({productName: previewProduct, language: new URLSearchParams(location.search).get('language') || (previewProduct === 'SnapZy' ? 'en' : 'th')});
document.querySelector('.brand-link b').textContent = previewProduct;
const refreshPreviewTitle = () => document.title = `${previewProduct} | ${SnapCraftI18n.translateText('วิดีโอ', document.documentElement.lang)}`;
document.addEventListener('snapcraft:language', refreshPreviewTitle);
refreshPreviewTitle();
if (previewProduct === 'SnapZy') document.querySelector('.brand-link img').src = 'icons/snapzy.svg';
const video = document.querySelector('#clipVideo');
const status = document.querySelector('#clipStatus');
const buttons = [...document.querySelectorAll('.clip-actions button')];
let pending = null;
let ready = false;
const refresh = () => buttons.forEach(button => button.disabled = !ready || pending !== null);
function result(action, success, text) {
  if (pending === action) pending = null;
  const button = document.getElementById(action);
  button?.setAttribute('aria-busy', 'false');
  if (action === 'copyClip') button?.setAttribute('aria-pressed', String(success));
  status.textContent = text || ''; status.classList.toggle('error', !success); refresh();
}
for (const action of ['copyClip', 'saveClip']) document.getElementById(action).onclick = () => {
  if (pending) return;
  pending = action; refresh(); document.getElementById(action).setAttribute('aria-busy', 'true');
  status.textContent = action === 'copyClip' ? 'กำลังคัดลอก…' : 'กำลังบันทึก…'; status.classList.remove('error');
  if (host) host.postMessage({action}); else result(action, false, 'กรุณาเปิดพรีวิวจาก SnapZy');
};
host?.addEventListener('message', event => {
  const message = event.data;
  if (message.type === 'language') previewI18n.setLanguage(message.language);
  if (message.type === 'result') result(message.action, message.success, message.text);
});
let dragStart;
const drag = document.querySelector('#dragClip');
drag.addEventListener('pointerdown', event => { if (event.button === 0 && !drag.disabled) { dragStart = {x:event.clientX,y:event.clientY}; drag.setPointerCapture(event.pointerId); } });
drag.addEventListener('pointermove', event => {
  if (!dragStart || Math.hypot(event.clientX - dragStart.x,event.clientY - dragStart.y) < 6) return;
  dragStart = null; if (drag.hasPointerCapture(event.pointerId)) drag.releasePointerCapture(event.pointerId);
  host?.postMessage({action:'dragClip'});
});
drag.addEventListener('pointerup', () => dragStart = null);
drag.addEventListener('pointercancel', () => dragStart = null);
video.addEventListener('loadedmetadata', () => {
  const duration = Number.isFinite(video.duration) ? `${Math.floor(video.duration/60)}:${String(Math.floor(video.duration%60)).padStart(2,'0')}` : '';
  document.querySelector('#clipInfo').textContent = `${duration} · ${video.videoWidth} × ${video.videoHeight} px`;
});
video.addEventListener('error', () => {
  const error = document.querySelector('#playbackError'); error.hidden = false; error.textContent = 'เล่นคลิปไม่ได้ แต่ยังคัดลอกหรือบันทึกไฟล์ได้';
});
const clip = new URLSearchParams(location.search).get('clip');
if (clip) { video.src = clip; ready = true; status.textContent = ''; refresh(); }
else { status.textContent = 'ไม่พบคลิป'; status.classList.add('error'); }
lucide.createIcons();
