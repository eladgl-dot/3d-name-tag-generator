// The 3D view. Draws only when something changes, so an idle page uses no graphics power.
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';

const VIEW_DIRECTION = new THREE.Vector3(0, -0.55, 1).normalize();
const PLATE_COLOUR = 0x4a4f58;
const PLATE_Z = -0.2;

let renderer, scene, camera, controls, baseMesh, letterMesh, plates;
let drawQueued = false;
// What was last fitted into view. The view refits when the model grows a lot or the number of beds changes,
// so it never cuts the model off, but zooming in by hand survives typing.
let fittedSize = null, fittedBeds = 0;

// Esc closes the help from anywhere on the page, unless it is closing the Arrange menu.
document.addEventListener('keydown', e => {
  if (e.key === 'Escape' && !document.querySelector('[popover]:popover-open')) document.querySelector('.help-close')?.click();
});

function draw() {
  if (drawQueued) return;
  drawQueued = true;
  requestAnimationFrame(() => {
    drawQueued = false;
    renderer.render(scene, camera);
  });
}

function solid(colour) {
  const mesh = new THREE.Mesh(new THREE.BufferGeometry(), new THREE.MeshStandardMaterial({ color: colour, roughness: 0.7, flatShading: true }));
  scene.add(mesh);
  return mesh;
}

/** Starts the view inside host. Returns false when this browser cannot draw 3D. */
export function init(host) {
  try {
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
  } catch {
    return false;
  }
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  host.appendChild(renderer.domElement);

  scene = new THREE.Scene();
  scene.add(new THREE.HemisphereLight(0xffffff, 0x8a8f99, 1.8));
  const sun = new THREE.DirectionalLight(0xffffff, 1.6);
  sun.position.set(-0.5, -1, 2);
  scene.add(sun);

  camera = new THREE.PerspectiveCamera(30, 1, 1, 10000);
  camera.up.set(0, 0, 1);
  controls = new OrbitControls(camera, renderer.domElement);
  controls.addEventListener('change', draw);

  baseMesh = solid(0xffffff);
  letterMesh = solid(0xffffff);
  plates = new THREE.Group();
  scene.add(plates);

  new ResizeObserver(() => {
    const { clientWidth: w, clientHeight: h } = host;
    if (w === 0 || h === 0) return;
    renderer.setSize(w, h);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    draw();
  }).observe(host);
  return true;
}

function setGeometry(mesh, positions, indices) {
  mesh.geometry.dispose();
  const geometry = new THREE.BufferGeometry();
  // slice() gives each array its own buffer, starting at a position floats can be read from.
  geometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(positions.slice().buffer), 3));
  geometry.setIndex(new THREE.BufferAttribute(new Uint32Array(indices.slice().buffer), 1));
  mesh.geometry = geometry;
}

function fitView() {
  const box = new THREE.Box3().setFromObject(baseMesh).union(new THREE.Box3().setFromObject(letterMesh));
  if (plates.children.length > 0) box.union(new THREE.Box3().setFromObject(plates));
  if (box.isEmpty()) return;

  const size = box.getSize(new THREE.Vector3());
  const middle = box.getCenter(new THREE.Vector3());
  const tan = Math.tan(THREE.MathUtils.degToRad(camera.fov / 2));
  const distance = 1.2 * Math.max(Math.max(size.y, size.z) / 2 / tan, size.x / 2 / (tan * camera.aspect)) + size.z / 2;

  controls.target.copy(middle);
  camera.position.copy(middle).addScaledVector(VIEW_DIRECTION, distance);
  camera.near = Math.max(distance / 100, 0.1);
  camera.far = distance * 20;
  camera.updateProjectionMatrix();
  controls.update();
}

/** A flat "Bed 2" label lying on the bed, behind its back edge. */
function bedLabel(text, x, y, width) {
  const canvas = document.createElement('canvas');
  canvas.width = 512;
  canvas.height = 128;
  const g = canvas.getContext('2d');
  g.fillStyle = '#4a4f58';
  g.font = '600 72px "Segoe UI", system-ui, sans-serif';
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.fillText(text, 256, 64);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const label = new THREE.Mesh(new THREE.PlaneGeometry(width, width / 4), new THREE.MeshBasicMaterial({ map: texture, transparent: true }));
  label.position.set(x, y + width / 8, PLATE_Z);
  return label;
}

/** Draws count beds of width × depth mm, the first centred on the origin, the next ones step mm to the right each. */
function setPlates(width, depth, count, step) {
  for (const child of [...plates.children]) {
    plates.remove(child);
    child.geometry.dispose();
    child.material.map?.dispose();
    child.material.dispose();
  }
  if (!(width > 0)) return;
  for (let i = 0; i < count; i++) {
    const plate = new THREE.Mesh(new THREE.PlaneGeometry(width, depth), new THREE.MeshStandardMaterial({ color: PLATE_COLOUR, roughness: 0.9 }));
    plate.position.set(i * step, 0, PLATE_Z);
    plates.add(plate);
    if (count > 1) plates.add(bedLabel(`Bed ${i + 1}`, i * step, depth / 2 + 4, Math.min(width * 0.4, 100)));
  }
}

/** Replaces what is shown. A plate size of 0 hides the plates. */
export function show(basePositions, baseIndices, letterPositions, letterIndices, plateWidth, plateDepth, plateCount, plateStep, baseColour, letterColour, refit) {
  setGeometry(baseMesh, basePositions, baseIndices);
  setGeometry(letterMesh, letterPositions, letterIndices);
  setPlates(plateWidth, plateDepth, plateCount, plateStep);
  setColours(baseColour, letterColour);
  const box = new THREE.Box3().setFromObject(baseMesh);
  if (plates.children.length > 0) box.union(new THREE.Box3().setFromObject(plates));
  const size = box.isEmpty() ? null : box.getSize(new THREE.Vector3());
  const beds = plateWidth > 0 ? plateCount : 0;
  const grew = size && (!fittedSize || size.x > 1.1 * fittedSize.x || size.y > 1.1 * fittedSize.y);
  if (refit || grew || beds !== fittedBeds) {
    fitView();
    fittedSize = size;
    fittedBeds = beds;
  }
  draw();
}

export function setColours(baseColour, letterColour) {
  baseMesh.material.color.set(baseColour);
  letterMesh.material.color.set(letterColour);
  draw();
}

/** Which line of a text box the caret is on, counting from 0. */
export function caretLine(textBox) {
  return textBox.value.slice(0, textBox.selectionStart).split('\n').length - 1;
}

/** Puts a list back on its current choice and opens a file picker. Clearing the picker lets the same file be chosen again. */
/** Hands a finished file to the browser as a download. */
export function download(fileName, bytes, contentType) {
  const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 10000);
}
