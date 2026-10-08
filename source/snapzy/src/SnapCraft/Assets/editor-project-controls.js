function syncProjectControls() {
  const panel=document.querySelector('#imageLayerActions');
  if(!panel) return;
  const item=objects[selected];panel.hidden=item?.tool!=='image';
  if(panel.hidden) return;
  const box=bounds(item);
  if(document.activeElement?.id!=='layerWidth') document.querySelector('#layerWidth').value=Math.round(box.width);
  if(document.activeElement?.id!=='layerHeight') document.querySelector('#layerHeight').value=Math.round(box.height);
}

let projectCommandQueue=Promise.resolve();
function runProjectCommand(action) {
  const run=async()=>{
    const buttons=[...document.querySelectorAll('.top-actions button')];
    const workspace=document.querySelector('.editor-workspace');
    buttons.forEach(b=>b.disabled=true);workspace.inert=true;
    projectMessage('projectOperationStarted');
    try { await action(); }
    catch(error) {showToast(error.message);}
    finally {buttons.forEach(b=>b.disabled=false);workspace.inert=false;projectMessage('projectOperationFinished',{success:true});}
  };
  projectCommandQueue=projectCommandQueue.then(run,run);
  return projectCommandQueue;
}

function projectMessage(action,extra={}) {window.chrome?.webview?.postMessage({action,...extra});}
document.querySelector('#combineButton').onclick=()=>{
  if(window.chrome?.webview) projectMessage('combineTabs');
  else {document.querySelector('#addImagesDialog').showModal();}
};
document.querySelector('#addImagesButton').onclick=()=>document.querySelector('#addImagesDialog').showModal();
document.querySelector('#closeAddImages').onclick=()=>document.querySelector('#addImagesDialog').close();
document.querySelector('#chooseImageFiles').onclick=()=>{document.querySelector('#addImagesDialog').close();document.querySelector('#imageFiles').click();};
document.querySelector('#addCaptureTabs').onclick=()=>{
  document.querySelector('#addImagesDialog').close();
  if(window.chrome?.webview) projectMessage('appendTabs');
  else showToast('เลือกแท็บได้ในโปรแกรม Windows');
};
document.querySelector('#imageFiles').onchange=event=>runProjectCommand(async()=>{
  const files=[...event.target.files];event.target.value='';
  if(!files.length) return;
  if(files.length>100 || files.reduce((n,f)=>n+f.size,0)>projectLimits.bytes) throw new Error('ไฟล์ภาพใหญ่เกินไป');
  const urls=files.map(f=>URL.createObjectURL(f));
  try {await addImages(urls);} finally {urls.forEach(url=>URL.revokeObjectURL(url));}
});
document.querySelector('#pasteImageButton').onclick=()=>runProjectCommand(async()=>{
  const items=await navigator.clipboard.read(),urls=[];
  try {
    for(const item of items) {
      const type=item.types.find(t=>t.startsWith('image/'));
      if(type) urls.push(URL.createObjectURL(await item.getType(type)));
    }
    if(!urls.length) throw new Error('ไม่พบภาพในคลิปบอร์ด');
    await addImages(urls);document.querySelector('#addImagesDialog').close();
  } finally {urls.forEach(url=>URL.revokeObjectURL(url));}
});
document.addEventListener('paste',event=>{
  if(event.target.closest('input,textarea')) return;
  const files=[...event.clipboardData.files].filter(f=>f.type.startsWith('image/'));
  if(!files.length) return;
  event.preventDefault();runProjectCommand(async()=>{
    const urls=files.map(f=>URL.createObjectURL(f));
    try {await addImages(urls);} finally {urls.forEach(url=>URL.revokeObjectURL(url));}
  });
});
document.querySelector('#openProjectButton').onclick=()=>{
  if(window.chrome?.webview) projectMessage('openProject');
  else document.querySelector('#projectFile').click();
};
document.querySelector('#projectFile').onchange=event=>runProjectCommand(async()=>{
  const file=event.target.files[0];event.target.value='';if(!file)return;
  if(file.size>projectLimits.bytes) throw new Error('ไฟล์งานใหญ่เกินไป');
  if(baseImage && neoSnapEditor.hasUnkeptChanges() && !confirm('เปิดงานใหม่แทนงานปัจจุบันที่ยังไม่ได้บันทึก?')) return;
  await loadProject(JSON.parse(await file.text()),true);
});
document.querySelector('#saveProjectButton').onclick=()=>document.querySelector('#projectSaveDialog').showModal();
document.querySelector('#cancelProjectSave').onclick=()=>document.querySelector('#projectSaveDialog').close();
document.querySelector('#confirmProjectSave').onclick=()=>runProjectCommand(async()=>{
  document.querySelector('#projectSaveDialog').close();
  if(window.chrome?.webview) {projectMessage('saveProject');return;}
  const exported=exportProject(),url=URL.createObjectURL(new Blob([JSON.stringify(exported.project)],{type:'application/json'}));
  const a=document.createElement('a');a.href=url;a.download=`neo-snap-${Date.now()}.neosnap`;a.click();setTimeout(()=>URL.revokeObjectURL(url),60000);
  // A browser download cannot confirm that the destination was written successfully.
});

for(const id of ['layerWidth','layerHeight']) document.querySelector('#'+id).oninput=()=>{
  const item=objects[selected];if(item?.tool!=='image')return;
  const box=bounds(item),value=Number(document.querySelector('#'+id).value);
  if(!Number.isFinite(value)||value<20)return;
  document.querySelector(id==='layerWidth'?'#layerHeight':'#layerWidth').value=Math.round(value*(id==='layerWidth'?box.height/box.width:box.width/box.height));
};
document.querySelector('#applyLayerSize').onclick=()=>{
  const item=objects[selected];if(item?.tool!=='image')return;
  const width=Number(document.querySelector('#layerWidth').value);
  if(!Number.isFinite(width)||width<20)return;
  checkpoint();const original=structuredClone(item);
  resizeItem(item,original,{x:item.x1+width,y:item.y1+(item.y2-item.y1)*width/(item.x2-item.x1)});syncControls();render();
};

neoSnapEditor.appendTabs=async(projects,layout,gap)=>{
  try {await combineProjects(projects,layout,gap,true);projectMessage('projectOperationFinished',{success:true});}
  catch(error){showToast(error.message);projectMessage('projectOperationFinished',{success:false});}
};
document.querySelector('#workspaceSizeButton').onclick=()=>{
  document.querySelector('#workspaceWidth').value=canvas.width;
  document.querySelector('#workspaceHeight').value=canvas.height;
  document.querySelector('#workspaceSizeDialog').showModal();
};
document.querySelector('#cancelWorkspaceSize').onclick=()=>document.querySelector('#workspaceSizeDialog').close();
document.querySelector('#confirmWorkspaceSize').onclick=()=>{
  try {resizeWorkspace(Number(document.querySelector('#workspaceWidth').value),Number(document.querySelector('#workspaceHeight').value));document.querySelector('#workspaceSizeDialog').close();}
  catch(error){showToast(error.message);}
};
