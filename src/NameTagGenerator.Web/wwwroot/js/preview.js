// The 3D view. Draws only when something changes, so an idle page uses no graphics power.
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';

const VIEW_DIRECTION = new THREE.Vector3(0, -0.55, 1).normalize();
const PLATE_COLOUR = 0x4a4f58;
const PLATE_Z = -0.2;

let renderer, scene, camera, controls, baseMesh, letterMesh, plateMesh;
let drawQueued = false;

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
  plateMesh = new THREE.Mesh(new THREE.PlaneGeometry(1, 1), new THREE.MeshStandardMaterial({ color: PLATE_COLOUR, roughness: 0.9 }));
  plateMesh.position.z = PLATE_Z;
  plateMesh.visible = false;
  scene.add(plateMesh);

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
  if (plateMesh.visible) box.union(new THREE.Box3().setFromObject(plateMesh));
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

/** Replaces what is shown. A plate size of 0 hides the plate. */
export function show(basePositions, baseIndices, letterPositions, letterIndices, plateWidth, plateDepth, baseColour, letterColour, refit) {
  setGeometry(baseMesh, basePositions, baseIndices);
  setGeometry(letterMesh, letterPositions, letterIndices);
  plateMesh.visible = plateWidth > 0;
  plateMesh.scale.set(plateWidth || 1, plateDepth || 1, 1);
  plateMesh.updateMatrixWorld();
  setColours(baseColour, letterColour);
  if (refit) fitView();
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

/** Hands a finished file to the browser as a download. */
export function download(fileName, bytes, contentType) {
  const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 10000);
}
