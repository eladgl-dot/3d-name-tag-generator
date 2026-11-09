// Keeps uploaded fonts and the picture in this browser. IndexedDB, because these files are too big for localStorage.
const DATABASE = 'nametag-fonts';
const VERSION = 2;
const FONTS = 'fonts';
const PICTURES = 'pictures';
const PICTURE_KEY = 'current';
const OPEN_WAIT_MS = 5000;

function open() {
  return new Promise((resolve, reject) => {
    // The page waits for this before it starts, and storage that never answers must not leave it on Loading.
    const giveUp = setTimeout(() => reject(new Error('Storage did not answer')), OPEN_WAIT_MS);
    const request = indexedDB.open(DATABASE, VERSION);
    request.onupgradeneeded = () => {
      for (const name of [FONTS, PICTURES])
        if (!request.result.objectStoreNames.contains(name)) request.result.createObjectStore(name);
    };
    request.onsuccess = () => { clearTimeout(giveUp); resolve(request.result); };
    request.onerror = () => { clearTimeout(giveUp); reject(request.error); };
  });
}

async function withStore(storeName, mode, work) {
  const database = await open();
  try {
    return await new Promise((resolve, reject) => {
      const transaction = database.transaction(storeName, mode);
      const request = work(transaction.objectStore(storeName));
      transaction.oncomplete = () => resolve(request.result);
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    });
  } finally {
    database.close();
  }
}

export const names = () => withStore(FONTS, 'readonly', store => store.getAllKeys());

export const get = name => withStore(FONTS, 'readonly', store => store.get(name));

// slice() stores just this file, not the whole block of memory the bytes arrived in.
export const put = (name, bytes) => withStore(FONTS, 'readwrite', store => store.put(bytes.slice(), name));

export const getPicture = () => withStore(PICTURES, 'readonly', store => store.get(PICTURE_KEY));

export const putPicture = bytes => withStore(PICTURES, 'readwrite', store => store.put(bytes.slice(), PICTURE_KEY));

export const removePicture = () => withStore(PICTURES, 'readwrite', store => store.delete(PICTURE_KEY));
