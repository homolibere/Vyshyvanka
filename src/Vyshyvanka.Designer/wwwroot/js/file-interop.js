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
