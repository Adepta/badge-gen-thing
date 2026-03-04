'use strict';

// ─────────────────────────────────────────────────────────
// State
// ─────────────────────────────────────────────────────────
const S = {
  current:       null,   // loaded template name (no extension)
  pendingSwitch: null,   // name to switch to after dirty confirm
  dirty:         false,
  previewMode:   'editor', // 'editor' | 'live'
};

// Default sample data (flat dotted-key-path → value)
const DEFAULT_SAMPLE_DATA = {
  'variables.firstName':     'Jane',
  'variables.lastName':      'Smith',
  'variables.jobTitle':      'Senior Engineer',
  'variables.company':       'Acme Corp',
  'variables.ticketType':    'Speaker',
  'variables.attendeeId':    'TC2026-00842',
  'variables.sessionName':   'Hall A \u2014 Keynote',
  'variables.eventDate':     '12\u201314 March 2026',
  'variables.eventVenue':    'ExCeL London',
  'branding.companyName':    'TechConf 2026',
  'branding.primaryColour':  '#6C3CE1',
  'branding.secondaryColour':'#F3F0FF',
  'branding.bodyFont':       'Segoe UI, Arial, sans-serif',
  'branding.custom.accentColour': '#FF5A5F',
};

// Current sample data (editable copy)
let sampleData = { ...DEFAULT_SAMPLE_DATA };

// ─────────────────────────────────────────────────────────
// Theme (dark / light)
// ─────────────────────────────────────────────────────────
function isDark() {
  return document.documentElement.classList.contains('dark');
}

function applyTheme(dark) {
  if (dark) {
    document.documentElement.classList.add('dark');
  } else {
    document.documentElement.classList.remove('dark');
  }
  localStorage.setItem('ed-theme', dark ? 'dark' : 'light');

  // Switch CodeMirror theme if editors exist
  const cmTheme = dark ? 'dracula' : 'default';
  if (typeof htmlCM !== 'undefined') {
    htmlCM.setOption('theme', cmTheme);
    cssCM.setOption('theme', cmTheme);
    setTimeout(() => { htmlCM.refresh(); cssCM.refresh(); }, 10);
  }
}

// Restore saved preference (default: dark)
(function initTheme() {
  const saved = localStorage.getItem('ed-theme');
  if (saved === 'light') {
    document.documentElement.classList.remove('dark');
  } else {
    document.documentElement.classList.add('dark');
  }
})();

// ─────────────────────────────────────────────────────────
// DOM
// ─────────────────────────────────────────────────────────
const $ = id => document.getElementById(id);
const crumbFile       = $('crumbFile');
const dirtyPill       = $('dirtyPill');
const statusChip      = $('statusChip');
const btnSave         = $('btnSave');
const btnNew          = $('btnNew');
const btnImportHtml   = $('btnImportHtml');
const btnImportCss    = $('btnImportCss');
const fileInputHtml   = $('fileInputHtml');
const fileInputCss    = $('fileInputCss');
const btnRefreshList  = $('btnRefreshList');
const btnRefreshPrev  = $('btnRefreshPreview');
const templateList    = $('templateList');
const previewFrame    = $('previewFrame');
const previewScale    = $('previewScale');
const previewSize     = $('previewSize');
const customSizeWrap  = $('customSizeWrap');
const customW         = $('customW');
const customH         = $('customH');
const emptyState      = $('emptyState');
const modalNew        = $('modalNew');
const btnNewCancel    = $('btnNewCancel');
const btnNewCreate    = $('btnNewCreate');
const newName         = $('newName');
const newSize         = $('newSize');
const modalConfirm    = $('modalConfirm');
const confirmName     = $('confirmName');
const btnConfirmDiscard = $('btnConfirmDiscard');
const btnConfirmSave    = $('btnConfirmSave');
const toast           = $('toast');
const toastIcon       = $('toastIcon');
const toastMsg        = $('toastMsg');
const htmlEditorBox   = $('htmlEditorBox');
const cssEditorBox    = $('cssEditorBox');
const editorResizer   = $('editorResizer');
const previewModeToggle = $('previewModeToggle');

// ─────────────────────────────────────────────────────────
// CodeMirror editors
// ─────────────────────────────────────────────────────────
const CM_COMMON = {
  theme: isDark() ? 'dracula' : 'default',
  lineNumbers: true,
  lineWrapping: false,
  autoCloseBrackets: true,
  tabSize: 2,
  indentWithTabs: false,
  extraKeys: {
    'Ctrl-S': () => save(),
    'Cmd-S':  () => save(),
    'Ctrl-/': 'toggleComment',
  },
};

const htmlCM = CodeMirror($('wrapHtml'), { ...CM_COMMON, mode: 'htmlmixed', autoCloseTags: true });
const cssCM  = CodeMirror($('wrapCss'),  { ...CM_COMMON, mode: 'css' });

htmlCM.setSize(null, '100%');
cssCM.setSize(null,  '100%');

// Theme toggle button
$('btnThemeToggle').addEventListener('click', () => {
  applyTheme(!isDark());
});

// ─────────────────────────────────────────────────────────
// Track last-focused editor (for Quick Parts insertion)
// ─────────────────────────────────────────────────────────
let _lastFocusedEditor = htmlCM;

htmlCM.on('focus', () => { _lastFocusedEditor = htmlCM; });
cssCM.on('focus',  () => { _lastFocusedEditor = cssCM;  });

function activeEditor() {
  return _lastFocusedEditor;
}

function insertAtCursor(cm, text) {
  const cursor = cm.getCursor();
  cm.replaceRange(text, cursor);
  cm.focus();
}

// ─────────────────────────────────────────────────────────
// Draggable resizer between HTML and CSS editors
// ─────────────────────────────────────────────────────────
(function initResizer() {
  let startY = 0;
  let startHtmlFlex = 0;
  let startCssFlex = 0;
  let totalHeight = 0;

  editorResizer.addEventListener('mousedown', e => {
    e.preventDefault();
    startY = e.clientY;
    const paneRect = htmlEditorBox.parentElement.getBoundingClientRect();
    totalHeight = paneRect.height - editorResizer.offsetHeight;

    const htmlRect = htmlEditorBox.getBoundingClientRect();
    const cssRect  = cssEditorBox.getBoundingClientRect();
    startHtmlFlex = htmlRect.height;
    startCssFlex  = cssRect.height;

    editorResizer.classList.add('dragging');
    document.body.style.cursor = 'row-resize';
    document.body.style.userSelect = 'none';

    function onMove(ev) {
      const dy = ev.clientY - startY;
      let newHtml = startHtmlFlex + dy;
      let newCss  = startCssFlex - dy;

      // Enforce minimum heights
      const minH = 60;
      if (newHtml < minH) { newCss -= (minH - newHtml); newHtml = minH; }
      if (newCss  < minH) { newHtml -= (minH - newCss); newCss  = minH; }

      const sum = newHtml + newCss;
      htmlEditorBox.style.flex = (newHtml / sum * 10).toFixed(2);
      cssEditorBox.style.flex  = (newCss  / sum * 10).toFixed(2);

      htmlCM.refresh();
      cssCM.refresh();
    }

    function onUp() {
      editorResizer.classList.remove('dragging');
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onUp);
      htmlCM.refresh();
      cssCM.refresh();
    }

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
  });
})();

// ─────────────────────────────────────────────────────────
// Dirty state
// ─────────────────────────────────────────────────────────
function setDirty(val) {
  S.dirty = val;
  document.body.classList.toggle('dirty', val);
}

htmlCM.on('change', () => { setDirty(true); schedulePrev(); });
cssCM.on('change',  () => { setDirty(true); schedulePrev(); });

// ─────────────────────────────────────────────────────────
// Preview mode toggle
// ─────────────────────────────────────────────────────────
previewModeToggle.querySelectorAll('button').forEach(btn => {
  btn.addEventListener('click', () => {
    previewModeToggle.querySelectorAll('button').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    S.previewMode = btn.dataset.mode;
    renderPreview();
  });
});

// ─────────────────────────────────────────────────────────
// Handlebars helpers (for Live Preview)
// ─────────────────────────────────────────────────────────
if (typeof Handlebars !== 'undefined') {
  // qrCode → grey 80x80 SVG placeholder
  Handlebars.registerHelper('qrCode', function(/* ...args */) {
    return new Handlebars.SafeString(
      '<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">' +
      '<rect width="80" height="80" rx="4" fill="#ccc"/>' +
      '<text x="40" y="44" text-anchor="middle" font-size="10" font-family="sans-serif" fill="#666">QR</text>' +
      '</svg>'
    );
  });

  // barCode → grey 160x40 SVG placeholder
  Handlebars.registerHelper('barCode', function(/* ...args */) {
    return new Handlebars.SafeString(
      '<svg xmlns="http://www.w3.org/2000/svg" width="160" height="40" viewBox="0 0 160 40">' +
      '<rect width="160" height="40" rx="2" fill="#ccc"/>' +
      '<text x="80" y="24" text-anchor="middle" font-size="10" font-family="sans-serif" fill="#666">BARCODE</text>' +
      '</svg>'
    );
  });

  Handlebars.registerHelper('upper', function(str) {
    return typeof str === 'string' ? str.toUpperCase() : str;
  });

  Handlebars.registerHelper('lower', function(str) {
    return typeof str === 'string' ? str.toLowerCase() : str;
  });

  Handlebars.registerHelper('formatDate', function(dateStr /*, format */) {
    return dateStr; // pass-through — real formatting is done server-side
  });

  Handlebars.registerHelper('currency', function(amount /*, currencyCode */) {
    return amount;
  });

  Handlebars.registerHelper('ifEquals', function(a, b, options) {
    return (a === b) ? options.fn(this) : options.inverse(this);
  });
}

// ─────────────────────────────────────────────────────────
// Sample data → nested object (for Handlebars context)
// ─────────────────────────────────────────────────────────
function flatToNested(flat) {
  const obj = {};
  for (const [dotPath, val] of Object.entries(flat)) {
    const parts = dotPath.split('.');
    let cur = obj;
    for (let i = 0; i < parts.length - 1; i++) {
      if (!cur[parts[i]] || typeof cur[parts[i]] !== 'object') cur[parts[i]] = {};
      cur = cur[parts[i]];
    }
    cur[parts[parts.length - 1]] = val;
  }
  return obj;
}

// Resolve CSS tokens: replace {{branding.primaryColour}} etc. via regex
function resolveCssTokens(css, flat) {
  return css.replace(/\{\{([^}]+)\}\}/g, (match, key) => {
    const k = key.trim();
    return flat[k] !== undefined ? flat[k] : match;
  });
}

// ─────────────────────────────────────────────────────────
// Live preview
// ─────────────────────────────────────────────────────────
let prevTimer = null;
function schedulePrev() { clearTimeout(prevTimer); prevTimer = setTimeout(renderPreview, 280); }

function getDims() {
  if (previewSize.value === 'custom') return { w: customW.value || '100mm', h: customH.value || '60mm' };
  const [w, h] = previewSize.value.split(',');
  return { w, h };
}

function toPx(v) {
  if (v.endsWith('mm')) return parseFloat(v) * 3.7795;
  if (v.endsWith('px')) return parseFloat(v);
  return parseFloat(v);
}

// Track the last Blob URL so we can revoke it and avoid memory leaks
let _prevBlobUrl = null;

function renderPreview() {
  let html = htmlCM.getValue();
  let css  = cssCM.getValue();

  // Always resolve CSS tokens (branding colours etc.) so the preview
  // looks consistent in both modes — without this, Editor mode shows
  // broken styles because {{branding.primaryColour}} is invalid CSS.
  css = resolveCssTokens(css, sampleData);

  if (S.previewMode === 'live') {
    // Live Preview: also compile HTML with Handlebars
    try {
      const nestedData = flatToNested(sampleData);
      const tpl = Handlebars.compile(html, { noEscape: false });
      html = tpl(nestedData);
    } catch (err) {
      // On compile error, show the raw template with an error banner
      html = '<div style="background:#f25c6e;color:#fff;padding:8px;font-size:12px;font-family:sans-serif">Handlebars error: ' +
             err.message.replace(/</g, '&lt;') + '</div>\n' + html;
    }
  } else {
    // Editor Preview: show raw Handlebars tokens as literal text.
    // Only resolve triple-stache helpers (qrCode/barCode) to SVG
    // placeholders so they don't break the layout.
    html = html.replace(/\{\{\{([^}]+)\}\}\}/g, function(match, expr) {
      const trimmed = expr.trim();
      if (trimmed.startsWith('qrCode')) {
        return '<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">' +
          '<rect width="80" height="80" rx="4" fill="#ccc"/>' +
          '<text x="40" y="44" text-anchor="middle" font-size="10" font-family="sans-serif" fill="#666">QR</text></svg>';
      }
      if (trimmed.startsWith('barCode')) {
        return '<svg xmlns="http://www.w3.org/2000/svg" width="160" height="40" viewBox="0 0 160 40">' +
          '<rect width="160" height="40" rx="2" fill="#ccc"/>' +
          '<text x="80" y="24" text-anchor="middle" font-size="10" font-family="sans-serif" fill="#666">BARCODE</text></svg>';
      }
      return match;
    });
    // Escape remaining double-stache tokens so they render as visible
    // text instead of being swallowed by the browser as invalid HTML.
    // {{upper variables.firstName}} → shows literally as that string.
    // (No replacement — tokens stay as-is in the HTML source.)
  }

  // Inject the CSS editor contents just before </head>.
  const baseTag  = '<base href="' + location.origin + '/">';
  const styleTag = '<style>\n/* === CSS editor === */\n' + css + '\n</style>';

  let combined;
  if (html.includes('</head>')) {
    combined = html
      .replace(/<head>/i,   '<head>\n  ' + baseTag)
      .replace(/<\/head>/i, styleTag + '\n</head>');
  } else {
    combined = '<!DOCTYPE html><html><head>' + baseTag + styleTag + '</head><body>' + html + '</body></html>';
  }

  // Size the iframe
  const { w, h } = getDims();
  const scale = parseFloat(previewScale.value);
  previewFrame.style.width     = toPx(w) + 'px';
  previewFrame.style.height    = toPx(h) + 'px';
  previewFrame.style.transform = 'scale(' + scale + ')';

  // Use a Blob URL so the iframe has a real origin and can load
  // external resources (Google Fonts, CDN scripts, etc.)
  const blob = new Blob([combined], { type: 'text/html' });
  const url  = URL.createObjectURL(blob);

  previewFrame.src = url;

  // Revoke the previous URL once the new one has loaded
  previewFrame.onload = () => {
    if (_prevBlobUrl) URL.revokeObjectURL(_prevBlobUrl);
    _prevBlobUrl = url;
  };
}

previewScale.addEventListener('change', renderPreview);
previewSize.addEventListener('change', () => {
  customSizeWrap.style.display = previewSize.value === 'custom' ? 'flex' : 'none';
  renderPreview();
});
customW.addEventListener('input', renderPreview);
customH.addEventListener('input', renderPreview);
btnRefreshPrev.addEventListener('click', renderPreview);

function autoSize(name) {
  if (!name) return;
  if (name.includes('-cc'))                         previewSize.value = '85.6mm,54mm';
  else if (name.includes('-a6'))                    previewSize.value = '105mm,148mm';
  else if (name.includes('invoice') || name.includes('-a4')) previewSize.value = '210mm,297mm';
  else                                              previewSize.value = '105mm,148mm';
  customSizeWrap.style.display = 'none';
}

// ─────────────────────────────────────────────────────────
// Sample Data panel
// ─────────────────────────────────────────────────────────
const sampleDataSection = $('sampleDataSection');
const sampleDataToggle  = $('sampleDataToggle');
const sampleDataBody    = $('sampleDataBody');
const sdAddKey          = $('sdAddKey');
const sdAddVal          = $('sdAddVal');
const sdAddBtn          = $('sdAddBtn');
const sdResetBtn        = $('sdResetBtn');

let sdDebounce = null;

sampleDataToggle.addEventListener('click', () => {
  sampleDataSection.classList.toggle('open');
});

function renderSampleDataRows() {
  // Remove existing rows (keep add-row and reset button)
  sampleDataBody.querySelectorAll('.sd-row').forEach(r => r.remove());

  const addRow = sampleDataBody.querySelector('.sd-add-row');

  for (const [key, val] of Object.entries(sampleData)) {
    const row = document.createElement('div');
    row.className = 'sd-row';

    const keyEl = document.createElement('div');
    keyEl.className = 'sd-key';
    keyEl.textContent = key;
    keyEl.title = key;

    const valEl = document.createElement('input');
    valEl.className = 'sd-val';
    valEl.type = 'text';
    valEl.value = val;
    valEl.addEventListener('input', () => {
      sampleData[key] = valEl.value;
      clearTimeout(sdDebounce);
      sdDebounce = setTimeout(() => {
        if (S.previewMode === 'live') renderPreview();
      }, 300);
    });

    const delBtn = document.createElement('button');
    delBtn.className = 'sd-del';
    delBtn.title = 'Remove field';
    delBtn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>';
    delBtn.addEventListener('click', () => {
      delete sampleData[key];
      renderSampleDataRows();
      if (S.previewMode === 'live') renderPreview();
    });

    row.appendChild(keyEl);
    row.appendChild(valEl);
    row.appendChild(delBtn);
    sampleDataBody.insertBefore(row, addRow);
  }
}

sdAddBtn.addEventListener('click', () => {
  const k = sdAddKey.value.trim();
  const v = sdAddVal.value.trim();
  if (!k) return;
  sampleData[k] = v;
  sdAddKey.value = '';
  sdAddVal.value = '';
  renderSampleDataRows();
  if (S.previewMode === 'live') renderPreview();
});
sdAddKey.addEventListener('keydown', e => { if (e.key === 'Enter') sdAddBtn.click(); });
sdAddVal.addEventListener('keydown', e => { if (e.key === 'Enter') sdAddBtn.click(); });

sdResetBtn.addEventListener('click', () => {
  sampleData = { ...DEFAULT_SAMPLE_DATA };
  renderSampleDataRows();
  if (S.previewMode === 'live') renderPreview();
});

// ─────────────────────────────────────────────────────────
// API helpers
// ─────────────────────────────────────────────────────────
async function apiFetch(url, opts = {}) {
  const r = await fetch(url, opts);
  if (!r.ok) {
    const b = await r.json().catch(() => ({ error: r.statusText }));
    throw new Error(b.error || r.statusText);
  }
  return r;
}

// ─────────────────────────────────────────────────────────
// Template list rendering
// ─────────────────────────────────────────────────────────
function iconClass(name) {
  if (name.startsWith('badge-pulse'))     return 'pulse';
  if (name.startsWith('badge-executive')) return 'executive';
  if (name.startsWith('badge-carbon'))    return 'carbon';
  if (name.startsWith('invoice'))         return 'invoice';
  if (name.startsWith('badge'))           return 'badge';
  return 'custom';
}

function iconLabel(name) {
  const cls = iconClass(name);
  const map = { pulse: 'PL', executive: 'EX', carbon: 'CA', invoice: 'IN', badge: 'BG', custom: '??' };
  return map[cls];
}

function sizeLabel(name) {
  if (name.endsWith('-a6'))  return 'A6 \u00b7 105x148mm';
  if (name.endsWith('-cc'))  return 'Credit Card \u00b7 85.6x54mm';
  if (name.includes('invoice')) return 'A4 \u00b7 210x297mm';
  return 'Badge';
}

function groupKey(name) {
  if (name.startsWith('badge-pulse'))     return 'Pulse';
  if (name.startsWith('badge-executive')) return 'Executive';
  if (name.startsWith('badge-carbon'))    return 'Carbon';
  if (name.startsWith('invoice'))         return 'Invoice';
  if (name.startsWith('badge'))           return 'Badge';
  return 'Custom';
}

async function refreshList(selectName) {
  try {
    const r    = await apiFetch('/api/templates');
    const list = await r.json();

    if (!list.length) {
      templateList.innerHTML = '<div style="padding:20px 12px;text-align:center;color:var(--col-text-faint);font-size:11px;">No templates found.</div>';
      return;
    }

    // Group templates
    const groups = {};
    list.forEach(t => {
      const g = groupKey(t.name);
      if (!groups[g]) groups[g] = [];
      groups[g].push(t);
    });

    let html = '';
    for (const [group, items] of Object.entries(groups)) {
      html += '<div class="tpl-group-label">' + group + '</div>';
      items.forEach(t => {
        const active = t.name === (selectName || S.current) ? 'active' : '';
        html += '<div class="tpl-item ' + active + '" data-name="' + t.name + '" tabindex="0" role="button">' +
          '<div class="tpl-item-icon ' + iconClass(t.name) + '">' + iconLabel(t.name) + '</div>' +
          '<div class="tpl-item-info">' +
            '<div class="tpl-item-name">' + t.name + '</div>' +
            '<div class="tpl-item-size">' + sizeLabel(t.name) + '</div>' +
          '</div></div>';
      });
    }
    templateList.innerHTML = html;

    // Click handlers
    templateList.querySelectorAll('.tpl-item').forEach(el => {
      el.addEventListener('click', () => switchTemplate(el.dataset.name));
      el.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') switchTemplate(el.dataset.name); });
    });
  } catch (err) {
    showToast(err.message, 'error');
  }
}

function setActiveItem(name) {
  templateList.querySelectorAll('.tpl-item').forEach(el => {
    el.classList.toggle('active', el.dataset.name === name);
  });
}

// ─────────────────────────────────────────────────────────
// Load template (+ auto-load sample data JSON if available)
// ─────────────────────────────────────────────────────────
async function loadTemplate(name) {
  if (!name) return;
  setStatus('Loading\u2026', 'saving');
  S.current = name;
  crumbFile.textContent = name;
  crumbFile.classList.remove('none');
  setActiveItem(name);

  try {
    const [h, c] = await Promise.all([
      apiFetch('/api/templates/' + name + '.html').then(r => r.text()),
      apiFetch('/api/templates/' + name + '.css').then(r => r.text()).catch(() => '/* No CSS found \u2014 will be created on save */\n'),
    ]);

    htmlCM.setValue(h); cssCM.setValue(c);
    htmlCM.clearHistory(); cssCM.clearHistory();
    setDirty(false);
    autoSize(name);

    // Try to load matching sample JSON to populate the sample data panel
    await loadSampleJson(name);

    renderPreview();
    emptyState.classList.add('hidden');
    setStatus('Loaded', 'ok');
  } catch (err) {
    setStatus('Load failed', 'error');
    showToast(err.message, 'error');
  }
}

// Try to load sample-{name}.json and populate sampleData from it
async function loadSampleJson(name) {
  try {
    const r = await fetch('/api/templates/sample-' + name + '.json');
    if (!r.ok) return; // no sample JSON — keep defaults
    const json = await r.json();

    // Flatten the nested JSON into our flat key-path format
    sampleData = {};
    function flatten(obj, prefix) {
      for (const [k, v] of Object.entries(obj)) {
        const path = prefix ? prefix + '.' + k : k;
        if (v && typeof v === 'object' && !Array.isArray(v)) {
          flatten(v, path);
        } else {
          sampleData[path] = String(v);
        }
      }
    }
    // Only pull variables and branding
    if (json.variables) flatten(json.variables, 'variables');
    if (json.branding) flatten(json.branding, 'branding');

    renderSampleDataRows();
  } catch {
    // Silently ignore — just keep current sampleData
  }
}

// ─────────────────────────────────────────────────────────
// Switch (with dirty guard)
// ─────────────────────────────────────────────────────────
function switchTemplate(name) {
  if (name === S.current) return;
  if (S.dirty && S.current) {
    S.pendingSwitch = name;
    confirmName.textContent = S.current;
    modalConfirm.classList.add('open');
  } else {
    loadTemplate(name);
  }
}

btnConfirmDiscard.addEventListener('click', () => {
  const next = S.pendingSwitch; S.pendingSwitch = null;
  modalConfirm.classList.remove('open');
  setDirty(false);
  loadTemplate(next);
});

btnConfirmSave.addEventListener('click', async () => {
  const next = S.pendingSwitch; S.pendingSwitch = null;
  modalConfirm.classList.remove('open');
  await save();
  loadTemplate(next);
});

// ─────────────────────────────────────────────────────────
// Save
// ─────────────────────────────────────────────────────────
async function save() {
  if (!S.current) { showToast('No template loaded', 'error'); return; }
  setStatus('Saving\u2026', 'saving');
  try {
    await Promise.all([
      apiFetch('/api/templates/' + S.current + '.html', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ content: htmlCM.getValue() }) }),
      apiFetch('/api/templates/' + S.current + '.css',  { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ content: cssCM.getValue() }) }),
    ]);
    setDirty(false);
    setStatus('Saved', 'ok');
    showToast('Saved \u2014 ' + S.current, 'success');
  } catch (err) {
    setStatus('Save failed', 'error');
    showToast('Save failed: ' + err.message, 'error');
  }
}

btnSave.addEventListener('click', save);

// ─────────────────────────────────────────────────────────
// Per-editor import from disk
// ─────────────────────────────────────────────────────────
btnImportHtml.addEventListener('click', () => fileInputHtml.click());
btnImportCss.addEventListener('click',  () => fileInputCss.click());

fileInputHtml.addEventListener('change', () => {
  const f = fileInputHtml.files[0];
  if (!f) return;
  fileInputHtml.value = '';
  const reader = new FileReader();
  reader.onload = e => {
    htmlCM.setValue(e.target.result);
    if (!S.current) {
      S.current = f.name.replace(/\.html$/i, '');
      crumbFile.textContent = S.current;
      crumbFile.classList.remove('none');
      emptyState.classList.add('hidden');
    }
    setDirty(true); schedulePrev();
    showToast('Imported ' + f.name, 'success');
  };
  reader.readAsText(f);
});

fileInputCss.addEventListener('change', () => {
  const f = fileInputCss.files[0];
  if (!f) return;
  fileInputCss.value = '';
  const reader = new FileReader();
  reader.onload = e => {
    cssCM.setValue(e.target.result);
    if (!S.current) {
      S.current = f.name.replace(/\.css$/i, '');
      crumbFile.textContent = S.current;
      crumbFile.classList.remove('none');
      emptyState.classList.add('hidden');
    }
    setDirty(true); schedulePrev();
    showToast('Imported ' + f.name, 'success');
  };
  reader.readAsText(f);
});

// ─────────────────────────────────────────────────────────
// New template modal
// ─────────────────────────────────────────────────────────
btnNew.addEventListener('click', () => { newName.value = ''; modalNew.classList.add('open'); setTimeout(() => newName.focus(), 60); });
btnNewCancel.addEventListener('click', () => modalNew.classList.remove('open'));
modalNew.addEventListener('click', e => { if (e.target === modalNew) modalNew.classList.remove('open'); });

btnNewCreate.addEventListener('click', async () => {
  const n = newName.value.trim();
  if (!n || !/^[\w-]+$/.test(n)) {
    newName.style.borderColor = 'var(--col-red)';
    setTimeout(() => newName.style.borderColor = '', 1600);
    showToast('Invalid name \u2014 alphanumeric and hyphens only', 'error');
    return;
  }
  modalNew.classList.remove('open');
  const sz = newSize.value;
  const [w, h] = sz ? sz.split(',') : ['', ''];
  const dims = w ? 'width: ' + w + '; height: ' + h + ';' : '';

  const scaffoldHtml = '<!DOCTYPE html>\n<html>\n<head>\n  <meta charset="utf-8">\n</head>\n<body>\n<div class="badge">\n  <div class="name">{{variables.firstName}} {{variables.lastName}}</div>\n  <div class="company">{{variables.company}}</div>\n</div>\n</body>\n</html>';
  const scaffoldCss  = '/* ' + n + ' */\n* { margin: 0; padding: 0; box-sizing: border-box; }\n\nbody {\n  ' + dims + '\n  font-family: \'Inter\', system-ui, sans-serif;\n  overflow: hidden;\n}\n\n.badge {\n  ' + dims + '\n  display: flex;\n  flex-direction: column;\n  align-items: center;\n  justify-content: center;\n  padding: 8mm;\n  background: #1a1040;\n  color: #fff;\n}\n\n.name    { font-size: 24pt; font-weight: 700; }\n.company { font-size: 12pt; opacity: .7; margin-top: 4mm; }';

  try {
    await Promise.all([
      apiFetch('/api/templates/' + n + '.html', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ content: scaffoldHtml }) }),
      apiFetch('/api/templates/' + n + '.css',  { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ content: scaffoldCss  }) }),
    ]);
    await refreshList(n);
    await loadTemplate(n);
    showToast('Created \u2014 ' + n, 'success');
  } catch (err) {
    showToast('Create failed: ' + err.message, 'error');
  }
});
newName.addEventListener('keydown', e => { if (e.key === 'Enter') btnNewCreate.click(); });

// ─────────────────────────────────────────────────────────
// Refresh list button
// ─────────────────────────────────────────────────────────
btnRefreshList.addEventListener('click', () => refreshList(S.current));

// ─────────────────────────────────────────────────────────
// Global keyboard
// ─────────────────────────────────────────────────────────
document.addEventListener('keydown', e => {
  if ((e.ctrlKey || e.metaKey) && e.key === 's') { e.preventDefault(); save(); }
  if (e.key === 'Escape') {
    modalNew.classList.remove('open');
    modalConfirm.classList.remove('open');
  }
});

// ─────────────────────────────────────────────────────────
// Unload guard
// ─────────────────────────────────────────────────────────
window.addEventListener('beforeunload', e => {
  if (S.dirty) { e.preventDefault(); e.returnValue = ''; }
});

// ─────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────
function setStatus(msg, type) {
  statusChip.textContent = msg;
  statusChip.classList.remove('ok', 'error', 'saving');
  if (type) statusChip.classList.add(type);
}

const ICONS = {
  success: '<polyline points="20 6 9 17 4 12"/>',
  error:   '<circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/>',
  info:    '<circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><circle cx="12" cy="16" r=".5"/>',
};

let toastTimer;
function showToast(msg, type) {
  if (type === undefined) type = 'info';
  toastMsg.textContent = msg;
  toastIcon.innerHTML  = ICONS[type] || ICONS.info;
  toast.classList.remove('show', 'success', 'error', 'info');
  toast.classList.add('show', type);
  clearTimeout(toastTimer);
  toastTimer = setTimeout(function() { toast.classList.remove('show', 'success', 'error', 'info'); }, 3600);
}

// ─────────────────────────────────────────────────────────
// Quick Parts
// ─────────────────────────────────────────────────────────

// Token definitions
const QP_VARS = [
  { label: '{{variables.firstName}}',   insert: '{{variables.firstName}}' },
  { label: '{{variables.lastName}}',    insert: '{{variables.lastName}}' },
  { label: '{{variables.jobTitle}}',    insert: '{{variables.jobTitle}}' },
  { label: '{{variables.company}}',     insert: '{{variables.company}}' },
  { label: '{{variables.attendeeId}}',  insert: '{{variables.attendeeId}}' },
  { label: '{{variables.ticketType}}',  insert: '{{variables.ticketType}}' },
  { label: '{{variables.sessionName}}', insert: '{{variables.sessionName}}' },
  { label: '{{variables.eventDate}}',   insert: '{{variables.eventDate}}' },
  { label: '{{variables.eventVenue}}',  insert: '{{variables.eventVenue}}' },
];

const QP_BRAND = [
  { label: '{{branding.companyName}}',     insert: '{{branding.companyName}}' },
  { label: '{{branding.primaryColour}}',   insert: '{{branding.primaryColour}}' },
  { label: '{{branding.secondaryColour}}', insert: '{{branding.secondaryColour}}' },
  { label: '{{branding.bodyFont}}',        insert: '{{branding.bodyFont}}' },
  { label: '{{branding.custom.accentColour}}', insert: '{{branding.custom.accentColour}}' },
];

const QP_HELPERS = [
  { label: '{{{qrCode \u2026}}}',  insert: '{{{qrCode variables.attendeeId "#ffffff" "transparent"}}}', triple: true },
  { label: '{{{barCode \u2026}}}', insert: '{{{barCode variables.attendeeId}}}', triple: true },
  { label: '{{upper \u2026}}',     insert: '{{upper variables.firstName}}' },
  { label: '{{lower \u2026}}',     insert: '{{lower variables.ticketType}}' },
  { label: '{{formatDate \u2026}}',insert: '{{formatDate variables.eventDate "DD MMM YYYY"}}' },
  { label: '{{currency \u2026}}',  insert: '{{currency variables.price "GBP"}}' },
  { label: '{{#ifEquals}}',        insert: '{{#ifEquals variables.ticketType "VIP"}}VIP content{{/ifEquals}}' },
];

const QP_CSS = [
  { label: 'primaryColour',   insert: '{{branding.primaryColour}}',   css: true },
  { label: 'secondaryColour', insert: '{{branding.secondaryColour}}',  css: true },
  { label: 'accentColour',    insert: '{{branding.custom.accentColour}}', css: true },
  { label: 'bodyFont',        insert: "'{{branding.bodyFont}}', sans-serif", css: true },
];

// Block definitions
const QP_BLOCKS = [
  {
    name: 'Header bar',
    desc: 'Company name, event date and venue, with ticket-type pill',
    target: 'html',
    icon: '<path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>',
    code: '<div class="header">\n  <div class="header-left">\n    <div class="event-name">{{branding.companyName}}</div>\n    <div class="event-date">{{variables.eventDate}}</div>\n    <div class="event-venue">{{variables.eventVenue}}</div>\n  </div>\n  <div class="ticket-pill ticket-pill--{{lower variables.ticketType}}">{{upper variables.ticketType}}</div>\n</div>',
  },
  {
    name: 'Name block',
    desc: 'Large first name, last name, job title and company',
    target: 'html',
    icon: '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
    code: '<div class="name-block">\n  <div class="first-name">{{upper variables.firstName}}</div>\n  <div class="last-name">{{upper variables.lastName}}</div>\n  <div class="meta-block">\n    <div class="job-title">{{variables.jobTitle}}</div>\n    <div class="company">{{variables.company}}</div>\n  </div>\n</div>',
  },
  {
    name: 'QR footer',
    desc: 'Footer strip with attendee ID, session name and QR code',
    target: 'html',
    icon: '<rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><path d="M14 14h3v3h-3zM17 17h3v3h-3zM14 20h3"/>',
    code: '<div class="footer">\n  <div class="footer-col">\n    <div class="footer-label">Attendee ID</div>\n    <div class="footer-value mono">{{variables.attendeeId}}</div>\n    <div class="footer-label" style="margin-top:1.5mm">Session</div>\n    <div class="footer-value">{{variables.sessionName}}</div>\n  </div>\n  <div class="footer-divider"></div>\n  <div class="footer-qr">\n    {{{qrCode variables.attendeeId "#ffffff" "transparent"}}}\n  </div>\n</div>',
  },
  {
    name: 'Diagonal stripe',
    desc: 'Accent gradient stripe using brand colours',
    target: 'html',
    icon: '<line x1="5" y1="12" x2="19" y2="12"/><polyline points="12 5 19 12 12 19"/>',
    code: '<div class="stripe-wrap">\n  <div class="stripe"></div>\n</div>',
  },
  {
    name: 'Barcode footer',
    desc: 'Footer with Code-128 barcode and attendee ID',
    target: 'html',
    icon: '<path d="M3 5h2M7 5h2M13 5h2M17 5h2M21 5h1M3 19h2M7 19h2M13 19h2M17 19h2"/><rect x="3" y="3" width="18" height="18" rx="2" ry="2"/>',
    code: '<div class="footer">\n  <div class="barcode-wrap">\n    {{{barCode variables.attendeeId}}}\n  </div>\n  <div class="footer-id mono">{{variables.attendeeId}}</div>\n</div>',
  },
  {
    name: 'Stripe CSS',
    desc: 'CSS for the diagonal brand-colour accent stripe',
    target: 'css',
    icon: '<path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"/>',
    code: '.stripe-wrap {\n  position: relative;\n  height: 4mm;\n  flex-shrink: 0;\n  overflow: hidden;\n}\n.stripe {\n  position: absolute;\n  top: 0; left: -10%;\n  width: 120%;\n  height: 100%;\n  background: linear-gradient(90deg,\n    {{branding.custom.accentColour}} 0%,\n    {{branding.primaryColour}} 60%,\n    transparent 100%);\n  transform: skewX(-8deg);\n  transform-origin: left center;\n}',
  },
  {
    name: 'Ticket pill CSS',
    desc: 'Coloured pill styles for Speaker, VIP, Attendee, Sponsor, Staff',
    target: 'css',
    icon: '<path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/><line x1="7" y1="7" x2="7.01" y2="7"/>',
    code: '.ticket-pill {\n  font-size: 6pt; font-weight: 700;\n  letter-spacing: 1.2px; text-transform: uppercase;\n  padding: 1.5mm 3mm;\n  border-radius: 20mm;\n  white-space: nowrap;\n  border: 0.4mm solid transparent;\n}\n.ticket-pill--speaker  { background: {{branding.custom.accentColour}}; color: #0D0D1A; }\n.ticket-pill--vip      { background: transparent; border-color: #D4AF37; color: #D4AF37; }\n.ticket-pill--attendee { background: transparent; border-color: rgba(255,255,255,.35); color: rgba(255,255,255,.7); }\n.ticket-pill--sponsor  { background: #D4AF37; color: #0D0D1A; }\n.ticket-pill--staff    { background: #3B82F6; color: #fff; }',
  },
  {
    name: 'QR footer CSS',
    desc: 'Footer layout, column, divider and QR sizing',
    target: 'css',
    icon: '<path d="M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h3M17 17h3M14 20h3M20 14v3"/>',
    code: '.footer {\n  background: rgba(255,255,255,.04);\n  border-top: 0.3mm solid rgba(255,255,255,.1);\n  padding: 3.5mm 6mm;\n  display: flex; align-items: stretch; gap: 4mm;\n  flex-shrink: 0;\n}\n.footer-col { display: flex; flex-direction: column; justify-content: center; flex: 1; }\n.footer-qr  { flex-shrink: 0; width: 14mm; height: 14mm; }\n.footer-qr svg { width: 100%; height: 100%; display: block; }\n.footer-divider { width: 0.3mm; background: rgba(255,255,255,.12); align-self: stretch; }\n.footer-label {\n  font-size: 5pt; font-weight: 700;\n  text-transform: uppercase; letter-spacing: .8px;\n  color: {{branding.custom.accentColour}}; margin-bottom: .8mm;\n}\n.footer-value { font-size: 7.5pt; font-weight: 600; color: rgba(255,255,255,.8); }\n.mono { font-family: \'Courier New\', monospace; letter-spacing: .5px; }',
  },
];

// Render Quick Parts
function buildQpTokenChip(tok, extraClass) {
  if (!extraClass) extraClass = '';
  var chip = document.createElement('div');
  chip.className = 'qp-chip' + (extraClass ? ' ' + extraClass : '');
  chip.textContent = tok.label;
  chip.title = 'Click to insert: ' + tok.insert;
  chip.addEventListener('click', function() {
    insertAtCursor(activeEditor(), tok.insert);
    showToast('Inserted', 'info');
  });
  return chip;
}

function buildQpBlockCard(block) {
  var card = document.createElement('div');
  card.className = 'qp-block-card';
  card.innerHTML =
    '<div class="qp-block-icon">' +
      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' + block.icon + '</svg>' +
    '</div>' +
    '<div class="qp-block-info">' +
      '<div class="qp-block-name">' + block.name + '</div>' +
      '<div class="qp-block-desc">' + block.desc + '</div>' +
    '</div>' +
    '<span class="qp-block-target ' + block.target + '">' + block.target.toUpperCase() + '</span>';
  card.title = 'Insert into ' + block.target.toUpperCase() + ' editor';
  card.addEventListener('click', function() {
    var editor = block.target === 'css' ? cssCM : htmlCM;
    insertAtCursor(editor, block.code);
    showToast('Inserted "' + block.name + '"', 'info');
  });
  return card;
}

function initQp() {
  var varsEl    = $('qpTokensVars');
  var brandEl   = $('qpTokensBrand');
  var helpersEl = $('qpTokensHelpers');
  var cssEl     = $('qpTokensCss');
  var blockEl   = $('qpBlockList');

  QP_VARS.forEach(function(t)    { varsEl.appendChild(buildQpTokenChip(t)); });
  QP_BRAND.forEach(function(t)   { brandEl.appendChild(buildQpTokenChip(t)); });
  QP_HELPERS.forEach(function(t) { helpersEl.appendChild(buildQpTokenChip(t, t.triple ? 'triple' : '')); });
  QP_CSS.forEach(function(t)     { cssEl.appendChild(buildQpTokenChip(t, 'css-chip')); });
  QP_BLOCKS.forEach(function(b)  { blockEl.appendChild(buildQpBlockCard(b)); });
}

// Drawer toggle
var btnQpToggle = $('btnQpToggle');
var qpDrawer    = $('qpDrawer');

btnQpToggle.addEventListener('click', function() {
  var open = qpDrawer.classList.toggle('open');
  btnQpToggle.classList.toggle('active', open);
  setTimeout(function() { htmlCM.refresh(); cssCM.refresh(); }, 10);
});

// Inner Quick Parts tab switching
document.querySelectorAll('.qp-tab').forEach(function(tab) {
  tab.addEventListener('click', function() {
    var which = tab.dataset.qptab;
    document.querySelectorAll('.qp-tab').forEach(function(t) { t.classList.remove('active'); });
    tab.classList.add('active');
    document.querySelectorAll('.qp-panel').forEach(function(p) { p.classList.remove('active'); });
    $('qpPanel' + which.charAt(0).toUpperCase() + which.slice(1)).classList.add('active');
  });
});

// ─────────────────────────────────────────────────────────
// Boot
// ─────────────────────────────────────────────────────────
(async function() {
  initQp();
  renderSampleDataRows();
  try {
    await refreshList(null);
    setStatus('Ready', '');
  } catch (e) {
    setStatus('Server unreachable', 'error');
  }
})();
