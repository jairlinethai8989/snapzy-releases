function orderedLayers(action) {
  const item = objects[selected];
  if (!item) return objects;
  const group = item.tool === 'image' ? objects.filter(o => o === item || o.owner === item.id) : [item];
  const members = new Set(group);
  const rest = objects.filter(o => !members.has(o));
  let insertion;
  if (action === 'front') insertion = rest.length;
  else if (action === 'back') insertion = 0;
  else {
    const indices = [];
    objects.forEach((o, index) => { if (members.has(o)) indices.push(index); });
    const edge = action === 'up' ? Math.max(...indices) : Math.min(...indices);
    const neighbor = objects[edge + (action === 'up' ? 1 : -1)];
    if (!neighbor) return objects;
    // Image layers retain their editable annotations when crossing another image group.
    const parent = item.tool === 'image' && objects.find(o => o.tool === 'image' && (o === neighbor || o.id === neighbor.owner));
    const adjacent = parent ? rest.filter(o => o === parent || o.owner === parent.id) : [neighbor];
    const adjacentMembers = new Set(adjacent), positions = [];
    rest.forEach((o, index) => { if (adjacentMembers.has(o)) positions.push(index); });
    insertion = action === 'up' ? Math.max(...positions) + 1 : Math.min(...positions);
  }
  return [...rest.slice(0, insertion), ...group, ...rest.slice(insertion)];
}

function layerOrderChanges(next) { return next.some((item, index) => item !== objects[index]); }

function syncLayerControls() {
  for (const [id, action] of Object.entries({ layerFrontButton: 'front', layerUpButton: 'up', layerDownButton: 'down', layerBackButton: 'back' })) {
    document.getElementById(id).disabled = !objects[selected] || !!cropDraft || !layerOrderChanges(orderedLayers(action));
  }
}

function reorderSelectedLayer(action) {
  if (!['front', 'back', 'up', 'down'].includes(action) || cropDraft) return;
  commitPendingEdit();
  const item = objects[selected], next = orderedLayers(action);
  if (!item || !layerOrderChanges(next)) return;
  checkpoint();
  objects = next;
  selected = objects.indexOf(item);
  syncControls();
  render();
}

for (const [id, action] of Object.entries({ layerFrontButton: 'front', layerUpButton: 'up', layerDownButton: 'down', layerBackButton: 'back' })) {
  document.getElementById(id).onclick = () => reorderSelectedLayer(action);
}
syncLayerControls();
