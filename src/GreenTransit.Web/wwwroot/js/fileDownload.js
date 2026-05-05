// Descarga un archivo desde bytes en Base64
window.downloadBase64File = function (base64, mimeType, fileName) {
    const link = document.createElement('a');
    link.href = `data:${mimeType};base64,${base64}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
