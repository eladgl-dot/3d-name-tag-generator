// Keeps uploaded font files in this browser. IndexedDB, because font files are too big for localStorage.
const DATABASE = 'nametag-fonts';
const STORE = 'fonts';

const OPEN_WAIT_MS = 5000;

function open() {
  return new Promise((resolve, reject) => {
    // The page waits for this before it starts, and storage that never answers must not leave it on Loading.
    const giveUp = setTimeout(() => reject(new Error('Font storage did not answer')), OPEN_WAIT_MS);
    const request = indexedDB.open(DATABASE, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(STORE);
    request.onsuccess = () => { clearTimeout(giveUp); resolve(request.result); };
    request.onerror = () => { clearTimeout(giveUp); reject(request.error); };
  });
}

async function withStore(mode, work) {
  const database = await open();
  try {
    return await new Promise((resolve, reject) => {
      const transaction = database.transaction(STORE, mode);
      const request = work(transaction.objectStore(STORE));
      transaction.oncomplete = () => resolve(request.result);
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    });
  } finally {
    database.close();
  }
}

export const names = () => withStore('readonly', store => store.getAllKeys());

export const get = name => withStore('readonly', store => store.get(name));

// slice() stores just this file, not the whole block of memory the bytes arrived in.
export const put = (name, bytes) => withStore('readwrite', store => store.put(bytes.slice(), name));
