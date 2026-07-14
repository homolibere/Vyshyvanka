/**
 * Triggers a browser file download from a string content.
 * @param {string} filename - The name of the file to download.
 * @param {string} content - The file content as a string.
 * @param {string} mimeType - The MIME type (e.g. "application/json").
 */
window.downloadFile = function (filename, content, mimeType) {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

/**
 * Opens a file picker dialog and returns the selected file's text content.
 * @param {string} accept - Accepted file types (e.g. ".json").
 * @returns {Promise<string|null>} The file content as a string, or null if cancelled.
 */
window.triggerFileUpload = function (accept) {
    return new Promise((resolve) => {
        const input = document.createElement("input");
        input.type = "file";
        input.accept = accept;
        input.style.display = "none";

        input.addEventListener("change", () => {
            const file = input.files[0];
            if (!file) {
                resolve(null);
                return;
            }
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => resolve(null);
            reader.readAsText(file);
        });

        // Handle cancel (focus returns to window without change event)
        input.addEventListener("cancel", () => resolve(null));

        document.body.appendChild(input);
        input.click();
        document.body.removeChild(input);
    });
};
