(() => {
  const thToEn = {
    'พร้อมใช้งาน':'Ready','เลือกภาพเพื่อแก้ไข':'Browse images','ตั้งค่าคีย์ลัด':'Keyboard shortcuts','เกี่ยวกับ SnapZy':'About SnapZy','จับภาพ':'Capture',
    'บันทึกคีย์ลัดสำเร็จ':'Shortcut saved successfully','ตกลง':'OK','วิธีใช้เครื่องมือ':'Editor help','เปลี่ยนเป็นภาษาไทย':'Switch to Thai','เปลี่ยนเป็นภาษาอังกฤษ':'Switch to English',
    'เลือกพื้นที่หน้าจอ':'Select screen area','พื้นที่':'Area','เลือกหน้าต่าง':'Select window','หน้าต่าง':'Window','จับภาพยาวในบริเวณที่เลื่อน':'Capture scrolling content','ภาพยาว':'Scrolling',
    'บันทึกวิดีโอ MP4':'Record MP4','วิดีโอ':'Video','ทั้งหน้าจอ':'Full screen','หน่วง':'Delay','ทันที':'None','3 วินาที':'3 sec','5 วินาที':'5 sec','10 วินาที':'10 sec',
    'เปิดภาพ':'Open image','หน้าต่างใหม่':'New window','แท็บใหม่':'New tab','หยุดและบันทึก MP4':'Stop and save MP4','หยุด':'Stop','ยกเลิกวิดีโอ':'Cancel recording','ยกเลิก':'Cancel',
    'ผู้พัฒนา:':'Developer:','อัปเดต:':'Updated:','7 ตุลาคม 2026':'October 6, 2026','มีอะไรใหม่ใน 1.0.0':"What's new in 1.0.0",'จัดชั้นวัตถุและภาพ ขึ้น–ลงทีละชั้น หรือบนสุด–ล่างสุด':'Move images and objects one layer up or down, or bring them to the front or back.',
    'ย้ายชั้นภาพพร้อมคำอธิบายที่ติดกับภาพ':'Move an image together with its attached annotations.','รองรับ Undo/Redo และเก็บลำดับชั้นในไฟล์งาน':'Layer order is preserved in editable projects, with Undo and Redo support.',
    'ติดตั้งตัวบันทึก CPU (FFmpeg)':'Install CPU recorder (FFmpeg)','ปิด':'Close','บันทึกเสียงด้วยหรือไม่?':'Record audio?','เสียงจากเครื่อง':'System audio','ไมโครโฟน':'Microphone','ไม่บันทึกเสียง':'No audio','เริ่มบันทึก':'Start recording',
    'โหมด':'Mode','เปิด SnapZy':'Open SnapZy','จับภาพเลือกพื้นที่':'Capture area','จับภาพหน้าต่าง':'Capture window','จับภาพยาว':'Scrolling capture','เปิดใช้คีย์ลัดนี้':'Enable this shortcut','ปุ่มคีย์ลัด':'Shortcut key',
    'เลือก Ctrl หรือ Alt อย่างน้อยหนึ่งปุ่ม':'Select at least Ctrl or Alt.','บันทึก':'Save','กำลังเลือกพื้นที่':'Selecting area','กำลังเลือกหน้าต่าง':'Selecting window','กำลังจับภาพยาว':'Capturing scrolling page',
    'ภาพหน้าจอ':'Screenshot','กำลังเตรียมภาพ...':'Preparing image...','รวมภาพจากแท็บ':'Combine images from tabs','เพิ่มภาพ':'Add images','เปิดงาน SnapZy':'Open project','บันทึกงานเพื่อแก้ไขต่อ':'Save editable project','คัดลอกภาพ':'Copy image','บันทึก PNG':'Save PNG',
    'เครื่องมือ':'Tools','ขนาดพื้นที่งาน':'Canvas size','พื้นที่งาน':'Canvas','เลือกและย้ายวัตถุ':'Select and move objects','เลือก':'Select','ครอบตัดภาพ':'Crop image','ครอบตัด':'Crop','วาดเส้นอิสระ':'Freehand pen','ปากกา':'Pen','ปากกาไฮไลต์':'Highlighter','ไฮไลต์':'Highlight',
    'วาดเส้นตรง':'Draw line','เส้น':'Line','วาดลูกศร':'Draw arrow','ลูกศร':'Arrow','กรอบสี่เหลี่ยมโปร่ง':'Outline rectangle','กรอบ':'Outline','เลือกรูปทรงโปร่ง':'Choose outline shape','รูปทรงโปร่ง':'Outline shapes','สี่เหลี่ยมโปร่ง':'Outline rectangle','กรอบวงกลมโปร่ง':'Outline circle','วงกลมโปร่ง':'Outline circle',
    'สี่เหลี่ยมทึบ':'Filled rectangle','ทึบ':'Fill','เลือกรูปทรงทึบ':'Choose filled shape','รูปทรงทึบ':'Filled shapes','วงกลมทึบ':'Filled circle','เบลอเฉพาะพื้นที่':'Blur selected area','เบลอ':'Blur','เลขลำดับในวงกลม':'Numbered marker','เลขลำดับ':'Number','วางข้อความบนภาพ':'Add text to image','ข้อความ':'Text','ช่องคำพูด':'Speech bubble',
    'ลำดับชั้น':'Layer order','นำไปไว้บนสุด':'Bring to front','เลื่อนขึ้นหนึ่งชั้น':'Move up one layer','เลื่อนลงหนึ่งชั้น':'Move down one layer','ส่งไปไว้ล่างสุด':'Send to back','ลบวัตถุ':'Delete object','ลบ':'Delete','ย้อนกลับ':'Undo','ทำซ้ำ':'Redo','ล้างวัตถุทั้งหมด':'Clear all objects','ล้าง':'Clear',
    'สี':'Color','จานสี':'Color palette','ขนาด':'Size','รูปแบบเส้น':'Stroke style','เส้นทึบ':'Solid','เส้นประ':'Dashed','เส้นจุด':'Dotted','ความเข้ม':'Opacity','เงา':'Shadow','ตั้งค่าเงา':'Shadow settings','ขอบอักษร':'Text outline','ตั้งค่าขอบอักษรและช่องคำพูด':'Text outline and speech bubble settings',
    'ใช้การครอบตัด':'Apply crop','ใช้':'Apply','ยกเลิกการครอบตัด':'Cancel crop','ขนาดการแสดงภาพ':'Zoom','พอดีความกว้าง':'Fit width','แสดงภาพ 100%':'Show at 100%','จากไฟล์':'From files','จากคลิปบอร์ด':'From clipboard','จากแท็บ':'From tabs',
    'กว้าง':'Width','สูง':'Height','บันทึกงาน':'Save project','ไฟล์งานเก็บภาพต้นฉบับ รวมถึงข้อมูลที่เบลอหรือปิดทับไว้':'The project keeps the original image, including areas blurred or covered by annotations.',
    'บันทึก .neosnap':'Save .neosnap','ขนาดพื้นที่งาน':'Canvas size','เลื่อนปลายลูกศรช่องคำพูด':'Move speech bubble tail','ดัดเส้นหรือลูกศร':'Bend line or arrow','ล่าสุด':'Recent','สีเพิ่มเติม':'More colors',
    'สีเงา':'Shadow color','เลือกสีเงา':'Choose shadow color','ความฟุ้ง':'Blur','แนวนอน':'Horizontal offset','แนวตั้ง':'Vertical offset','ตั้งค่าข้อความ':'Text settings','สีขอบอักษร':'Text outline color','เลือกสีขอบอักษร':'Choose text outline color',
    'ความหนาขอบ':'Outline width','สีช่องคำพูด':'Bubble fill','เลือกสีช่องคำพูด':'Choose bubble fill color','สีกรอบ':'Border color','เลือกสีกรอบช่องคำพูด':'Choose bubble border color','ความหนากรอบ':'Border width','ผู้พัฒนา:':'Developer:','ปิด':'Close',
    'อ่านภาพไม่ได้':'Unable to read image','พิมพ์ข้อความ…':'Type text…','ลากกรอบครอบตัดบนภาพก่อน':'Drag a crop rectangle over the image first','สร้าง PNG ไม่สำเร็จ':'Could not create PNG','ภาพยังไม่พร้อมบันทึก':'Image is not ready to save',
    'กำลังคัดลอก…':'Copying…','กำลังบันทึก…':'Saving…','คัดลอกแล้ว':'Copied','คัดลอกภาพแล้ว':'Image copied','คัดลอกไม่สำเร็จ':'Copy failed','คัดลอกไม่ได้ กรุณาบันทึก PNG':'Could not copy. Please save as PNG.',
    'ไม่พบภาพที่จับไว้':'No captured image found','อ่านไฟล์งานไม่ได้':'Could not read project','กรุณาเปิดพรีวิวจาก SnapZy':'Open the preview from SnapZy','เล่นคลิปไม่ได้ แต่ยังคัดลอกหรือบันทึกไฟล์ได้':'Could not play the clip. You can still copy or save it.',
    'ไม่พบคลิป':'Clip not found','พร้อมใช้งาน':'Ready','เลือกภาพ':'Choose image','ภาษา':'Language','English':'English','ไทย':'Thai',
    'ลากคลิปไปยังแอปอื่น':'Drag clip to another app','คัดลอกคลิป':'Copy clip','บันทึก MP4':'Save MP4','กำลังเตรียมคลิป…':'Preparing clip…'
  };
  const enToTh = Object.fromEntries(Object.entries(thToEn).map(([th, en]) => [en, th]));
  const normalizeLanguage = value => value === 'th' ? 'th' : 'en';
  const formatReleaseLabel = (version, language) => normalizeLanguage(language) === 'en' ? `What's new in ${version}` : `มีอะไรใหม่ใน ${version}`;
  function translateText(value, language, productName = 'SnapZy') {
    const mode = normalizeLanguage(language);
    const leading = value.match(/^\s*/)?.[0] || '';
    const trailing = value.match(/\s*$/)?.[0] || '';
    const phrase = value.slice(leading.length, value.length - trailing.length || undefined);
    const canonical = phrase.replace(/SnapZy|Snapzy/gi, 'SnapZy');
    let translated = mode === 'en' ? (thToEn[canonical] || canonical) : (enToTh[canonical] || canonical);
    translated = translated.replace(/SnapZy|Snapzy/gi, productName);
    return `${leading}${translated}${trailing}`;
  }

  const textSource = new WeakMap();
  const textRendered = new WeakMap();
  const attributeState = new WeakMap();
  let language = 'th';
  let productName = 'SnapZy';
  let observer;

  function updateProductIcon() {
    for (const img of document.querySelectorAll('[data-product-icon]')) img.src = productName.toLowerCase() === 'snapzy' ? 'icons/snapzy.svg' : 'icons/snapzy.svg';
  }

  function translateNode(node) {
    if (node.nodeType === Node.TEXT_NODE) {
      if (/^(SCRIPT|STYLE)$/.test(node.parentElement?.tagName || '')) return;
      if (textRendered.get(node) !== node.nodeValue) textSource.set(node, node.nodeValue || '');
      const next = translateText(textSource.get(node) || '', language, productName);
      textRendered.set(node, next);
      if (node.nodeValue !== next) node.nodeValue = next;
      return;
    }
    if (node.nodeType !== Node.ELEMENT_NODE && node.nodeType !== Node.DOCUMENT_NODE) return;
    if (node.nodeType === Node.ELEMENT_NODE) {
      let states = attributeState.get(node);
      if (!states) { states = new Map(); attributeState.set(node, states); }
      for (const name of ['title', 'aria-label', 'placeholder', 'alt']) {
        if (!node.hasAttribute(name)) continue;
        const value = node.getAttribute(name);
        let state = states.get(name);
        if (!state || state.rendered !== value) state = {source: value, rendered: value};
        const next = translateText(state.source, language, productName);
        state.rendered = next; states.set(name, state);
        if (value !== next) node.setAttribute(name, next);
      }
    }
    for (const child of node.childNodes) translateNode(child);
  }

  function setLanguage(value) {
    language = normalizeLanguage(value);
    document.documentElement.lang = language;
    for (const select of document.querySelectorAll('[data-language-select]')) select.value = language;
    for (const button of document.querySelectorAll('[data-language-toggle]')) {
      button.textContent = language.toUpperCase();
      const label = language === 'en' ? 'Switch to Thai' : 'เปลี่ยนเป็นภาษาอังกฤษ';
      button.title = label; button.setAttribute('aria-label', label);
    }
    translateNode(document.body);
    document.dispatchEvent(new CustomEvent('snapcraft:language', { detail: { language } }));
  }

  function init(options = {}) {
    productName = options.productName || productName;
    updateProductIcon();
    for (const button of document.querySelectorAll('[data-language-toggle]')) button.addEventListener('click', () => {
      setLanguage(language === 'en' ? 'th' : 'en');
      options.onLanguageChange?.(language);
    });
    for (const select of document.querySelectorAll('[data-language-select]')) {
      select.value = normalizeLanguage(options.language || select.value || 'th');
      select.addEventListener('change', () => {
        setLanguage(select.value);
        options.onLanguageChange?.(language);
      });
    }
    setLanguage(options.language || 'th');
    observer?.disconnect();
    observer = new MutationObserver(records => {
      for (const record of records) {
        if (record.type === 'characterData') translateNode(record.target);
        else if (record.type === 'attributes') translateNode(record.target);
        else for (const node of record.addedNodes) translateNode(node);
      }
    });
    observer.observe(document.body, {subtree:true, childList:true, characterData:true, attributes:true, attributeFilter:['title','aria-label','placeholder','alt']});
    return {setLanguage, formatReleaseLabel, setProductName(name) { productName = name || productName; updateProductIcon(); translateNode(document.body); }};
  }

  const api = {normalizeLanguage, translateText, formatReleaseLabel, init};
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else globalThis.SnapCraftI18n = api;
})();
