// Drag-and-drop interop for file upload zones
export function initDropZone(dropZoneId, inputFileId) {
    const dropZone = document.getElementById(dropZoneId);
    const inputFile = document.getElementById(inputFileId);
    if (!dropZone || !inputFile) return;

    ['dragenter', 'dragover'].forEach(evt => {
        dropZone.addEventListener(evt, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.add('drag-over');
        });
    });

    ['dragleave', 'drop'].forEach(evt => {
        dropZone.addEventListener(evt, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.remove('drag-over');
        });
    });

    dropZone.addEventListener('drop', (e) => {
        const dt = e.dataTransfer;
        if (dt.files.length > 0) {
            inputFile.files = dt.files;
            inputFile.dispatchEvent(new Event('change', { bubbles: true }));
        }
    });
}

export function destroyDropZone(dropZoneId) {
    // Element removal handles cleanup
}
