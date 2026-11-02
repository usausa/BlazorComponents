const isEditable = (e) => {
    const target = e.composedPath()[0];
    return (target instanceof HTMLElement) && (target.isContentEditable || target.matches("input, textarea, select"));
};

const createOptions = (key, byCode, ctrl, alt, shift, meta, preventDefault, allowInInput, allowRepeat, disabled) =>
    ({ key, byCode, ctrl, alt, shift, meta, preventDefault, allowInInput, allowRepeat, disabled });

export function register(dotNetRef, ...args) {
    let options = createOptions(...args);
    const handler = (e) => {
        if (options.disabled || e.isComposing || (e.keyCode === 229)) {
            return;
        }
        if (e.repeat && !options.allowRepeat) {
            return;
        }
        const pressed = options.byCode ? e.code : e.key;
        if ((typeof pressed !== "string") || (pressed.toUpperCase() !== options.key)) {
            return;
        }
        if ((e.ctrlKey !== options.ctrl) || (e.altKey !== options.alt) || ((options.shift !== null) && (e.shiftKey !== options.shift)) || (e.metaKey !== options.meta)) {
            return;
        }
        if (!options.allowInInput && isEditable(e)) {
            return;
        }
        if (options.preventDefault) {
            e.preventDefault();
        }
        dotNetRef.invokeMethodAsync("NotifyPressed");
    };
    document.addEventListener("keydown", handler);

    return {
        update: (...values) => {
            options = createOptions(...values);
        },
        dispose: () => document.removeEventListener("keydown", handler)
    };
}
