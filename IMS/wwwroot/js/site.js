window.downloadFileFromBytes = (fileName, bytesBase64) => {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = "data:application/octet-stream;base64," + bytesBase64;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

/** Blazor Server: streams PDF bytes in chunks (avoids SignalR single-message size limits). */
window.downloadFileFromStream = async (fileName, contentStreamReference) => {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);
    try {
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } finally {
        URL.revokeObjectURL(url);
    }
};

/** Same-origin GET with cookies; avoids Blazor JS interop limits on PDF bytes. */
window.downloadFileFromAuthenticatedUrl = async (fileName, url) => {
    const resp = await fetch(url, { method: 'GET', credentials: 'same-origin' });
    if (!resp.ok) {
        throw new Error(`Download failed (${resp.status}) for ${url}`);
    }
    const blob = await resp.blob();
    const objUrl = URL.createObjectURL(blob);
    try {
        const a = document.createElement('a');
        a.href = objUrl;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    } finally {
        URL.revokeObjectURL(objUrl);
    }
};




