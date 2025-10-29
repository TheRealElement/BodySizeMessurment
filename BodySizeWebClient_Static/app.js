// basic Three.js scene + simple measurement rings
let scene, camera, renderer, controls;
let mannequin = null;
const rings = [];

const storedUrl = localStorage.getItem('serverUrl');
const serverUrlInput = document.getElementById('serverUrl');
if (storedUrl) serverUrlInput.value = storedUrl;

function init3D() {
  const w = document.getElementById('viewer').clientWidth;
  const h = document.getElementById('viewer').clientHeight;

  scene = new THREE.Scene();
  camera = new THREE.PerspectiveCamera(45, w/h, 0.1, 2000);
  camera.position.set(0, 180, 380);
  scene.add(new THREE.AmbientLight(0xffffff, 0.7));
  const dir = new THREE.DirectionalLight(0xffffff, 0.8);
  dir.position.set(200,300,200);
  scene.add(dir);

  renderer = new THREE.WebGLRenderer({antialias:true});
  renderer.setSize(w,h);
  document.getElementById('viewer').appendChild(renderer.domElement);

  // ground
  const g = new THREE.CircleGeometry(300, 64);
  const m = new THREE.MeshBasicMaterial({color:0xf1f1f1});
  const ground = new THREE.Mesh(g, m);
  ground.rotation.x = -Math.PI/2;
  ground.position.y = -2;
  scene.add(ground);

  animate();
  window.addEventListener('resize', onResize);
  setupManipulation();
  createDefaultMannequin();
  createRings();
  tryAutoLoad();
}

function onResize() {
  const w = document.getElementById('viewer').clientWidth;
  const h = document.getElementById('viewer').clientHeight;
  camera.aspect = w/h;
  camera.updateProjectionMatrix();
  renderer.setSize(w,h);
}

function animate() {
  requestAnimationFrame(animate);
  renderer.render(scene, camera);
}

function setupManipulation() {
  let dragging = false, lastX=0,lastY=0;
  const el = renderer.domElement;
  el.addEventListener('mousedown', e=>{dragging=true; lastX=e.clientX; lastY=e.clientY;});
  window.addEventListener('mouseup', ()=>dragging=false);
  el.addEventListener('mousemove', e=>{
    if(!dragging) return;
    const dx = (e.clientX-lastX)*0.01, dy=(e.clientY-lastY)*0.01;
    scene.rotation.y += dx;
    camera.position.y = Math.max(40, Math.min(400, camera.position.y - dy*50));
    lastX=e.clientX; lastY=e.clientY;
  });
  el.addEventListener('wheel', e=>{
    camera.position.z = Math.max(80, Math.min(800, camera.position.z + e.deltaY*0.2));
  }, {passive:true});
  document.getElementById('resetView').onclick = ()=>{
    scene.rotation.set(0,0,0);
    camera.position.set(0,180,380);
  };
}

function createDefaultMannequin() {
  // simple capsule-like body so viewer is never empty
  const group = new THREE.Group();
  const bodyM = new THREE.MeshPhongMaterial({color:0xdddddd, shininess:30});
  const limbM = new THREE.MeshPhongMaterial({color:0xcfcfcf});

  const chest = new THREE.Mesh(new THREE.CylinderGeometry(18,18,40,24), bodyM);
  chest.position.y = 80; group.add(chest);

  const belly = new THREE.Mesh(new THREE.CylinderGeometry(16,16,30,24), bodyM);
  belly.position.y = 50; group.add(belly);

  const head = new THREE.Mesh(new THREE.SphereGeometry(13, 24, 16), new THREE.MeshPhongMaterial({color:0xeeeeee, envMap:null}));
  head.position.y = 130; group.add(head);

  const legL = new THREE.Mesh(new THREE.CylinderGeometry(8,8,50,16), limbM);
  legL.position.set(0,25,0); group.add(legL);
  const legR = legL.clone(); legR.position.x = 16; group.add(legR);

  mannequin = group;
  scene.add(mannequin);
}

function createRings() {
  const ringY = [130, 96, 66, 46]; // shoulder, chest, waist, hips-ish
  const material = new THREE.MeshBasicMaterial({color:0x009B55});
  ringY.forEach((y,idx)=>{
    const torus = new THREE.Mesh(new THREE.TorusGeometry(20, 0.8, 8, 64), material);
    torus.rotation.x = Math.PI/2;
    torus.position.y = y;
    rings.push(torus);
    scene.add(torus);
  });
}

function setRingCircumferences(cm) {
  // cm = {shoulders, chest, waist, hips}
  const map = [cm.shoulders, cm.chest, cm.waist, cm.hips];
  for (let i=0;i<rings.length;i++) {
    const r = map[i] / (2*Math.PI); // radius in "cm units"
    rings[i].geometry.dispose();
    rings[i].geometry = new THREE.TorusGeometry(r, 0.8, 8, 64);
  }
}

function tryAutoLoad() {
  fetch('assets/mannequin.glb', {method:'HEAD'}).then(r=>{
    if (r.ok) loadGlb('assets/mannequin.glb');
  }).catch(()=>{});
}

function loadGlb(path) {
  // Lightweight GLTF loader via three/examples is not on CDN here; use basic OBJ-like fallback:
  // For simplicity, we won't parse GLB here—this keeps the demo fully static.
  alert('GLB loading in this barebones static demo is not built-in. Use the default mannequin or host a GLTFLoader build. See README.');
}

async function analyze() {
  const status = document.getElementById('status');
  const server = document.getElementById('serverUrl').value.replace(/\/$/, '');
  localStorage.setItem('serverUrl', server);
  status.textContent = 'Sending…';

  const files = {
    front: document.getElementById('front').files[0],
    back: document.getElementById('back').files[0],
    left: document.getElementById('left').files[0],
    right: document.getElementById('right').files[0],
  };
  for (const k of Object.keys(files)) if (!files[k]) { status.textContent = 'Select all four photos'; return; }

  const form = new FormData();
  form.append('heightCm', document.getElementById('height').value);
  form.append('gender', document.getElementById('gender').value);
  for (const [k,f] of Object.entries(files)) form.append(k, f);

  try {
    const resp = await fetch(server + '/api/Analyze/analyze', { method:'POST', body: form });
    if (!resp.ok) throw new Error('HTTP ' + resp.status);
    const data = await resp.json();

    // Expecting: { chestCm, waistCm, hipsCm, shoulderWidthCm, torsoLengthCm, topSizeEU, bottomSizeEU }
    const out = {
      chest: data.chestCm, waist: data.waistCm, hips: data.hipsCm,
      shoulders: data.shoulderWidthCm
    };
    setRingCircumferences(out);
    document.getElementById('resultsText').textContent = JSON.stringify(data, null, 2);
    status.textContent = 'Done';
  } catch (e) {
    console.error(e);
    status.textContent = 'Failed: ' + e.message + '. Check server URL & CORS.';
  }
}

document.getElementById('analyze').onclick = analyze;
document.getElementById('saveUrl').onclick = ()=>{
  localStorage.setItem('serverUrl', document.getElementById('serverUrl').value);
  document.getElementById('saveInfo').textContent = 'Saved';
  setTimeout(()=>document.getElementById('saveInfo').textContent='', 1500);
};
document.getElementById('loadGlb').onclick = ()=>{
  const inp = document.createElement('input');
  inp.type='file'; inp.accept='.glb,.gltf';
  inp.onchange = ()=>{
    if (inp.files[0]) loadGlb(URL.createObjectURL(inp.files[0]));
  };
  inp.click();
};

['front','back','left','right'].forEach(id=>{
  const inp=document.getElementById(id);
  const img=document.getElementById(id+'Preview');
  inp.addEventListener('change', ()=>{
    if (inp.files && inp.files[0]) img.src = URL.createObjectURL(inp.files[0]);
  });
});

init3D();
