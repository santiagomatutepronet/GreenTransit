// Descarga un archivo desde bytes en Base64
window.downloadBase64File = function (base64, mimeType, fileName) {
    const link = document.createElement('a');
    link.href = `data:${mimeType};base64,${base64}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// Descarga un archivo de texto plano (JSON, CSV, etc.) desde una cadena de texto
window.saveAsTextFile = function (fileName, content, mimeType) {
    const blob = new Blob([content], { type: mimeType || 'application/octet-stream' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

// Exporta a PDF una captura del elemento identificado por containerId.
// fileName: nombre del archivo resultante (sin extensión).
window.exportPageToPdf = async function (containerId, fileName) {
    const el = document.getElementById(containerId);
    if (!el) {
        console.warn('exportPageToPdf: elemento no encontrado:', containerId);
        return;
    }

    const canvas = await html2canvas(el, {
        scale: 2,
        useCORS: true,
        logging: false,
        backgroundColor: '#ffffff'
    });

    const imgData = canvas.toDataURL('image/png');
    const { jsPDF } = window.jspdf;

    // Orientación landscape si el ancho supera al alto
    const orientation = canvas.width > canvas.height ? 'l' : 'p';
    const pdf = new jsPDF({ orientation: orientation, unit: 'px', format: 'a4' });

    const pageWidth  = pdf.internal.pageSize.getWidth();
    const pageHeight = pdf.internal.pageSize.getHeight();
    const imgRatio   = canvas.width / canvas.height;
    const pdfRatio   = pageWidth / pageHeight;

    let imgW, imgH;
    if (imgRatio > pdfRatio) {
        imgW = pageWidth;
        imgH = pageWidth / imgRatio;
    } else {
        imgH = pageHeight;
        imgW = pageHeight * imgRatio;
    }

    const offsetX = (pageWidth  - imgW) / 2;
    const offsetY = (pageHeight - imgH) / 2;

    pdf.addImage(imgData, 'PNG', offsetX, offsetY, imgW, imgH);
    pdf.save((fileName || 'exportacion') + '.pdf');
};
