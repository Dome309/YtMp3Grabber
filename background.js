// background.js — Service Worker dell'estensione
// Gestisce tutte le chiamate HTTP alla API locale (.NET).
// Il content_script non chiama direttamente l'API per evitare
// qualsiasi conflitto con la CSP di YouTube.

const API_BASE = 'http://localhost:5000';

// ──────────────────────────────────────────────
//  MESSAGE HANDLER
//  Riceve messaggi dal content_script.js
// ──────────────────────────────────────────────
chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  switch (message.type) {
    case 'GET_SAVED_FOLDER':
      getSavedFolder().then(sendResponse);
      return true;

    case 'SELECT_FOLDER':
      selectFolder().then(sendResponse);
      return true;

    case 'DOWNLOAD':
      downloadMp3(message.url, message.folder).then(sendResponse);
      return true;
  }
});

// ──────────────────────────────────────────────
//  Recupera la cartella salvata in precedenza
// ──────────────────────────────────────────────
async function getSavedFolder() {
  const data = await chrome.storage.local.get('lastFolder');
  return { folder: data.lastFolder || null };
}

// ──────────────────────────────────────────────
//  Chiama l'API per aprire il dialogo cartella
//  e salva il percorso scelto in storage locale
// ──────────────────────────────────────────────
async function selectFolder() {
  try {
    const res = await fetch(`${API_BASE}/select-folder`, { method: 'GET' });

    if (!res.ok) {
      return { success: false, error: chrome.i18n.getMessage('selectFolderError') };
    }

    const data = await res.json();
    await chrome.storage.local.set({ lastFolder: data.path });
    return { success: true, folder: data.path };

  } catch {
    return { 
      success: false, 
      error: chrome.i18n.getMessage('serverUnreachableError') 
    };
  }
}

// ──────────────────────────────────────────────
//  Chiama l'API per scaricare e convertire in MP3
// ──────────────────────────────────────────────
async function downloadMp3(url, folder) {
  try {
    const res = await fetch(`${API_BASE}/download`, {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ url, folder })
    });

    if (res.ok) {
      // Notifica desktop al completamento
      chrome.notifications.create({
        type:    'basic',
        iconUrl: 'icon128.png',
        title:   chrome.i18n.getMessage('downloadWorkerNotificationTitle'),
        message: chrome.i18n.getMessage('downloadWorkerNotificationMessage', folder)
      });
      return { success: true };
    }

    const err = await res.json().catch(() => ({}));
    return { success: false, error: err.error || err.detail || chrome.i18n.getMessage('downloadFailedError') };

  } catch {
    return { 
      success: false, 
      error: 'Impossibile raggiungere il server locale.\nAssicurati che YtMp3Api.exe sia in esecuzione!' 
    };
  }
}
