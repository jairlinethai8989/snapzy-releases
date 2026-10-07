function curvePoint(item, t = .5) {
  const c = item.curve || { x: (item.x1 + item.x2) / 2, y: (item.y1 + item.y2) / 2 }, u = 1 - t;
  return { x: u*u*item.x1 + 2*u*t*c.x + t*t*item.x2, y: u*u*item.y1 + 2*u*t*c.y + t*t*item.y2 };
}

function curveBounds(item) {
  const points = [curvePoint(item, 0), curvePoint(item, 1)];
  for (const [a,b,c] of [[item.x1,item.curve.x,item.x2],[item.y1,item.curve.y,item.y2]]) {
    const t = (a-b)/(a-2*b+c);
    if(t>0 && t<1) points.push(curvePoint(item,t));
  }
  const xs=points.map(p=>p.x),ys=points.map(p=>p.y);
  return {x:Math.min(...xs),y:Math.min(...ys),width:Math.max(1,Math.max(...xs)-Math.min(...xs)),height:Math.max(1,Math.max(...ys)-Math.min(...ys))};
}

function strokePath(ctx,item) {
  ctx.beginPath();ctx.moveTo(item.x1,item.y1);
  if(item.curve) ctx.quadraticCurveTo(item.curve.x,item.curve.y,item.x2,item.y2);
  else ctx.lineTo(item.x2,item.y2);
  ctx.stroke();
}

function curveHit(point,item,tolerance) {
  if(!item.curve) return segmentDistance(point,{x:item.x1,y:item.y1},{x:item.x2,y:item.y2})<=tolerance;
  const length=Math.hypot(item.curve.x-item.x1,item.curve.y-item.y1)+Math.hypot(item.x2-item.curve.x,item.y2-item.curve.y);
  const steps=Math.max(16,Math.min(2048,Math.ceil(length/8)));
  let a=curvePoint(item,0);
  for(let n=1;n<=steps;n++) {const b=curvePoint(item,n/steps);if(segmentDistance(point,a,b)<=tolerance) return true;a=b;}
  return false;
}

function moveCurve(item,point) {
  const x=Math.max(cropRect.x,Math.min(cropRect.x+cropRect.width,point.x));
  const y=Math.max(cropRect.y,Math.min(cropRect.y+cropRect.height,point.y));
  // The handle stays on the curve midpoint, not at the off-path control point.
  item.curve={x:2*x-(item.x1+item.x2)/2,y:2*y-(item.y1+item.y2)/2};
}

function updateCurveHandle() {
  const handle=document.querySelector('#curveHandle'),item=objects[selected];
  handle.hidden=!baseImage || !['line','arrow'].includes(item?.tool) || Boolean(textEditor) || activeTool==='crop';
  if(handle.hidden) return;
  const p=curvePoint(item),image=canvas.getBoundingClientRect(),v=stage.getBoundingClientRect();
  const x=image.left+(p.x-cropRect.x)*image.width/canvas.width,y=image.top+(p.y-cropRect.y)*image.height/canvas.height;
  handle.hidden=x<v.left || x>v.left+stage.clientWidth || y<v.top || y>v.top+stage.clientHeight;
  handle.style.left=`${x}px`;handle.style.top=`${y}px`;
}

document.querySelector('#curveHandle').addEventListener('pointerdown',event=>{
  if(event.button!==0 || !['line','arrow'].includes(objects[selected]?.tool) || textEditor) return;
  event.preventDefault();endGesture();checkpoint();
  gesture={mode:'curve',start:canvasPoint(event),original:structuredClone(objects[selected])};
  canvas.setPointerCapture(event.pointerId);capturedPointer=event.pointerId;
});
document.querySelector('#curveHandle').addEventListener('keydown',event=>{
  const d={ArrowLeft:[-1,0],ArrowRight:[1,0],ArrowUp:[0,-1],ArrowDown:[0,1]}[event.key],item=objects[selected];
  if(!d || !['line','arrow'].includes(item?.tool)) return;
  event.preventDefault();event.stopPropagation();checkpoint();
  const p=curvePoint(item),step=event.shiftKey?10:1;moveCurve(item,{x:p.x+d[0]*step,y:p.y+d[1]*step});render();
});
