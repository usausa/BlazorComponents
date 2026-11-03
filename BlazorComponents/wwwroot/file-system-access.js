const handles = new Map();
let lastHandle = 0;

export async function pick(fileName, contentType) {
    const handle = await showPicker(fileName, contentType);
    if (handle === null) {
        return 0;
    }
    if (handle === undefined) {
        return -1;
    }

    lastHandle++;
    handles.set(lastHandle, handle);
    return lastHandle;
}

export async function write(id, streamReference) {
    const handle = handles.get(id);
    handles.delete(id);
    const stream = await streamReference.stream();
    await stream.pipeTo(await handle.createWritable());
}

export async function download(fileName, contentType, streamReference) {
    const blob = await new Response(await streamReference.stream()).blob();
    const url = URL.createObjectURL(blob.slice(0, blob.size, contentType));
    click(url, fileName);
    setTimeout(() => URL.revokeObjectURL(url), 0);
}

export async function saveUrl(fileName, contentType, url) {
    const handle = await showPicker(fileName, contentType);
    if (handle === null) {
        return "canceled";
    }
    if (handle === undefined) {
        click(url, fileName);
        return "downloaded";
    }

    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`Failed to fetch ${url}: ${response.status}`);
    }
    await response.body.pipeTo(await handle.createWritable());
    return "saved";
}

async function showPicker(fileName, contentType) {
    if (typeof window.showSaveFilePicker !== "function") {
        return undefined;
    }

    try {
        return await window.showSaveFilePicker(createOptions(fileName, contentType));
    } catch (error) {
        return error?.name === "AbortError" ? null : undefined;
    }
}

function createOptions(fileName, contentType) {
    const options = { suggestedName: fileName };
    const index = fileName.lastIndexOf(".");
    if ((index > 0) && (index < fileName.length - 1) && contentType) {
        const extension = fileName.substring(index).toLowerCase();
        options.types = [{ description: extension.substring(1).toUpperCase(), accept: { [contentType]: [extension] } }];
    }
    return options;
}

function click(url, fileName) {
    const link = document.createElement("a");
    link.href = url;
    link.target = "_self";
    link.download = fileName;
    link.click();
    link.remove();
}
