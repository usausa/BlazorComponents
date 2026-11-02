const hasFiles = (e) => Array.from(e.dataTransfer?.types ?? []).includes("Files");

const findInput = (element) => element.querySelector("input[type=file]");

const collect = (data) => {
    if (!data.items) {
        return Array.from(data.files);
    }
    return Array.from(data.items)
        .filter((item) => (item.kind === "file") && !item.webkitGetAsEntry?.()?.isDirectory)
        .map((item) => item.getAsFile())
        .filter((file) => file !== null);
};

const matchesToken = (name, type, token) => {
    if (token.startsWith(".")) {
        return name.endsWith(token);
    }
    if (token.endsWith("/*")) {
        return type.startsWith(token.slice(0, -1));
    }
    return type === token;
};

export const matches = (file, accept) => {
    const tokens = accept.split(",").map((token) => token.trim().toLowerCase()).filter((token) => token.length > 0);
    const name = file.name.toLowerCase();
    const type = file.type.toLowerCase();
    return (tokens.length === 0) || tokens.some((token) => matchesToken(name, type, token));
};

const dispatch = (input, data) => {
    if (!input || input.disabled) {
        return false;
    }
    let files = collect(data).filter((file) => matches(file, input.accept));
    if (!input.multiple) {
        files = files.slice(0, 1);
    }
    if (files.length === 0) {
        return false;
    }
    const transfer = new DataTransfer();
    files.forEach((file) => transfer.items.add(file));
    input.files = transfer.files;
    input.dispatchEvent(new Event("change", { bubbles: true }));
    return true;
};

export const observe = (element, dragOverClass, paste, target) => {
    let options = { dragOverClass, paste };
    let depth = 0;

    const highlight = (on) => {
        if (options.dragOverClass) {
            element.classList.toggle(options.dragOverClass, on);
        }
    };
    const onDragEnter = (e) => {
        if (!hasFiles(e)) {
            return;
        }
        e.preventDefault();
        depth++;
        highlight(target.accepting());
    };
    const onDragOver = (e) => {
        if (!hasFiles(e)) {
            return;
        }
        e.preventDefault();
        e.dataTransfer.dropEffect = target.accepting() ? "copy" : "none";
    };
    const onDragLeave = (e) => {
        if (!hasFiles(e)) {
            return;
        }
        depth = Math.max(depth - 1, 0);
        if (depth === 0) {
            highlight(false);
        }
    };
    const onDrop = (e) => {
        if (!hasFiles(e)) {
            return;
        }
        depth = 0;
        highlight(false);
        if (e.defaultPrevented) {
            return;
        }
        e.preventDefault();
        if (target.accepting()) {
            target.receive(e.dataTransfer);
        }
    };
    const onPaste = (e) => {
        if (!options.paste || e.defaultPrevented || !e.clipboardData || !target.accepting()) {
            return;
        }
        if (target.receive(e.clipboardData)) {
            e.preventDefault();
        }
    };
    const handlers = [
        ["dragenter", onDragEnter],
        ["dragover", onDragOver],
        ["dragleave", onDragLeave],
        ["drop", onDrop],
        ["paste", onPaste]
    ];
    handlers.forEach(([type, handler]) => element.addEventListener(type, handler));

    return {
        update: (newDragOverClass, newPaste) => {
            highlight(false);
            options = { dragOverClass: newDragOverClass, paste: newPaste };
        },
        dispose: () => {
            handlers.forEach(([type, handler]) => element.removeEventListener(type, handler));
            highlight(false);
        }
    };
};

export function attach(element, dragOverClass, paste) {
    return observe(element, dragOverClass, paste, {
        accepting: () => {
            const input = findInput(element);
            return (input !== null) && !input.disabled;
        },
        receive: (data) => dispatch(findInput(element), data)
    });
}

export function open(element) {
    findInput(element)?.click();
}
