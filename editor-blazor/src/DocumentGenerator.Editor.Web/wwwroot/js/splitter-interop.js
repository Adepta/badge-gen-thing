// Splitter Interop for Blazor
// General-purpose drag-to-resize panel splitter

const splitters = new Map();

export function initSplitter(splitterElementId, beforeElementId, afterElementId, direction, dotnetRef, storageKey) {
    const splitterEl = document.getElementById(splitterElementId);
    const beforeEl = document.getElementById(beforeElementId);
    const afterEl = document.getElementById(afterElementId);

    if (!splitterEl || !beforeEl || !afterEl) {
        console.error('Splitter: Could not find elements', { splitterElementId, beforeElementId, afterElementId });
        return;
    }

    const isHorizontal = direction === 'horizontal'; // left-right
    const cursor = isHorizontal ? 'col-resize' : 'row-resize';
    const minSize = 80; // px
    const maxSizePercent = 85; // %

    splitterEl.style.cursor = cursor;

    // Restore saved position
    const savedPercent = storageKey ? localStorage.getItem(storageKey) : null;
    if (savedPercent !== null) {
        applyPosition(parseFloat(savedPercent));
    }

    let isDragging = false;
    let startPos = 0;
    let startBeforeSize = 0;

    function getContainerSize() {
        const parent = splitterEl.parentElement;
        return isHorizontal ? parent.offsetWidth : parent.offsetHeight;
    }

    function applyPosition(percent) {
        percent = Math.max(100 - maxSizePercent, Math.min(maxSizePercent, percent));

        if (isHorizontal) {
            beforeEl.style.width = `${percent}%`;
            beforeEl.style.flexGrow = '0';
            beforeEl.style.flexShrink = '0';
            afterEl.style.flexGrow = '1';
        } else {
            beforeEl.style.height = `${percent}%`;
            beforeEl.style.flexGrow = '0';
            beforeEl.style.flexShrink = '0';
            afterEl.style.flexGrow = '1';
        }
    }

    function onMouseDown(e) {
        e.preventDefault();
        isDragging = true;
        startPos = isHorizontal ? e.clientX : e.clientY;
        startBeforeSize = isHorizontal ? beforeEl.offsetWidth : beforeEl.offsetHeight;

        document.body.style.cursor = cursor;
        document.body.style.userSelect = 'none';

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
    }

    function onMouseMove(e) {
        if (!isDragging) return;

        const currentPos = isHorizontal ? e.clientX : e.clientY;
        const delta = currentPos - startPos;
        const containerSize = getContainerSize();
        const newSize = startBeforeSize + delta;
        const newPercent = (newSize / containerSize) * 100;

        if (newSize >= minSize && newPercent <= maxSizePercent) {
            applyPosition(newPercent);
        }
    }

    function onMouseUp(e) {
        isDragging = false;
        document.body.style.cursor = '';
        document.body.style.userSelect = '';

        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);

        // Calculate final percent
        const containerSize = getContainerSize();
        const beforeSize = isHorizontal ? beforeEl.offsetWidth : beforeEl.offsetHeight;
        const percent = (beforeSize / containerSize) * 100;

        // Save to localStorage
        if (storageKey) {
            localStorage.setItem(storageKey, percent.toString());
        }

        // Notify C#
        if (dotnetRef) {
            dotnetRef.invokeMethodAsync('OnSplitterMoved', percent);
        }

        // Relayout Monaco editors
        if (typeof window.monacoLayoutAll === 'function') {
            window.monacoLayoutAll();
        }

        // Try to call layoutAll from imported module
        try {
            import('./monaco-interop.js').then(m => m.layoutAll()).catch(() => {});
        } catch (e) {
            // Ignore
        }
    }

    splitterEl.addEventListener('mousedown', onMouseDown);

    // Store cleanup info
    splitters.set(splitterElementId, {
        mouseDownHandler: onMouseDown,
        splitterEl
    });
}

export function destroySplitter(splitterElementId) {
    const entry = splitters.get(splitterElementId);
    if (entry) {
        entry.splitterEl.removeEventListener('mousedown', entry.mouseDownHandler);
        splitters.delete(splitterElementId);
    }
}
