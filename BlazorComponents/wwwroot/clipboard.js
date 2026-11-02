export function copy(text) {
    const active = document.activeElement;
    const selection = document.getSelection();
    const ranges = [];
    for (let i = 0; i < (selection?.rangeCount ?? 0); i++) {
        ranges.push(selection.getRangeAt(i));
    }

    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.readOnly = true;
    textarea.style.position = "fixed";
    textarea.style.top = "0";
    textarea.style.left = "0";
    textarea.style.opacity = "0";
    document.body.append(textarea);
    textarea.select();

    let copied;
    try {
        copied = document.execCommand("copy");
    } catch {
        copied = false;
    }

    textarea.remove();
    if (selection) {
        selection.removeAllRanges();
        for (const range of ranges) {
            selection.addRange(range);
        }
    }
    if (active instanceof HTMLElement) {
        active.focus({ preventScroll: true });
    }
    return copied;
}
