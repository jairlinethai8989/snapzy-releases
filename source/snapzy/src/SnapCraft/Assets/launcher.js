const host = window.chrome?.webview;
const post = (action, extra = {}) => host?.postMessage({ action, ...extra });
const pageQuery = new URLSearchParams(location.search);
const i18n = SnapCraftI18n.init({
  language: pageQuery.get('language') || document.documentElement.lang,
  productName: pageQuery.get('product') || 'SnapZy',
  onLanguageChange: language => post('language', { language })
});
document.querySelector('#openProject').onclick = () => post('browseImages');
const delay = document.querySelector('#delay');
const open = document.querySelector('#open');
const state = document.querySelector('#state');
const recordingBar = document.querySelector('#recordingBar');
const videoMenu = document.querySelector('#videoMenu');
const audioDialog = document.querySelector('#audioDialog');
const aboutDialog = document.querySelector('#aboutDialog');
let pendingVideo = null;
let capturing = false;
let statusReset;
function idleState() {
  clearTimeout(statusReset);
  state.textContent = 'พร้อมใช้งาน'; state.title = ''; state.classList.remove('error');
}
function requestLayout() {
  post('layout', { expanded: [...document.querySelectorAll('dialog')].some(dialog => dialog.open) || !videoMenu.hidden });
}
const layoutObserver = new MutationObserver(requestLayout);
document.querySelectorAll('dialog').forEach(dialog => layoutObserver.observe(dialog, { attributes: true, attributeFilter: ['open'] }));
layoutObserver.observe(videoMenu, { attributes: true, attributeFilter: ['hidden'] });
const shortcutDialog = document.querySelector('#shortcutDialog');
const shortcutSavedDialog = document.querySelector('#shortcutSavedDialog');
document.querySelector('#closeShortcutSaved').onclick = () => shortcutSavedDialog.close();
const hotkeyKey = document.querySelector('#hotkeyKey');
const hotkeyMode = document.querySelector('#hotkeyMode');
let bindings = [
  { mode: 'launcher', modifiers: 6, key: 83, enabled: true },
  { mode: 'area', modifiers: 2, key: 44, enabled: true },
  { mode: 'window', modifiers: 3, key: 87, enabled: true },
  { mode: 'scroll', modifiers: 3, key: 76, enabled: true },
  { mode: 'video', modifiers: 3, key: 86, enabled: true }
];
function loadShortcut() {
  const binding = bindings.find((item) => item.mode === hotkeyMode.value);
  if (!binding) return;
  document.querySelector('#hotkeyCtrl').checked = !!(binding.modifiers & 2);
  document.querySelector('#hotkeyAlt').checked = !!(binding.modifiers & 1);
  document.querySelector('#hotkeyShift').checked = !!(binding.modifiers & 4);
  document.querySelector('#hotkeyEnabled').checked = binding.enabled !== false;
  hotkeyKey.value = String(binding.key);
  document.querySelector('#hotkeyStatus').textContent = binding.error || '';
}
for (const key of [...Array.from({ length: 26 }, (_, i) => i + 65), ...Array.from({ length: 10 }, (_, i) => i + 48), ...Array.from({ length: 11 }, (_, i) => i + 112)]) {
  hotkeyKey.add(new Option(key >= 112 ? `F${key - 111}` : String.fromCharCode(key), key));
}
hotkeyKey.add(new Option('PrtSc', '44'));
hotkeyKey.value = '83';
document.querySelector('#shortcutButton').onclick = () => { loadShortcut(); shortcutDialog.showModal(); };
hotkeyMode.onchange = loadShortcut;
document.querySelector('#closeShortcut').onclick = () => shortcutDialog.close();
document.querySelector('#saveShortcut').onclick = () => {
  const modifiers = (document.querySelector('#hotkeyCtrl').checked ? 2 : 0) | (document.querySelector('#hotkeyAlt').checked ? 1 : 0) | (document.querySelector('#hotkeyShift').checked ? 4 : 0);
  const enabled = document.querySelector('#hotkeyEnabled').checked;
  if (enabled && !(modifiers & 3)) { document.querySelector('#hotkeyStatus').textContent = 'เลือก Ctrl หรือ Alt อย่างน้อยหนึ่งปุ่ม'; return; }
  post('hotkey', { mode: hotkeyMode.value, modifiers, key: Number(hotkeyKey.value), enabled });
};
function selectMode(mode) {
  document.querySelectorAll('[data-action]').forEach((button) => {
    const active = button.dataset.action === mode;
    button.classList.toggle('active', active); button.setAttribute('aria-pressed', String(active));
    if (!button.hasAttribute('aria-busy')) button.setAttribute('aria-busy', 'false');
  });
}
function beginVideo(silent) {
  if (!pendingVideo) return;
  const action = pendingVideo; pendingVideo = null; audioDialog.close();
  post(action, { systemAudio: !silent && document.querySelector('#systemAudio').checked, microphoneAudio: !silent && document.querySelector('#microphoneAudio').checked });
}
document.querySelector('#aboutButton').onclick = () => aboutDialog.showModal();
document.querySelector('#closeAbout').onclick = () => aboutDialog.close();
document.querySelector('#installCpu').onclick = () => { aboutDialog.close(); post('installCpu'); };
document.querySelector('#cancelAudio').onclick = () => { pendingVideo = null; audioDialog.close(); };
document.querySelector('#silentVideo').onclick = () => beginVideo(true);
document.querySelector('#confirmAudio').onclick = () => beginVideo(false);
audioDialog.addEventListener('cancel', () => { pendingVideo = null; });
document.querySelectorAll('[data-action]').forEach((button) => {
  button.addEventListener('click', () => {
    selectMode(button.dataset.action);
    idleState();
    if (button.dataset.action === 'video') { videoMenu.hidden = !videoMenu.hidden; return; }
    videoMenu.hidden = true;
    post(button.dataset.action, { delayMs: Number(delay.value) });
  });
});
document.querySelectorAll('[data-video]').forEach((button) => button.addEventListener('click', () => { videoMenu.hidden = true; pendingVideo = button.dataset.video; audioDialog.showModal(); }));
function showDelay() { delay.dataset.delayed = String(Number(delay.value) > 0); }
delay.addEventListener('change', () => { showDelay(); post('settings', { delayMs: Number(delay.value), openMode: open.value }); });
open.addEventListener('change', () => post('settings', { delayMs: Number(delay.value), openMode: open.value }));
document.querySelector('#stop').addEventListener('click', () => post('stopVideo'));
document.querySelector('#cancel').addEventListener('click', () => post('cancelVideo'));
host?.addEventListener('message', (event) => {
  const message = event.data;
  if (message.type === 'settings') {
    delay.value = String(message.delayMs); open.value = message.openMode;
    showDelay();
    i18n.setProductName(message.productName);
    i18n.setLanguage(message.language);
    document.querySelector('#aboutDialog details summary').textContent = i18n.formatReleaseLabel(message.version, message.language);
    document.querySelector('#version').textContent = `v${message.version}`;
    document.querySelector('#aboutVersion').textContent = message.version;
    document.querySelector('#developer').textContent = message.developer;
    document.querySelector('#installCpu').hidden = message.cpuAvailable !== false;
    if (message.shortcuts) bindings = message.shortcuts;
    if (!shortcutDialog.open) loadShortcut();
  }
  if (message.type === 'hotkeySaved') {
    document.querySelector('#shortcutSavedMode').textContent = [...hotkeyMode.options].find(option => option.value === message.mode)?.textContent || '';
    shortcutDialog.close();
    loadShortcut();
    if (!shortcutSavedDialog.open) shortcutSavedDialog.showModal();
  }
  if (message.type === 'cpuAvailability') document.querySelector('#installCpu').hidden = message.value;
  if (message.type === 'about' && !aboutDialog.open) aboutDialog.showModal();
  if (message.type === 'mode') selectMode(message.value);
  if (message.type === 'idle') { idleState(); selectMode(null); }
  if (message.type === 'captureActivity') {
    capturing = message.value; clearTimeout(statusReset);
    selectMode(message.value ? message.mode : null);
    document.querySelectorAll('[data-action]').forEach(button => {
      const working = message.value && button.dataset.action === message.mode;
      button.classList.toggle('working', working); button.setAttribute('aria-busy', String(working)); button.disabled = message.value;
    });
    if (message.value) {
      state.textContent = { area: 'กำลังเลือกพื้นที่', window: 'กำลังเลือกหน้าต่าง', scroll: 'กำลังจับภาพยาว' }[message.mode];
      state.classList.remove('error');
    } else if (!state.classList.contains('error')) idleState();
  }
  if (message.type === 'videoPrompt') {
    selectMode('video'); videoMenu.hidden = true; pendingVideo = 'videoScreen';
    if (!audioDialog.open) audioDialog.showModal();
  }
  if (message.type === 'status') {
    state.textContent = message.text; state.classList.toggle('error', !!message.error);
    state.title = message.text;
    clearTimeout(statusReset);
    if (!capturing && !message.error) statusReset = setTimeout(idleState, 3500);
    if (shortcutDialog.open && message.error) document.querySelector('#hotkeyStatus').textContent = message.text;
  }
  if (message.type === 'busy') document.querySelectorAll('[data-action]').forEach((button) => { button.disabled = message.value; });
  if (message.type === 'recording') { recordingBar.hidden = !message.value; document.querySelectorAll('[data-action]').forEach((button) => { button.disabled = message.value; }); }
  if (message.type === 'elapsed') document.querySelector('#elapsed').textContent = message.text;
});
post('ready');
showDelay();
lucide.createIcons();
selectMode(null);
