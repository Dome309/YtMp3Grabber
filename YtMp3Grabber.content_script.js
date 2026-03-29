// YtMp3Grabber.content_script.js
// Fixed floating button that does not depend on YouTube DOM structure.
// Works independently from YouTube UI updates.

// ──────────────────────────────────────────────
//  STATE
// ──────────────────────────────────────────────
const WIDGET_ID = 'yt-mp3-widget';
let selectedFolder = null;
let isDownloading  = false;

function t(key, substitutions) {
  if (!chrome?.i18n?.getMessage) return key;
  const value = chrome.i18n.getMessage(key, substitutions);
  return value || key;
}

// ──────────────────────────────────────────────
//  INIT: restore saved folder
// ──────────────────────────────────────────────
chrome.runtime.sendMessage({ type: 'GET_SAVED_FOLDER' }, (res) => {
  if (res?.folder) {
    selectedFolder = res.folder;
    const folderBtn = document.getElementById('yt-mp3-folder-btn');
    if (folderBtn) folderBtn.title = t('folderButtonTitleSelected', selectedFolder);
  }
});

// ──────────────────────────────────────────────
//  CREATE FLOATING WIDGET
// ──────────────────────────────────────────────
function createWidget() {
  if (document.getElementById(WIDGET_ID)) return;

  const widget = document.createElement('div');
  widget.id = WIDGET_ID;
  widget.style.cssText = `
    position: fixed;
    bottom: 24px;
    right: 24px;
    z-index: 999999;
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 8px;
    font-family: 'Roboto', Arial, sans-serif;
  `;

  // ── Riga pulsanti ──────────────────────────
  const row = document.createElement('div');
  row.style.cssText = `
    display: flex;
    align-items: center;
    gap: 8px;
    background: #1a1a1a;
    border: 1px solid #333;
    border-radius: 24px;
    padding: 6px 10px 6px 14px;
    box-shadow: 0 4px 20px rgba(0,0,0,0.5);
  `;

  // Label
  const label = document.createElement('span');
  label.textContent = t('widgetLabel');
  label.style.cssText = `
    color: #aaa;
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.5px;
    text-transform: uppercase;
    user-select: none;
  `;

  // Folder button
  const btnFolder = document.createElement('button');
  btnFolder.id = 'yt-mp3-folder-btn';
  btnFolder.textContent = '📁';
  btnFolder.title = selectedFolder
    ? t('folderButtonTitleSelected', selectedFolder)
    : t('folderButtonTitleDefault');
  styleBtn(btnFolder, '#333');
  btnFolder.addEventListener('click', handleSelectFolder);

  // Pulsante DOWNLOAD
  const btnDownload = document.createElement('button');
  btnDownload.id = 'yt-mp3-download-btn';
  btnDownload.textContent = t('downloadButtonText');
  btnDownload.title = t('downloadButtonTitle');
  styleBtn(btnDownload, '#cc0000');
  btnDownload.addEventListener('click', handleDownload);

  row.appendChild(label);
  row.appendChild(btnFolder);
  row.appendChild(btnDownload);
  widget.appendChild(row);
  document.body.appendChild(widget);
}

function styleBtn(btn, bg) {
  btn.style.cssText = `
    background: ${bg};
    color: #fff;
    border: none;
    border-radius: 16px;
    padding: 7px 14px;
    font-size: 13px;
    font-weight: 700;
    cursor: pointer;
    white-space: nowrap;
    transition: opacity 0.15s, transform 0.1s;
    outline: none;
  `;
  btn.addEventListener('mouseenter', () => {
    btn.style.opacity = '0.8';
    btn.style.transform = 'scale(1.04)';
  });
  btn.addEventListener('mouseleave', () => {
    btn.style.opacity = '1';
    btn.style.transform = 'scale(1)';
  });
}

// ──────────────────────────────────────────────
//  HANDLER: select folder
// ──────────────────────────────────────────────
async function handleSelectFolder() {
  const btn = document.getElementById('yt-mp3-folder-btn');
  if (!btn) return;

  btn.textContent = '⏳';
  btn.disabled = true;

  const res = await sendMessage({ type: 'SELECT_FOLDER' });

  btn.disabled = false;
  btn.textContent = '📁';

  if (res?.success) {
    selectedFolder = res.folder;
    btn.title = t('folderButtonTitleSelected', selectedFolder);
    showToast(t('folderSetToast'), '#2ecc71');
  } else {
    showToast(t('downloadErrorToast', res?.error || t('folderChangeErrorToast')), '#e74c3c');
  }
}

// ──────────────────────────────────────────────
//  HANDLER: download MP3
// ──────────────────────────────────────────────
async function handleDownload() {
  if (isDownloading) return;

  const btn = document.getElementById('yt-mp3-download-btn');
  if (!btn) return;

  if (!selectedFolder) {
    showToast(t('selectFolderFirstToast'), '#e67e22');
    await handleSelectFolder();
    if (!selectedFolder) return;
  }

  const url = window.location.href;

  isDownloading = true;
  btn.textContent = t('downloadingStatus');
  btn.style.background = '#888';
  btn.disabled = true;

  const res = await sendMessage({ type: 'DOWNLOAD', url, folder: selectedFolder });

  isDownloading = false;
  btn.disabled = false;

  if (res?.success) {
    btn.textContent = t('doneStatus');
    btn.style.background = '#27ae60';
    showToast(t('downloadSavedToast', selectedFolder), '#27ae60', 5000);
    setTimeout(() => {
      btn.textContent = t('downloadButtonText');
      btn.style.background = '#cc0000';
    }, 3000);
  } else {
    btn.textContent = t('errorStatus');
    btn.style.background = '#e74c3c';
    const err = res?.error ? t('downloadErrorToast', res.error) : t('downloadFailedError');
    showToast(err, '#e74c3c', 6000);
    setTimeout(() => {
      btn.textContent = t('downloadButtonText');
      btn.style.background = '#cc0000';
    }, 4000);
  }
}

// ──────────────────────────────────────────────
//  TOAST NOTIFICATION
// ──────────────────────────────────────────────
function showToast(message, color, duration = 3500) {
  let toast = document.getElementById('yt-mp3-toast');
  if (!toast) {
    toast = document.createElement('div');
    toast.id = 'yt-mp3-toast';
    toast.style.cssText = `
      position: fixed;
      bottom: 90px;
      right: 24px;
      z-index: 999999;
      padding: 12px 18px;
      border-radius: 10px;
      color: #fff;
      font-size: 13px;
      font-family: 'Roboto', Arial, sans-serif;
      font-weight: 500;
      box-shadow: 0 4px 16px rgba(0,0,0,0.5);
      max-width: 300px;
      white-space: pre-line;
      line-height: 1.4;
      transition: opacity 0.4s;
      pointer-events: none;
    `;
    document.body.appendChild(toast);
  }

  toast.textContent = message;
  toast.style.background = color;
  toast.style.opacity = '1';

  clearTimeout(toast._t);
  toast._t = setTimeout(() => { toast.style.opacity = '0'; }, duration);
}

// ──────────────────────────────────────────────
//  SPA NAVIGATION
// ──────────────────────────────────────────────
function onNavigate() {
  const isWatch = window.location.pathname === '/watch';
  if (!isWatch) {
    document.getElementById(WIDGET_ID)?.remove();
    document.getElementById('yt-mp3-toast')?.remove();
    isDownloading = false;
  } else {
    if (!document.getElementById(WIDGET_ID)) createWidget();
  }
}

document.addEventListener('yt-navigate-finish', onNavigate);

// ── Initial load ─────────────────────────────
if (window.location.pathname === '/watch') {
  if (document.body) {
    createWidget();
  } else {
    document.addEventListener('DOMContentLoaded', createWidget);
  }
}

// ──────────────────────────────────────────────
//  HELPER: sendMessage con Promise
// ──────────────────────────────────────────────
function sendMessage(msg) {
  return new Promise((resolve) => {
    chrome.runtime.sendMessage(msg, (response) => {
      if (chrome.runtime.lastError) {
        console.error('[YT-MP3]', chrome.runtime.lastError.message);
        resolve(null);
      } else {
        resolve(response);
      }
    });
  });
}