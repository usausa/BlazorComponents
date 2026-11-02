import { matches, observe } from "./file-drop-zone.js";

const chunkLimit = 16 * 1024;
const reportInterval = 200;
const errorLimit = 100;

const encoder = new TextEncoder();

const takeChunk = (values, index) => {
    const chunk = [];
    let size = 0;
    for (let i = index; i < values.length; i++) {
        const length = encoder.encode(JSON.stringify(values[i])).length;
        if ((chunk.length > 0) && (size + length > chunkLimit)) {
            break;
        }
        chunk.push(values[i]);
        size += length;
    }
    return chunk;
};

const readEntries = (reader) => new Promise((resolve, reject) => reader.readEntries(resolve, reject));

const readFile = (entry) => new Promise((resolve, reject) => entry.file(resolve, reject));

const trimPath = (path) => (path.startsWith("/") ? path.slice(1) : path);

const walk = async (entry) => {
    if (entry.isFile) {
        return [{ file: await readFile(entry), path: trimPath(entry.fullPath) }];
    }
    const reader = entry.createReader();
    const children = [];
    for (let batch = await readEntries(reader); batch.length > 0; batch = await readEntries(reader)) {
        children.push(...batch);
    }
    return (await Promise.all(children.map(walk))).flat();
};

const fromFiles = (files) => Array.from(files, (file) => ({ file, path: file.webkitRelativePath || file.name }));

const fromData = (data, allowFolders) => {
    if (!data.items) {
        return Promise.resolve(fromFiles(data.files));
    }
    const lists = [];
    for (const item of Array.from(data.items)) {
        if (item.kind !== "file") {
            continue;
        }
        const entry = item.webkitGetAsEntry?.();
        if (entry?.isDirectory) {
            if (allowFolders) {
                lists.push(walk(entry));
            }
            continue;
        }
        const file = item.getAsFile();
        if (file !== null) {
            lists.push([{ file, path: file.name }]);
        }
    }
    return Promise.all(lists).then((values) => values.flat());
};

const hasFileItems = (data) => (data.items ? Array.from(data.items).some((item) => item.kind === "file") : (data.files.length > 0));

const summarize = (items) => {
    const entries = new Map();
    for (const { file, path } of items) {
        const index = path.indexOf("/");
        const name = (index < 0) ? path : path.slice(0, index);
        let entry = entries.get(name);
        if (!entry) {
            entry = { name, isFolder: index >= 0, size: 0 };
            entries.set(name, entry);
        }
        entry.size += file.size;
    }
    return Array.from(entries.values());
};

const send = (session, item, url, fieldName, headers, progress) => new Promise((resolve) => {
    const request = new XMLHttpRequest();
    request.upload.addEventListener("progress", (e) => {
        if (e.lengthComputable && (e.total > 0)) {
            progress(item.file.size * e.loaded / e.total);
        }
    });
    request.addEventListener("loadend", () => {
        session.requests.delete(request);
        resolve(request.status);
    });
    try {
        request.open("POST", url);
        Object.entries(headers).forEach(([name, value]) => request.setRequestHeader(name, value));
        const data = new FormData();
        data.append("relativePath", item.path);
        data.append(fieldName, item.file, item.file.name);
        request.send(data);
        session.requests.add(request);
    } catch (error) {
        console.error(error);
        resolve(0);
    }
});

const upload = async (invoke, session, items, totalBytes, [url, fieldName, concurrency, headers]) => {
    let next = 0;
    let completed = 0;
    let succeeded = 0;
    let failed = 0;
    let loaded = 0;
    let currentPath = null;
    let reported = "";
    let reporting = null;

    const report = () => {
        const values = [completed, items.length, Math.round(loaded), totalBytes, currentPath];
        const key = values.join();
        if (reporting || (key === reported)) {
            return;
        }
        reported = key;
        reporting = invoke("NotifyProgress", ...values)
            .catch((error) => console.error(error))
            .finally(() => {
                reporting = null;
            });
    };
    const work = async () => {
        while (!session.canceled && (next < items.length)) {
            const item = items[next++];
            currentPath = item.path;
            let sent = 0;
            const status = await send(session, item, url, fieldName, headers, (value) => {
                loaded += value - sent;
                sent = value;
            });
            if ((status === 0) && session.canceled) {
                break;
            }
            loaded += item.file.size - sent;
            completed++;
            if ((status >= 200) && (status < 300)) {
                succeeded++;
            } else {
                failed++;
                if (session.errors.length < errorLimit) {
                    session.errors.push({ path: item.path, status });
                }
            }
        }
    };

    const timer = setInterval(report, reportInterval);
    try {
        const workers = Array.from({ length: Math.min(concurrency, items.length) }, work);
        report();
        await Promise.all(workers);
    } finally {
        clearInterval(timer);
        await reporting;
        report();
        await reporting;
        await invoke("NotifyCompleted", succeeded, failed, session.canceled, session.errors.length);
    }
};

export function attach(dotNetRef, element, dragOverClass, paste, accept, multiple, allowFolders, disabled) {
    let options = { accept, multiple, allowFolders, disabled };
    let session = null;
    let disposed = false;

    const invoke = (method, ...values) => (disposed ? Promise.resolve(null) : dotNetRef.invokeMethodAsync(method, ...values));
    const accepting = () => !disposed && !options.disabled && (session === null);

    const run = async (collecting) => {
        const current = { entries: [], errors: [], requests: new Set(), canceled: false };
        session = current;
        try {
            let items = (await collecting).filter((item) => matches(item.file, options.accept));
            if (!options.multiple) {
                items = items.slice(0, 1);
            }
            if ((items.length === 0) || current.canceled) {
                return;
            }
            current.entries = summarize(items);
            const totalBytes = items.reduce((sum, item) => sum + item.file.size, 0);
            const start = await invoke("NotifyStarting", current.entries.length, items.length, totalBytes);
            if (start) {
                await upload(invoke, current, items, totalBytes, start);
            }
        } finally {
            session = null;
        }
    };
    const cancel = () => {
        if (session) {
            session.canceled = true;
            Array.from(session.requests).forEach((request) => request.abort());
        }
    };
    const createInput = (directory) => {
        const input = document.createElement("input");
        input.type = "file";
        input.webkitdirectory = directory;
        input.addEventListener("change", () => {
            const files = Array.from(input.files ?? []);
            input.value = "";
            if ((files.length > 0) && accepting()) {
                run(Promise.resolve(fromFiles(files)));
            }
        });
        return input;
    };
    const fileInput = createInput(false);
    const folderInput = createInput(true);

    const observer = observe(element, dragOverClass, paste, {
        accepting,
        receive: (data) => {
            if (!hasFileItems(data)) {
                return false;
            }
            run(fromData(data, options.allowFolders));
            return true;
        }
    });

    return {
        update: (newDragOverClass, newPaste, newAccept, newMultiple, newAllowFolders, newDisabled) => {
            observer.update(newDragOverClass, newPaste);
            options = { accept: newAccept, multiple: newMultiple, allowFolders: newAllowFolders, disabled: newDisabled };
        },
        open: (directory) => {
            if (!accepting() || (directory && !options.allowFolders)) {
                return;
            }
            if (directory) {
                folderInput.click();
                return;
            }
            fileInput.accept = options.accept;
            fileInput.multiple = options.multiple;
            fileInput.click();
        },
        cancel,
        entries: (index) => takeChunk(session?.entries ?? [], index),
        errors: (index) => takeChunk(session?.errors ?? [], index),
        dispose: () => {
            disposed = true;
            observer.dispose();
            cancel();
        }
    };
}
