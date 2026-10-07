// Embedded assets are immutable; history stores references, not repeated image bytes.
const projectAssets = new Map();
let projectBackground = 'base';
let projectWidth = 0, projectHeight = 0;
const projectLimits = { pixels: 80000000, edge: 32767, objects: 5000, bytes: 180000000 };

function projectDimensions(width, height) {
  if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1 || width > projectLimits.edge || height > projectLimits.edge || width * height > projectLimits.pixels)
    throw new Error('พื้นที่งานใหญ่เกินไป กรุณาแบ่งเป็นหลายงาน');
}

async function projectAsset(dataUrl) {
  if (typeof dataUrl !== 'string' || dataUrl.length > projectLimits.bytes || !/^data:image\/png;base64,[A-Za-z0-9+/]+=*$/.test(dataUrl)) throw new Error('ไฟล์งานต้องใช้ภาพ PNG ภายในไฟล์เท่านั้น');
  const header = atob(dataUrl.slice(22, 22 + 44));
  if (header.slice(0,8) !== '\x89PNG\r\n\x1a\n' || header.slice(12,16) !== 'IHDR') throw new Error('ข้อมูล PNG ไม่ถูกต้อง');
  const number = offset => [0,1,2,3].reduce((n,i)=>n*256+header.charCodeAt(offset+i),0);
  projectDimensions(number(16), number(20));
  const image = await loadImage(dataUrl);
  return { dataUrl, image };
}

function imageDataUrl(image) {
  const c = document.createElement('canvas'); c.width = image.width; c.height = image.height;
  c.getContext('2d').drawImage(image,0,0); return c.toDataURL('image/png');
}

function registerBaseImage(image) {
  projectBackground = 'base'; projectWidth = image.width; projectHeight = image.height;
  projectAssets.set('base', { image, dataUrl: null });
}

function projectState() { return { objects, cropRect, background: projectBackground, width: projectWidth, height: projectHeight }; }
function restoreProjectState(state) {
  objects = state.objects; cropRect = state.cropRect;
  projectBackground = 'background' in state ? state.background : 'base';
  projectWidth = state.width || baseImage.width; projectHeight = state.height || baseImage.height;
  baseImage = projectBackground ? projectAssets.get(projectBackground).image : projectSurface(projectWidth,projectHeight);
}
function projectSurface(width,height) {
  const c=document.createElement('canvas');c.width=width;c.height=height;
  const x=c.getContext('2d');x.fillStyle='#ffffff';x.fillRect(0,0,width,height);return c;
}

function exportProject() {
  commitPendingEdit();
  const ids = new Set(objects.filter(o=>o.tool==='image').map(o=>o.assetId));
  if (projectBackground) ids.add(projectBackground);
  const assets=[...ids].map(id=>{
    const asset=projectAssets.get(id);
    asset.dataUrl ||= imageDataUrl(asset.image);
    return {id,dataUrl:asset.dataUrl};
  });
  return { project: { format:'neo-snap',version:1,width:projectWidth,height:projectHeight,background:projectBackground,cropRect:structuredClone(cropRect),assets,objects:structuredClone(objects) }, snapshot:imageSnapshot() };
}

async function decodeProject(project, cache = new Map([...projectAssets.values()].filter(a=>a.dataUrl).map(a=>[a.dataUrl,a]))) {
  if (!project || project.format !== 'neo-snap' || project.version !== 1) throw new Error('ไฟล์งานหรือเวอร์ชันไม่รองรับ');
  if (JSON.stringify(project).length > projectLimits.bytes) throw new Error('ไฟล์งานใหญ่เกินไป');
  projectDimensions(project.width,project.height);
  if (!Array.isArray(project.assets) || project.assets.length>100 || !Array.isArray(project.objects) || project.objects.length>projectLimits.objects) throw new Error('ไฟล์งานมีรายการมากเกินไป');
  const assets=new Map(); let pixels=0;
  for (const asset of project.assets) {
    if (!asset || typeof asset.id!=='string' || asset.id.length>100 || assets.has(asset.id)) throw new Error('รหัสภาพในไฟล์งานไม่ถูกต้อง');
    const loaded=cache.get(asset.dataUrl) || await projectAsset(asset.dataUrl);cache.set(asset.dataUrl,loaded); pixels+=loaded.image.width*loaded.image.height;
    if (pixels>projectLimits.pixels) throw new Error('ภาพในไฟล์งานใหญ่เกินไป');
    assets.set(asset.id,loaded);
  }
  if (project.background !== null && !assets.has(project.background)) throw new Error('ไฟล์งานไม่มีภาพต้นฉบับ');
  if (project.background && (assets.get(project.background).image.width !== project.width || assets.get(project.background).image.height !== project.height)) throw new Error('ขนาดภาพต้นฉบับไม่ตรงกับพื้นที่งาน');
  const rect = (r,maxWidth,maxHeight) => {
    if (!r || !['x','y','width','height'].every(k=>Number.isFinite(r[k])) || r.x<0 || r.y<0 || r.width<1 || r.height<1 || r.x+r.width>maxWidth+.001 || r.y+r.height>maxHeight+.001) throw new Error('กรอบภาพในไฟล์งานไม่ถูกต้อง');
  };
  rect(project.cropRect,project.width,project.height);
  projectDimensions(project.cropRect.width,project.cropRect.height);
  const tools=new Set(['image','pen','highlight','line','arrow','box','block','ellipse','ellipseFill','number','blur','text','bubble']);
  const images=new Map();
  const inspect=(value,depth=0)=>{
    if(depth>10) throw new Error('โครงสร้างไฟล์งานไม่ถูกต้อง');
    if(typeof value==='number' && (!Number.isFinite(value)||Math.abs(value)>1000000)) throw new Error('ตำแหน่งวัตถุไม่ถูกต้อง');
    if(typeof value==='string' && value.length>100000) throw new Error('ข้อความในไฟล์งานยาวเกินไป');
    if(value && typeof value==='object') for(const [key,child] of Object.entries(value)) {
      if(['__proto__','constructor','prototype'].includes(key)) throw new Error('โครงสร้างไฟล์งานไม่ถูกต้อง');inspect(child,depth+1);
    }
  };
  for(const item of project.objects) {
    inspect(item);
    const color=value=>typeof value==='string' && /^#[0-9a-f]{6}$/i.test(value);
    if(item.shadow && (typeof item.shadow.enabled!=='boolean' || !color(item.shadow.color) || !Number.isFinite(item.shadow.blur) || item.shadow.blur<0 || item.shadow.blur>128 || !['x','y'].every(k=>Number.isFinite(item.shadow[k])&&Math.abs(item.shadow[k])<=128))) throw new Error('ค่าเงาในไฟล์งานไม่ถูกต้อง');
    if(item.outline && (typeof item.outline.enabled!=='boolean' || !color(item.outline.color) || !Number.isFinite(item.outline.width) || item.outline.width<0 || item.outline.width>32767)) throw new Error('ขอบอักษรในไฟล์งานไม่ถูกต้อง');
    if(['bubbleFill','bubbleBorder'].some(k=>k in item && !color(item[k]))) throw new Error('สีในไฟล์งานไม่ถูกต้อง');
    if(['bubbleWidth','bubbleHeight','bubbleBorderWidth'].some(k=>k in item && (!Number.isFinite(item[k])||item[k]<0||item[k]>32767))) throw new Error('ขนาดวัตถุในไฟล์งานไม่ถูกต้อง');
    if(item.tail && !['x','y'].every(k=>Number.isFinite(item.tail[k]))) throw new Error('ตำแหน่งลูกศรในไฟล์งานไม่ถูกต้อง');
    if(item.curve && (!['line','arrow'].includes(item.tool) || !['x','y'].every(k=>Number.isFinite(item.curve[k])))) throw new Error('จุดดัดเส้นในไฟล์งานไม่ถูกต้อง');
    if(item.clipRect && (!['x','y','width','height'].every(k=>Number.isFinite(item.clipRect[k]))||item.clipRect.width<1||item.clipRect.height<1)) throw new Error('กรอบวัตถุในไฟล์งานไม่ถูกต้อง');
    if(!tools.has(item.tool) || !/^#[0-9a-f]{6}$/i.test(item.color) || !Number.isFinite(item.size) || item.size<0 || item.size>32767 || !Number.isFinite(item.opacity) || item.opacity<0 || item.opacity>1) throw new Error('วัตถุในไฟล์งานไม่ถูกต้อง');
    if(['pen','highlight'].includes(item.tool)) {
      if(!Array.isArray(item.points)||!item.points.length||item.points.length>100000||!item.points.every(p=>Number.isFinite(p.x)&&Number.isFinite(p.y))) throw new Error('เส้นในไฟล์งานไม่ถูกต้อง');
    } else if(!Number.isFinite(item.x1)||!Number.isFinite(item.y1)) throw new Error('ตำแหน่งวัตถุไม่ถูกต้อง');
    if(['line','arrow','box','block','ellipse','ellipseFill','blur','image'].includes(item.tool) && (!Number.isFinite(item.x2)||!Number.isFinite(item.y2))) throw new Error('ขนาดวัตถุไม่ถูกต้อง');
    if(item.tool==='number' && (!Number.isInteger(item.number)||item.number<1)) throw new Error('เลขลำดับไม่ถูกต้อง');
    if(['text','bubble'].includes(item.tool) && typeof item.text!=='string') throw new Error('ข้อความในไฟล์งานไม่ถูกต้อง');
    if(item.tool==='blur' && (!Number.isFinite(item.blur)||item.blur<1||item.blur>64)) throw new Error('ความเบลอไม่ถูกต้อง');
    if(item.tool==='image') {
      if(typeof item.id!=='string'||images.has(item.id)||!assets.has(item.assetId)||item.x2<=item.x1||item.y2<=item.y1) throw new Error('ชิ้นภาพในไฟล์งานไม่ถูกต้อง');
      const source=assets.get(item.assetId).image;rect(item.sourceCrop,source.width,source.height);images.set(item.id,item);
    }
  }
  for(const item of project.objects) if(item.owner && !images.has(item.owner)) throw new Error('ไม่พบภาพของคำอธิบาย');
  return {project:structuredClone(project),assets};
}

function applyProject(decoded, kept=false) {
  commitPendingEdit();
  projectAssets.clear(); decoded.assets.forEach((v,k)=>projectAssets.set(k,v));
  restoreProjectState({...decoded.project,objects:decoded.project.objects});
  selected=-1;undoStack.length=0;redoStack.length=0;cropDraft=null;
  canvas.width=cropRect.width;canvas.height=cropRect.height;
  keptSnapshot=kept?imageSnapshot():null;
  document.querySelector('#loading').hidden=true;updateImageInfo();setTool('select');fitCanvas();
}
async function loadProject(project,kept=false) { applyProject(await decodeProject(project),kept); }

function imageLayer(assetId,id,x,y,width,height,sourceCrop) {
  return {tool:'image',assetId,id,x1:x,y1:y,x2:x+width,y2:y+height,sourceCrop,color:'#ffffff',size:5,opacity:1};
}
function translated(item,dx,dy) {
  const copy=structuredClone(item);
  if(copy.points) copy.points=copy.points.map(p=>({x:p.x+dx,y:p.y+dy}));
  else {copy.x1+=dx;copy.y1+=dy;if('x2' in copy){copy.x2+=dx;copy.y2+=dy;}}
  if(copy.clipRect){copy.clipRect.x+=dx;copy.clipRect.y+=dy;}
  if(copy.curve){copy.curve.x+=dx;copy.curve.y+=dy;}
  return copy;
}

async function combineProjects(projects,layout='vertical',gap=16,append=false) {
  if(!['vertical','horizontal','free'].includes(layout)||!Number.isInteger(gap)||gap<0||gap>500||!Array.isArray(projects)||!projects.length||projects.length>100) throw new Error('การจัดภาพไม่ถูกต้อง');
  const decoded=[];
  if(JSON.stringify(projects).length>projectLimits.bytes) throw new Error('งานรวมภาพใหญ่เกินไป');
  const cache=new Map([...projectAssets.values()].filter(a=>a.dataUrl).map(a=>[a.dataUrl,a]));
  // Validate every input before touching the current editor.
  for(const p of projects) decoded.push(await decodeProject(p,cache));
  if(append) decoded.unshift(await decodeProject(exportProject().project,cache));
  const assets=new Map(),items=[];let cursor=0,width=0,height=0,pixels=0;
  decoded.forEach(({project:p,assets:sourceAssets},index)=>{
    const prefix=crypto.randomUUID(), remap=new Map();
    sourceAssets.forEach((asset,id)=>{pixels+=asset.image.width*asset.image.height;const key=prefix+id;assets.set(key,asset);remap.set(id,key);});
    const x=layout==='horizontal'?cursor:layout==='free'?index*24:0;
    const y=layout==='vertical'?cursor:layout==='free'?index*24:0;
    const dx=x-p.cropRect.x,dy=y-p.cropRect.y;
    let owner=null;
    if(p.background) {
      owner=prefix+'image';items.push(imageLayer(remap.get(p.background),owner,x,y,p.cropRect.width,p.cropRect.height,p.cropRect));
    }
    for(const old of p.objects) {
      const item=translated(old,dx,dy);
      if(item.tool==='image') {
        item.id=prefix+old.id;item.assetId=remap.get(old.assetId);
        const left=Math.max(item.x1,x),top=Math.max(item.y1,y),right=Math.min(item.x2,x+p.cropRect.width),bottom=Math.min(item.y2,y+p.cropRect.height);
        if(right<=left||bottom<=top) continue;
        const sx=item.sourceCrop.width/(item.x2-item.x1),sy=item.sourceCrop.height/(item.y2-item.y1);
        item.sourceCrop={x:item.sourceCrop.x+(left-item.x1)*sx,y:item.sourceCrop.y+(top-item.y1)*sy,width:(right-left)*sx,height:(bottom-top)*sy};
        item.x1=left;item.y1=top;item.x2=right;item.y2=bottom;
      } else if(owner) item.owner=owner;
      else if(item.owner) item.owner=prefix+item.owner;
      else item.clipRect={x,y,width:p.cropRect.width,height:p.cropRect.height};
      if(item.owner) item.childId=crypto.randomUUID();
      items.push(item);
    }
    width=Math.max(width,x+p.cropRect.width);height=Math.max(height,y+p.cropRect.height);
    cursor+=(layout==='horizontal'?p.cropRect.width:p.cropRect.height)+gap;
  });
  const owners=new Set(items.filter(o=>o.tool==='image').map(o=>o.id));
  const visible=items.filter(o=>!o.owner||owners.has(o.owner));
  // Reuse identical source assets across imports and undo history.
  const canonical=new Map(append?[...projectAssets].filter(([,a])=>a.dataUrl).map(([id,a])=>[a.dataUrl,id]):[]);
  const replacements=new Map();
  for(const [id,asset] of assets) {
    if(canonical.has(asset.dataUrl)) replacements.set(id,canonical.get(asset.dataUrl));
    else canonical.set(asset.dataUrl,id);
  }
  for(const item of visible) if(item.tool==='image' && replacements.has(item.assetId)) item.assetId=replacements.get(item.assetId);
  for(const id of replacements.keys()) assets.delete(id);
  const retained=new Set([...(append?projectAssets.values():[]),...assets.values()].map(a=>a.image));
  pixels=[...retained].reduce((n,img)=>n+img.width*img.height,0);
  projectDimensions(width,height);
  if(pixels>projectLimits.pixels||visible.length>projectLimits.objects||new Set(visible.filter(o=>o.tool==='image').map(o=>o.assetId)).size>100) throw new Error('งานรวมภาพใหญ่เกินไป กรุณาแบ่งเป็นหลายงาน');
  if(append) {
    checkpoint();assets.forEach((v,k)=>projectAssets.set(k,v));
    restoreProjectState({objects:visible,cropRect:{x:0,y:0,width,height},background:null,width,height});
    selected=-1;canvas.width=width;canvas.height=height;updateImageInfo();setTool('select');fitCanvas();
  } else applyProject({assets,project:{objects:visible,cropRect:{x:0,y:0,width,height},background:null,width,height}});
}

async function addImages(urls) {
  if(!Array.isArray(urls)||!urls.length||urls.length>100) throw new Error('เลือกภาพไม่เกิน 100 ภาพ');
  const projects=[];
  for(const url of urls) {
    const source=await loadImage(url);projectDimensions(source.width,source.height);
    const dataUrl=imageDataUrl(source);
    projects.push({format:'neo-snap',version:1,width:source.width,height:source.height,background:'base',cropRect:{x:0,y:0,width:source.width,height:source.height},assets:[{id:'base',dataUrl}],objects:[]});
  }
  await combineProjects(projects,'vertical',16,true);
}

function transformImageChildren(item,original,dx,dy,scale=1) {
  const originals=gesture?.children || prepareImageChildren(item);
  for(const old of originals) {
    const index=objects.findIndex(o=>o.owner===item.id && o.childId===old.childId);
    if(index<0) continue;
    const next=translated(old,dx,dy);
    const transform=p=>({x:item.x1+(p.x-original.x1)*scale,y:item.y1+(p.y-original.y1)*scale});
    if(scale!==1) {
      if(old.curve) next.curve=transform(old.curve);
      if(old.points) next.points=old.points.map(transform);
      else {const start=transform({x:old.x1,y:old.y1});next.x1=start.x;next.y1=start.y;if('x2' in old){const end=transform({x:old.x2,y:old.y2});next.x2=end.x;next.y2=end.y;}}
      next.size*=scale;
      if(next.tail){next.tail.x*=scale;next.tail.y*=scale;}
      for(const k of ['bubbleWidth','bubbleHeight','bubbleBorderWidth']) if(Number.isFinite(next[k])) next[k]*=scale;
      if(Number.isFinite(next.blur)) next.blur=Math.min(64,next.blur*scale);
      if(next.shadow){next.shadow.blur=Math.min(128,next.shadow.blur*scale);next.shadow.x=Math.max(-128,Math.min(128,next.shadow.x*scale));next.shadow.y=Math.max(-128,Math.min(128,next.shadow.y*scale));}
      if(next.outline) next.outline.width*=scale;
    }
    objects[index]=next;
  }
}

function prepareImageChildren(item) {
  const children=objects.filter(o=>o.owner===item.id);
  children.forEach(o=>o.childId ||= crypto.randomUUID());
  return structuredClone(children);
}

function cropSelectedImage(draft,index) {
  const item=objects[index],box=bounds(item);
  const x=Math.max(box.x,Math.round(draft.x)),y=Math.max(box.y,Math.round(draft.y));
  const right=Math.min(box.x+box.width,Math.round(draft.x+draft.width)),bottom=Math.min(box.y+box.height,Math.round(draft.y+draft.height));
  if(right-x<20||bottom-y<20) throw new Error('กรอบครอบตัดเล็กเกินไป');
  const sx=item.sourceCrop.width/box.width,sy=item.sourceCrop.height/box.height;
  checkpoint();item.sourceCrop={x:item.sourceCrop.x+(x-box.x)*sx,y:item.sourceCrop.y+(y-box.y)*sy,width:(right-x)*sx,height:(bottom-y)*sy};
  item.x1=x;item.y1=y;item.x2=right;item.y2=bottom;
}

function resizeWorkspace(width,height) {
  projectDimensions(width,height);commitPendingEdit();checkpoint();
  if(projectBackground) {
    const id=crypto.randomUUID(),oldCrop=structuredClone(cropRect);
    const image=imageLayer(projectBackground,id,0,0,oldCrop.width,oldCrop.height,oldCrop);
    objects=[image,...objects.map(o=>({...translated(o,-oldCrop.x,-oldCrop.y),owner:id,childId:crypto.randomUUID()}))];
  }
  projectBackground=null;projectWidth=width;projectHeight=height;
  cropRect={x:0,y:0,width,height};baseImage=projectSurface(width,height);
  canvas.width=width;canvas.height=height;selected=-1;cropDraft=null;
  updateImageInfo();setTool('select');fitCanvas();
}
