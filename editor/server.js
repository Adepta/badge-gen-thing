'use strict';

const express = require('express');
const fs      = require('fs');
const path    = require('path');
const multer  = require('multer');

const app  = express();
const PORT = process.env.PORT || 3500;

// The templates directory is volume-mounted at /templates inside the container,
// but falls back to ../../templates when running locally.
const TEMPLATES_DIR = process.env.TEMPLATES_DIR
  ? path.resolve(process.env.TEMPLATES_DIR)
  : path.resolve(__dirname, '..', 'templates');

// Assets directory — sits alongside templates (volume-mounted)
const ASSETS_DIR = process.env.ASSETS_DIR
  ? path.resolve(process.env.ASSETS_DIR)
  : path.resolve(TEMPLATES_DIR, 'assets');

// Ensure assets directory exists on startup
fs.mkdirSync(ASSETS_DIR, { recursive: true });

// Multer storage for asset uploads
const upload = multer({
  storage: multer.diskStorage({
    destination: (_req, _file, cb) => cb(null, ASSETS_DIR),
    filename:    (_req, file, cb) => {
      // Sanitise: only allow word chars, hyphens, dots
      const safe = file.originalname.replace(/[^a-zA-Z0-9._-]/g, '_');
      cb(null, safe);
    },
  }),
  limits: { fileSize: 5 * 1024 * 1024 }, // 5 MB max per file
  fileFilter: (_req, file, cb) => {
    if (/\.(png|jpe?g|gif|svg|webp)$/i.test(file.originalname)) {
      cb(null, true);
    } else {
      cb(new Error('Only image files are allowed (png, jpg, gif, svg, webp)'));
    }
  },
});

// ---------------------------------------------------------------------------
// Middleware
// ---------------------------------------------------------------------------
app.use(express.json({ limit: '2mb' }));
app.use(express.static(path.join(__dirname, 'public')));

// Serve uploaded assets at /assets/<filename>
app.use('/assets', express.static(ASSETS_DIR));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Returns true if `filename` is a safe relative name (no path traversal).
 */
function isSafeName(filename) {
  if (!filename) return false;
  const base = path.basename(filename);
  // Must be a .html, .css, or .json file and must not differ once basename-resolved
  return /^[\w\-]+\.(html|css|json)$/i.test(base) && base === filename;
}

/**
 * Return the absolute path only if it sits inside TEMPLATES_DIR.
 * Throws if path traversal is attempted.
 */
function safeTemplatePath(filename) {
  const resolved = path.resolve(TEMPLATES_DIR, filename);
  if (!resolved.startsWith(TEMPLATES_DIR + path.sep) && resolved !== TEMPLATES_DIR) {
    throw new Error('Path traversal attempt detected');
  }
  return resolved;
}

// ---------------------------------------------------------------------------
// Routes — Template listing
// ---------------------------------------------------------------------------

/**
 * GET /api/templates
 * Returns a list of template "pairs" (html+css sets) that exist in the
 * templates directory.  Each entry looks like:
 *   { name: "badge-pulse-a6", html: "badge-pulse-a6.html", css: "badge-pulse-a6.css" }
 */
app.get('/api/templates', (_req, res) => {
  let files;
  try {
    files = fs.readdirSync(TEMPLATES_DIR);
  } catch (err) {
    return res.status(500).json({ error: 'Cannot read templates directory', detail: err.message });
  }

  const htmlFiles = files.filter(f => f.endsWith('.html'));

  const pairs = htmlFiles.map(htmlFile => {
    const name    = htmlFile.replace(/\.html$/i, '');
    const cssFile = name + '.css';
    return {
      name,
      html:    htmlFile,
      css:     cssFile,
      hasCss:  files.includes(cssFile),
    };
  });

  res.json(pairs);
});

// ---------------------------------------------------------------------------
// Routes — Read a single file
// ---------------------------------------------------------------------------

/**
 * GET /api/templates/:filename
 * Returns the raw text content of a single template file (html or css).
 */
app.get('/api/templates/:filename', (req, res) => {
  const filename = req.params.filename;

  if (!isSafeName(filename)) {
    return res.status(400).json({ error: 'Invalid filename' });
  }

  let filepath;
  try {
    filepath = safeTemplatePath(filename);
  } catch {
    return res.status(400).json({ error: 'Invalid path' });
  }

  if (!fs.existsSync(filepath)) {
    return res.status(404).json({ error: 'File not found' });
  }

  const content = fs.readFileSync(filepath, 'utf8');
  res.type('text/plain').send(content);
});

// ---------------------------------------------------------------------------
// Routes — Save (overwrite or create new)
// ---------------------------------------------------------------------------

/**
 * PUT /api/templates/:filename
 * Body: { content: "<string>" }
 * Saves the file to the templates directory.
 * If the file already exists it is overwritten.
 * If the filename is new it is created (as long as the name is safe).
 */
app.put('/api/templates/:filename', (req, res) => {
  const filename = req.params.filename;
  const { content } = req.body;

  if (!isSafeName(filename)) {
    return res.status(400).json({ error: 'Invalid filename — must be alphanumeric/hyphens with .html or .css extension' });
  }

  if (typeof content !== 'string') {
    return res.status(400).json({ error: 'Missing or invalid "content" field' });
  }

  let filepath;
  try {
    filepath = safeTemplatePath(filename);
  } catch {
    return res.status(400).json({ error: 'Invalid path' });
  }

  try {
    fs.mkdirSync(TEMPLATES_DIR, { recursive: true });
    fs.writeFileSync(filepath, content, 'utf8');
  } catch (err) {
    return res.status(500).json({ error: 'Failed to write file', detail: err.message });
  }

  const isNew = !fs.existsSync(filepath + '.bak');
  res.json({ ok: true, filename, isNew });
});

// ---------------------------------------------------------------------------
// Routes — Delete a template pair
// ---------------------------------------------------------------------------

/**
 * DELETE /api/templates/:filename
 * Deletes the specified file from the templates directory.
 */
app.delete('/api/templates/:filename', (req, res) => {
  const filename = req.params.filename;

  if (!isSafeName(filename)) {
    return res.status(400).json({ error: 'Invalid filename' });
  }

  let filepath;
  try {
    filepath = safeTemplatePath(filename);
  } catch {
    return res.status(400).json({ error: 'Invalid path' });
  }

  if (!fs.existsSync(filepath)) {
    return res.status(404).json({ error: 'File not found' });
  }

  try {
    fs.unlinkSync(filepath);
  } catch (err) {
    return res.status(500).json({ error: 'Failed to delete file', detail: err.message });
  }

  res.json({ ok: true, filename });
});

// ---------------------------------------------------------------------------
// Routes — Rename a template (all related files)
// ---------------------------------------------------------------------------

/**
 * POST /api/templates/rename
 * Body: { oldName: "badge-old", newName: "badge-new" }
 * Renames .html, .css, and sample-*.json files atomically.
 */
app.post('/api/templates/rename', (req, res) => {
  const { oldName, newName } = req.body;

  if (!oldName || !newName) {
    return res.status(400).json({ error: 'Both oldName and newName are required' });
  }
  if (!/^[\w-]+$/.test(oldName) || !/^[\w-]+$/.test(newName)) {
    return res.status(400).json({ error: 'Names must be alphanumeric with hyphens only' });
  }
  if (oldName === newName) {
    return res.json({ ok: true, renamed: [] });
  }

  // Check the new name doesn't already exist
  const newHtml = path.resolve(TEMPLATES_DIR, newName + '.html');
  if (fs.existsSync(newHtml)) {
    return res.status(409).json({ error: 'A template named "' + newName + '" already exists' });
  }

  const filePairs = [
    [oldName + '.html', newName + '.html'],
    [oldName + '.css',  newName + '.css'],
    ['sample-' + oldName + '.json', 'sample-' + newName + '.json'],
  ];

  const renamed = [];
  try {
    for (const [oldFile, newFile] of filePairs) {
      const oldPath = path.resolve(TEMPLATES_DIR, oldFile);
      const newPath = path.resolve(TEMPLATES_DIR, newFile);
      if (fs.existsSync(oldPath)) {
        fs.renameSync(oldPath, newPath);
        renamed.push(oldFile + ' -> ' + newFile);
      }
    }
  } catch (err) {
    return res.status(500).json({ error: 'Rename failed', detail: err.message });
  }

  res.json({ ok: true, renamed });
});

// ---------------------------------------------------------------------------
// Routes — Asset upload
// ---------------------------------------------------------------------------

/**
 * POST /api/assets/upload
 * Multipart form with field name "asset" (single or multiple files).
 */
app.post('/api/assets/upload', (req, res) => {
  upload.array('asset', 10)(req, res, (err) => {
    if (err) {
      const msg = err instanceof multer.MulterError
        ? err.message
        : err.message || 'Upload failed';
      return res.status(400).json({ error: msg });
    }

    if (!req.files || !req.files.length) {
      return res.status(400).json({ error: 'No files received' });
    }

    const uploaded = req.files.map(f => ({
      filename: f.filename,
      size: f.size,
      url: '/assets/' + f.filename,
    }));

    res.json({ ok: true, files: uploaded });
  });
});

/**
 * GET /api/assets
 * Lists all assets in the assets directory.
 */
app.get('/api/assets', (_req, res) => {
  try {
    const files = fs.readdirSync(ASSETS_DIR);
    const assets = files
      .filter(f => /\.(png|jpe?g|gif|svg|webp)$/i.test(f))
      .map(f => {
        const stat = fs.statSync(path.join(ASSETS_DIR, f));
        return { filename: f, size: stat.size, url: '/assets/' + f };
      });
    res.json(assets);
  } catch (err) {
    res.status(500).json({ error: 'Cannot read assets directory', detail: err.message });
  }
});

// ---------------------------------------------------------------------------
// Catch-all — serve the SPA for any non-API, non-static route
// ---------------------------------------------------------------------------
app.get('*', (_req, res) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

// ---------------------------------------------------------------------------
// Start
// ---------------------------------------------------------------------------
app.listen(PORT, () => {
  console.log(`Template editor running on http://localhost:${PORT}`);
  console.log(`Templates directory: ${TEMPLATES_DIR}`);
  console.log(`Assets directory:    ${ASSETS_DIR}`);
});
