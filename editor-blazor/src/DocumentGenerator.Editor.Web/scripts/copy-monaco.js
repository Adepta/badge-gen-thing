const fs = require('fs');
const path = require('path');

// --- Copy Monaco Editor ---
const monacoSrc = path.join(__dirname, '..', 'node_modules', 'monaco-editor', 'min');
const monacoDest = path.join(__dirname, '..', 'wwwroot', 'lib', 'monaco-editor');

function copyRecursive(source, target) {
    if (!fs.existsSync(target)) {
        fs.mkdirSync(target, { recursive: true });
    }
    const entries = fs.readdirSync(source, { withFileTypes: true });
    for (const entry of entries) {
        const srcPath = path.join(source, entry.name);
        const destPath = path.join(target, entry.name);
        if (entry.isDirectory()) {
            copyRecursive(srcPath, destPath);
        } else {
            fs.copyFileSync(srcPath, destPath);
        }
    }
}

if (fs.existsSync(monacoSrc)) {
    console.log('Copying Monaco Editor files...');
    copyRecursive(monacoSrc, monacoDest);
    console.log('Monaco Editor files copied to wwwroot/lib/monaco-editor');
} else {
    console.warn('Monaco Editor not found in node_modules. Run npm install first.');
}

// --- Copy Handlebars browser build ---
const handlebarsSrc = path.join(__dirname, '..', 'node_modules', 'handlebars', 'dist', 'handlebars.min.js');
const handlebarsDest = path.join(__dirname, '..', 'wwwroot', 'lib', 'handlebars');

if (fs.existsSync(handlebarsSrc)) {
    console.log('Copying Handlebars browser build...');
    if (!fs.existsSync(handlebarsDest)) {
        fs.mkdirSync(handlebarsDest, { recursive: true });
    }
    fs.copyFileSync(handlebarsSrc, path.join(handlebarsDest, 'handlebars.min.js'));
    console.log('Handlebars copied to wwwroot/lib/handlebars/handlebars.min.js');
} else {
    console.warn('Handlebars not found in node_modules. Run npm install first.');
}
