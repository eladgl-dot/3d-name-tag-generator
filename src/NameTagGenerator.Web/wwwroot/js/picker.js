// Small helpers for the font list and the emoji picker.

/** Up, Down, Home and End move through a list. When it opens, the chosen row is in view and has the focus. */
export function arrowKeys(panel) {
  const rows = () => [...panel.querySelectorAll('button')];
  panel.addEventListener('toggle', e => {
    if (e.newState !== 'open') return;
    const row = panel.querySelector('button.on') ?? rows()[0];
    row?.focus();
    row?.scrollIntoView({ block: 'nearest' });
  });
  panel.addEventListener('keydown', e => {
    const all = rows();
    const at = all.indexOf(document.activeElement);
    const next = { ArrowDown: Math.min(at + 1, all.length - 1), ArrowUp: Math.max(at - 1, 0), Home: 0, End: all.length - 1 }[e.key];
    if (next === undefined) return;
    e.preventDefault();
    all[next].focus();
  });
}

/**
 * Decodes an image file to grey, at most maxSide pixels on its longest side. Transparent parts read as white paper.
 * Returns the width and height (two bytes each, low byte first), then one byte per pixel.
 */
export async function grayImage(bytes, maxSide) {
  const bitmap = await createImageBitmap(new Blob([bytes]));
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
  const width = Math.max(1, Math.round(bitmap.width * scale));
  const height = Math.max(1, Math.round(bitmap.height * scale));
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d', { willReadFrequently: true });
  context.fillStyle = '#fff';
  context.fillRect(0, 0, width, height);
  context.drawImage(bitmap, 0, 0, width, height);
  bitmap.close();
  const pixels = context.getImageData(0, 0, width, height).data;
  const result = new Uint8Array(width * height + 4);
  result.set([width & 255, width >> 8, height & 255, height >> 8]);
  for (let i = 0; i < width * height; i++)
    result[i + 4] = Math.round(0.299 * pixels[i * 4] + 0.587 * pixels[i * 4 + 1] + 0.114 * pixels[i * 4 + 2]);
  return result;
}

/** Puts text where the cursor is in a text box and returns the box's new contents. */
export function insertAtCaret(box, text) {
  box.setRangeText(text, box.selectionStart, box.selectionEnd, 'end');
  box.focus();
  return box.value;
}
