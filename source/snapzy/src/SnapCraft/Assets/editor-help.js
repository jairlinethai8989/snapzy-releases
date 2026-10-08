(() => {
  // Keep the guide separate from drawing logic; entries follow the toolbar order.
  const groups = [
    ['Files and Output', 'ไฟล์และการส่งออก', [
      ['workToolsButton', 'Work tools', 'เครื่องมือทำงาน', 'Review scrolling joins, create PDF/Word reports, permanently cover private regions in a new image, recover local history, and compare two images.', 'ตรวจรอยต่อภาพยาว สร้างรายงาน PDF/Word ปิดข้อมูลในภาพใหม่ กู้คืนประวัติในเครื่อง และเปรียบเทียบสองภาพ'],
      ['homeLink', 'Home', 'หน้าหลัก', 'Return to the capture menu without closing this image.', 'กลับไปเมนูจับภาพโดยไม่ปิดภาพนี้'],
      ['editorAbout', 'About', 'เกี่ยวกับโปรแกรม', 'Show the version, developer and update details.', 'ดูเวอร์ชัน ผู้พัฒนา และรายการอัปเดต'],
      ['languageToggle', 'EN / TH', 'EN / TH', 'Click to switch between English and Thai.', 'กดเพื่อสลับภาษาอังกฤษและไทย'],
      ['editorHelpButton', 'Help', 'วิธีใช้', 'Show this guide. Close with the X button or Esc.', 'เปิดคู่มือนี้ ปิดด้วยปุ่ม X หรือ Esc'],
      ['combineButton addCaptureTabs', 'Combine tabs', 'รวมภาพจากแท็บ', 'Choose captured tabs and arrange them vertically or horizontally.', 'เลือกภาพจากแท็บแล้วจัดเรียงแนวตั้งหรือแนวนอน'],
      ['addImagesButton chooseImageFiles pasteImageButton', 'Add images', 'เพิ่มภาพ', 'Add files, paste from the clipboard or use captured tabs on the same canvas.', 'เพิ่มไฟล์ภาพ ภาพจากคลิปบอร์ด หรือแท็บที่จับไว้ลงในพื้นที่งานเดียวกัน'],
      ['ocrButton', 'Scan Text', 'อ่านข้อความ OCR', 'Open local OCR to review and copy text or table data. Read the selected region, or the whole image when nothing is selected, without cropping the image.', 'เปิด OCR ในเครื่องเพื่ออ่าน ตรวจสอบ และคัดลอกข้อความหรือตาราง อ่านเฉพาะพื้นที่ที่เลือก หรือทั้งภาพเมื่อไม่ได้เลือก โดยไม่ตัดภาพต้นฉบับ'],
      ['openProjectButton', 'Open project', 'เปิดงาน', 'Open a .neosnap project to continue editing its images and objects.', 'เปิดไฟล์ .neosnap เพื่อแก้ไขภาพและวัตถุต่อ'],
      ['saveProjectButton confirmProjectSave', 'Save project', 'บันทึกงาน', 'Save an editable .neosnap file. It retains original images, including information under blur or covering shapes. Share a PNG instead when hiding sensitive data.', 'บันทึกไฟล์ .neosnap ที่แก้ไขต่อได้ ไฟล์ยังเก็บภาพต้นฉบับ รวมข้อมูลใต้บริเวณเบลอหรือปิดทับ หากปกปิดข้อมูลสำคัญให้ส่งเป็น PNG แทน'],
      ['copyButton', 'Copy image', 'คัดลอกภาพ', 'Copy the finished image to paste into a chat or another app.', 'คัดลอกภาพที่ตกแต่งแล้วไปวางในแชทหรือโปรแกรมอื่น'],
      ['downloadButton', 'Save PNG', 'บันทึก PNG', 'Save the finished image at its original pixel resolution; view zoom does not reduce output quality.', 'บันทึกภาพตามความละเอียดพิกเซลจริง การย่อภาพเพื่อดูไม่ลดคุณภาพไฟล์']
    ]],
    ['Drawing Tools', 'เครื่องมือวาด', [
      ['workspaceSizeButton confirmWorkspaceSize', 'Canvas size', 'ขนาดพื้นที่งาน', 'Set the canvas width and height in pixels.', 'กำหนดความกว้างและความสูงของพื้นที่งานเป็นพิกเซล'],
      ['select applyLayerSize', 'Select and move', 'เลือกและย้าย', 'Click an object or image to select it, drag to move it, or drag its handles to resize. Image dimensions can also be entered below.', 'คลิกเลือกวัตถุหรือภาพ ลากเพื่อย้าย ลากจุดจับเพื่อปรับขนาด หรือกรอกขนาดภาพในแถบด้านล่าง'],
      ['crop applyCrop cancelCrop', 'Crop', 'ครอบตัด', 'Drag a crop rectangle, adjust its edges and click Apply. Cancel keeps the image unchanged. Drag near the viewport edge to scroll a long image.', 'ลากกรอบตัดภาพ ปรับขอบ แล้วกดใช้ กดยกเลิกเพื่อคงภาพเดิม ลากใกล้ขอบหน้าจอเพื่อเลื่อนภาพยาว'],
      ['pen', 'Freehand pen', 'ปากกา', 'Drag to draw freely. Set the color and stroke width in the style bar.', 'ลากเพื่อวาดเส้นอิสระ เลือกสีและขนาดเส้นได้จากแถบปรับแต่ง'],
      ['highlight', 'Highlighter', 'ไฮไลต์', 'Drag over text to add a translucent highlight.', 'ลากเน้นข้อความด้วยสีโปร่งแสง'],
      ['line curveHandle', 'Line and curve', 'เส้นและเส้นโค้ง', 'Drag from the start to the end. Select the line and drag its curve handle to bend it.', 'ลากจากจุดเริ่มไปจุดสิ้นสุด เลือกเส้นแล้วลากจุดดัดเพื่อทำเส้นโค้ง'],
      ['arrow', 'Arrow', 'ลูกศร', 'Drag toward the point you want to emphasize. Select it and drag the curve handle to bend the arrow.', 'ลากไปยังจุดที่ต้องการชี้ เลือกลูกศรแล้วลากจุดดัดเพื่อทำลูกศรโค้ง'],
      ['box ellipse outlineShapes', 'Outline shapes', 'รูปทรงโปร่ง', 'Drag to draw a rectangle. Use the adjacent arrow menu for a circle or ellipse.', 'ลากวาดกรอบสี่เหลี่ยม กดลูกศรข้างปุ่มเพื่อเลือกวงกลมหรือวงรี'],
      ['block ellipseFill filledShapes', 'Filled shapes', 'รูปทรงทึบ', 'Draw a solid rectangle, circle or ellipse using the adjacent shape menu.', 'วาดสี่เหลี่ยม วงกลม หรือวงรีทึบ โดยเลือกรูปทรงจากเมนูข้างปุ่ม'],
      ['blur', 'Blur area', 'เบลอเฉพาะพื้นที่', 'Drag over the region to soften its details. Save or copy the finished image, not the editable project, when covering sensitive information.', 'ลากบริเวณที่ต้องการเบลอ หากปกปิดข้อมูลสำคัญให้บันทึกหรือคัดลอกภาพสำเร็จ ไม่ส่งไฟล์งานที่แก้ไขต่อได้'],
      ['number', 'Numbered markers', 'เลขลำดับ', 'Click to place 1, 2, 3 and so on. Deleting the last marker makes the next marker continue from the highest remaining number.', 'คลิกวางเลข 1, 2, 3 ต่อเนื่อง หากลบเลขล่าสุด เลขใหม่จะต่อจากเลขสูงสุดที่เหลือ'],
      ['text', 'Text', 'ข้อความ', 'Click to type. Click away or press Ctrl+Enter to finish; select to move or resize. Double-click existing text to edit it.', 'คลิกเพื่อพิมพ์ คลิกด้านนอกหรือกด Ctrl+Enter เพื่อจบ เลือกเพื่อย้ายหรือปรับขนาด ดับเบิลคลิกข้อความเดิมเพื่อแก้ไข'],
      ['bubble bubbleTailHandle', 'Speech bubble', 'ช่องคำพูด', 'Click to type in a bubble. Select it to move or resize, drag the tail handle to point elsewhere, and adjust fill and border in text settings.', 'คลิกพิมพ์ข้อความในช่องคำพูด เลือกเพื่อย้ายหรือปรับขนาด ลากจุดจับปลายลูกศรเพื่อเปลี่ยนจุดชี้ ปรับสีพื้นและกรอบในตั้งค่าข้อความ']
    ]],
    ['Layer Order and History', 'ลำดับชั้นและประวัติ', [
      ['layerFrontButton', 'Bring to front', 'นำไปไว้บนสุด', 'Put the selected image or object above all other layers.', 'นำภาพหรือวัตถุที่เลือกไปไว้เหนือทุกชั้น'],
      ['layerUpButton', 'Move up', 'เลื่อนขึ้นหนึ่งชั้น', 'Move the selection one layer forward.', 'เลื่อนสิ่งที่เลือกขึ้นหนึ่งชั้น'],
      ['layerDownButton', 'Move down', 'เลื่อนลงหนึ่งชั้น', 'Move the selection one layer backward.', 'เลื่อนสิ่งที่เลือกลงหนึ่งชั้น'],
      ['layerBackButton', 'Send to back', 'ส่งไปไว้ล่างสุด', 'Put the selection behind all other layers. Attached annotations move with their image.', 'ส่งสิ่งที่เลือกไว้ใต้ทุกชั้น คำอธิบายที่ติดกับภาพจะย้ายพร้อมภาพ'],
      ['deleteButton', 'Delete', 'ลบวัตถุ', 'Remove only the selected object or image.', 'ลบเฉพาะวัตถุหรือภาพที่เลือก'],
      ['undoButton', 'Undo', 'ย้อนกลับ', 'Undo the last change. Ctrl+Z also works.', 'ยกเลิกการเปลี่ยนแปลงล่าสุด หรือกด Ctrl+Z'],
      ['redoButton', 'Redo', 'ทำซ้ำ', 'Restore an undone change. Ctrl+Shift+Z also works.', 'คืนการเปลี่ยนแปลงที่ย้อนกลับ หรือกด Ctrl+Shift+Z'],
      ['clearButton', 'Clear objects', 'ล้างวัตถุทั้งหมด', 'Remove annotations while keeping images.', 'ล้างวัตถุที่วาดทั้งหมดโดยคงภาพไว้']
    ]],
    ['Styles and View', 'ปรับแต่งและมุมมอง', [
      ['paletteButton colorInput customColor', 'Color palette', 'จานสี', 'Pick a swatch, a recent color or a custom color. Applies to the selected object or the next drawing.', 'เลือกสีจากจานสี สีล่าสุด หรือสีเพิ่มเติม ใช้กับวัตถุที่เลือกหรือการวาดครั้งต่อไป'],
      ['sizeInput', 'Stroke width', 'ขนาดเส้น', 'Adjust the selected stroke in real time. New strokes start at 5 px.', 'ปรับขนาดเส้นที่เลือกได้ทันที เส้นเริ่มต้นมีขนาด 5 px'],
      ['solid dashed dotted', 'Solid / dashed / dotted', 'เส้นทึบ / ประ / จุด', 'Change the stroke pattern for lines, arrows and outline shapes.', 'เปลี่ยนรูปแบบเส้นของเส้น ลูกศร และกรอบ'],
      ['opacityInput', 'Opacity', 'ความเข้ม', 'Adjust the selected object from transparent to opaque without changing blur strength.', 'ปรับความจางถึงทึบของวัตถุที่เลือก ไม่ใช่ความแรงของการเบลอ'],
      ['shadowEnabled shadowSettingsButton shadowColor shadowBlur shadowOffsetX shadowOffsetY', 'Shadow', 'เงา', 'Enable a shadow on the selected item or new items. Open settings to adjust its color, softness and horizontal/vertical offsets.', 'เปิดเงาให้สิ่งที่เลือกหรือสิ่งที่จะวาด กดตั้งค่าเพื่อปรับสี ความฟุ้ง และระยะเงาแนวนอน/แนวตั้ง'],
      ['outlineEnabled textSettingsButton outlineColor outlineWidth bubbleFill bubbleBorder bubbleBorderWidth', 'Text outline and bubble settings', 'ขอบอักษรและช่องคำพูด', 'For text or bubbles, enable a text outline and set its color and thickness. Bubble fill and border are adjustable separately.', 'เลือกข้อความหรือช่องคำพูด เปิดขอบอักษรแล้วปรับสีและความหนา ช่องคำพูดปรับสีพื้นและกรอบแยกได้'],
      ['fit', 'Fit width', 'พอดีความกว้าง', 'Fit the image to the viewport width; long images remain scrollable.', 'แสดงภาพพอดีความกว้าง ภาพยาวยังเลื่อนดูได้'],
      ['actual', '100% view', 'แสดงภาพ 100%', 'View at actual pixel size to inspect sharpness without changing export resolution.', 'ดูตามขนาดพิกเซลจริงเพื่อตรวจความคมชัด ไม่เปลี่ยนความละเอียดไฟล์']
    ]]
  ];
  const dialog = document.querySelector('#editorHelpDialog');
  const content = document.querySelector('#editorHelpContent');
  const trigger = document.querySelector('#editorHelpButton');
  const guideIcons = document.querySelector('#editorHelpIcons').content;
  function renderGuide() {
    const thai = document.documentElement.lang === 'th';
    const fragment = document.createDocumentFragment();
    for (const [en, th, entries] of groups) {
      const section = document.createElement('section');
      const heading = document.createElement('h3'); heading.textContent = thai ? th : en; section.append(heading);
      const list = document.createElement('ol');
      for (const [ids, titleEn, titleTh, textEn, textTh] of entries) {
        const item = document.createElement('li'); item.dataset.helpFor = ids;
        const id = ids.split(' ')[0];
        const control = document.getElementById(id) || document.querySelector(`[data-tool="${id}"], [data-stroke="${id}"], [data-view="${id}"]`);
        const icon = guideIcons.querySelector(`[data-help-icon="${id}"]`) || control?.querySelector('svg');
        if (control?.classList.contains('filled-shape')) item.classList.add('filled-shape');
        if (icon) {
          const visual = document.createElement('span'); visual.className = 'help-icon'; visual.setAttribute('aria-hidden', 'true');
          const copy = icon.cloneNode(true); copy.removeAttribute('data-help-icon'); visual.append(copy);
          const numeral = control?.querySelector('.number-icon');
          if (numeral) visual.append(numeral.cloneNode(true));
          item.append(visual);
        }
        const title = document.createElement('b'); title.textContent = thai ? titleTh : titleEn;
        const description = document.createElement('p'); description.textContent = thai ? textTh : textEn;
        const body = document.createElement('div'); body.append(title, description); item.append(body); list.append(item);
      }
      section.append(list); fragment.append(section);
    }
    content.replaceChildren(fragment);
    lucide.createIcons();
  }
  trigger.onclick = () => { renderGuide(); dialog.showModal(); };
  document.querySelector('#closeEditorHelp').onclick = () => dialog.close();
  dialog.addEventListener('close', () => trigger.focus());
  document.addEventListener('snapcraft:language', () => { if (dialog.open) renderGuide(); });
})();
