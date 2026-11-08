const zones = new Map();
const deferred = new Map();
const interactive = "input, textarea, select, option, [contenteditable=\"\"], [contenteditable=\"true\"]";
const mouseThreshold = 4;
const touchTolerance = 8;
const scrollEdge = 40;
const scrollSpeed = 20;

let pending = null;
let session = null;
let live = null;
let locked = null;

const preventDefault = (e) => e.preventDefault();

const classes = (value) => (value ?? "").split(/\s+/).filter((name) => name.length > 0);

const format = (text, position, count, name) =>
    (text ?? "").replaceAll("{0}", String(position)).replaceAll("{1}", String(count)).replaceAll("{2}", name);

const announce = (text) => {
    if (!text) {
        return;
    }
    if (!live) {
        live = document.createElement("div");
        live.setAttribute("aria-live", "assertive");
        live.setAttribute("aria-atomic", "true");
        Object.assign(live.style, {
            position: "fixed",
            width: "1px",
            height: "1px",
            margin: "-1px",
            padding: "0",
            border: "0",
            overflow: "hidden",
            clip: "rect(0 0 0 0)",
            whiteSpace: "nowrap"
        });
        document.body.appendChild(live);
    }
    live.textContent = "";
    requestAnimationFrame(() => {
        if (live) {
            live.textContent = text;
        }
    });
};

const lockPage = (touch) => {
    const style = document.body.style;
    if (!locked) {
        locked = {
            userSelect: style.userSelect,
            webkitUserSelect: style.webkitUserSelect,
            webkitTouchCallout: style.webkitTouchCallout,
            cursor: style.cursor
        };
    }
    style.userSelect = "none";
    style.webkitUserSelect = "none";
    if (touch) {
        style.webkitTouchCallout = "none";
    }
};

const unlockPage = () => {
    if (locked) {
        Object.assign(document.body.style, locked);
        locked = null;
    }
};

const flush = (element) => {
    const entry = deferred.get(element);
    if (entry) {
        deferred.delete(element);
        clearTimeout(entry.timer);
        entry.apply();
    }
};

const flushAll = () => {
    for (const element of Array.from(deferred.keys())) {
        flush(element);
    }
};

const defer = (element, apply, delay) => {
    flush(element);
    deferred.set(element, { apply, timer: setTimeout(() => flush(element), delay) });
};

const translateOf = (element) => {
    const value = getComputedStyle(element).translate;
    if (!value || (value === "none")) {
        return { x: 0, y: 0 };
    }
    const [x = 0, y = 0] = value.split(/\s+/).map((part) => parseFloat(part) || 0);
    return { x, y };
};

const baseRect = (element) => {
    const rect = element.getBoundingClientRect();
    const { x, y } = translateOf(element);
    return {
        left: rect.left - x,
        top: rect.top - y,
        right: rect.right - x,
        bottom: rect.bottom - y,
        width: rect.width,
        height: rect.height
    };
};

const contains = (rect, x, y) => (x >= rect.left) && (x <= rect.right) && (y >= rect.top) && (y <= rect.bottom);

const scrolls = (overflow) => (overflow === "auto") || (overflow === "scroll") || (overflow === "overlay");

const clamp = (value, min, max) => Math.min(Math.max(value, min), max);

const intersect = (a, b) => ({
    left: Math.max(a.left, b.left),
    top: Math.max(a.top, b.top),
    right: Math.min(a.right, b.right),
    bottom: Math.min(a.bottom, b.bottom)
});

const visibleRect = (zone) => {
    let rect = { left: 0, top: 0, right: window.innerWidth, bottom: window.innerHeight };
    for (let element = zone.element; element && (element !== document.body) && (element !== document.documentElement); element = element.parentElement) {
        const style = getComputedStyle(element);
        if ((style.overflowX !== "visible") || (style.overflowY !== "visible")) {
            const box = element.getBoundingClientRect();
            const left = box.left + element.clientLeft;
            const top = box.top + element.clientTop;
            rect = intersect(rect, { left, top, right: left + element.clientWidth, bottom: top + element.clientHeight });
        }
    }
    return rect;
};

const itemsOf = (zone) => Array.from(zone.element.children);

const itemOf = (zone, target) => {
    let node = target instanceof Element ? target : null;
    while (node && (node.parentElement !== zone.element)) {
        node = node.parentElement;
    }
    return node;
};

const draggable = (zone, item, target, keyboard) => {
    const options = zone.options;
    if (options.disabled || !item || (options.filter && item.matches(options.filter))) {
        return false;
    }
    if (keyboard) {
        return true;
    }
    if (target.closest(interactive)) {
        return false;
    }
    if (!options.handle) {
        return true;
    }
    const handle = target.closest(options.handle);
    return (handle !== null) && item.contains(handle);
};

const accepts = (s, zone) =>
    !zone.options.disabled &&
    !s.item.contains(zone.element) &&
    ((zone === s.source) || (!!s.source.options.group && (zone.options.group === s.source.options.group)));

const candidates = (s) =>
    Array.from(zones.values())
        .filter((zone) => accepts(s, zone))
        .sort((a, b) => ((a.element.compareDocumentPosition(b.element) & Node.DOCUMENT_POSITION_FOLLOWING) ? -1 : 1));

const save = (s, element) => {
    if (!s.saved.has(element)) {
        s.saved.set(element, {
            translate: element.style.translate,
            transition: element.style.transition,
            visibility: element.style.visibility
        });
    }
};

const setOffset = (s, element, x, y) => {
    const current = s.offsets.get(element) ?? { x: 0, y: 0 };
    if ((current.x === x) && (current.y === y)) {
        return;
    }
    save(s, element);
    if (!s.animated.has(element)) {
        s.animated.add(element);
        element.style.transition = (s.animation > 0) ? `translate ${s.animation}ms ease` : "none";
    }
    s.offsets.set(element, { x, y });
    element.style.translate = ((x === 0) && (y === 0)) ? s.saved.get(element).translate : `${x}px ${y}px`;
};

const gapOf = (s, zone) => {
    if (s.gaps.has(zone)) {
        return s.gaps.get(zone);
    }
    const horizontal = zone.options.horizontal;
    const style = getComputedStyle(zone.element);
    let gap = parseFloat(horizontal ? style.columnGap : style.rowGap);
    if (!Number.isFinite(gap)) {
        const items = itemsOf(zone);
        if (items.length >= 2) {
            const first = baseRect(items[0]);
            const second = baseRect(items[1]);
            gap = horizontal ? (second.left - first.right) : (second.top - first.bottom);
        }
    }
    gap = Number.isFinite(gap) ? Math.max(gap, 0) : 0;
    s.gaps.set(zone, gap);
    return gap;
};

const sizeOf = (s, zone) => (zone.options.horizontal ? s.rect.width : s.rect.height) + gapOf(s, zone);

const remainingOf = (s, zone) => itemsOf(zone).filter((element) => element !== s.item);

const closedRect = (s, zone, element, index) => {
    const rect = baseRect(element);
    if ((zone !== s.source) || (index < s.index)) {
        return rect;
    }
    const size = sizeOf(s, zone);
    return zone.options.horizontal
        ? { ...rect, left: rect.left - size, right: rect.right - size }
        : { ...rect, top: rect.top - size, bottom: rect.bottom - size };
};

const zoneAt = (s) => {
    for (const element of document.elementsFromPoint(s.x, s.y)) {
        const zone = zones.get(element);
        if (zone && accepts(s, zone)) {
            return zone;
        }
    }
    return null;
};

const indexAt = (s, zone, x, y) => {
    if (!zone.options.sort) {
        return (zone === s.source) ? s.index : remainingOf(s, zone).length;
    }
    const remaining = remainingOf(s, zone);
    const horizontal = zone.options.horizontal;
    const point = horizontal ? x : y;
    for (let i = 0; i < remaining.length; i++) {
        const rect = closedRect(s, zone, remaining[i], i);
        const center = horizontal ? ((rect.left + rect.right) / 2) : ((rect.top + rect.bottom) / 2);
        if (point < center) {
            return i;
        }
    }
    return remaining.length;
};

const slotOf = (s) => {
    const zone = s.target;
    const horizontal = zone.options.horizontal;
    const remaining = remainingOf(s, zone);
    if (s.targetIndex < remaining.length) {
        const rect = closedRect(s, zone, remaining[s.targetIndex], s.targetIndex);
        return horizontal
            ? { left: rect.left, top: rect.top, width: s.rect.width, height: rect.height }
            : { left: rect.left, top: rect.top, width: rect.width, height: s.rect.height };
    }
    if (remaining.length > 0) {
        const last = remaining.length - 1;
        const rect = closedRect(s, zone, remaining[last], last);
        const gap = gapOf(s, zone);
        return horizontal
            ? { left: rect.right + gap, top: rect.top, width: s.rect.width, height: rect.height }
            : { left: rect.left, top: rect.bottom + gap, width: rect.width, height: s.rect.height };
    }
    const box = zone.element.getBoundingClientRect();
    const style = getComputedStyle(zone.element);
    const left = box.left + parseFloat(style.borderLeftWidth) + parseFloat(style.paddingLeft);
    const top = box.top + parseFloat(style.borderTopWidth) + parseFloat(style.paddingTop);
    const width = box.right - parseFloat(style.borderRightWidth) - parseFloat(style.paddingRight) - left;
    const height = box.bottom - parseFloat(style.borderBottomWidth) - parseFloat(style.paddingBottom) - top;
    return horizontal
        ? { left, top, width: s.rect.width, height: Math.max(height, s.rect.height) }
        : { left, top, width: Math.max(width, 0), height: s.rect.height };
};

const unpad = (s) => {
    if (s.padded) {
        s.padded.zone.element.style[s.padded.property] = s.padded.value;
        s.padded = null;
    }
};

const pad = (s, zone) => {
    const target = (zone === s.source) ? null : zone;
    if ((s.padded?.zone ?? null) === target) {
        return;
    }
    unpad(s);
    if (!target) {
        return;
    }
    const property = target.options.horizontal ? "paddingRight" : "paddingBottom";
    const current = parseFloat(getComputedStyle(target.element)[property]) || 0;
    s.padded = { zone: target, property, value: target.element.style[property] };
    target.element.style[property] = `${current + sizeOf(s, target)}px`;
};

const highlight = (s, zone) => {
    if (s.over === zone) {
        return;
    }
    if (s.over) {
        s.over.element.classList.remove(...classes(s.over.options.overClass));
    }
    s.over = zone;
    if (zone) {
        zone.element.classList.add(...classes(zone.options.overClass));
    }
};

const locate = (s) => {
    const zone = zoneAt(s);
    if (zone) {
        s.target = zone;
        s.targetIndex = indexAt(s, zone, s.x, s.y);
        return;
    }
    const view = intersect(s.target.element.getBoundingClientRect(), visibleRect(s.target));
    const near = { left: view.left - scrollEdge, top: view.top - scrollEdge, right: view.right + scrollEdge, bottom: view.bottom + scrollEdge };
    if ((view.right > view.left) && (view.bottom > view.top) && contains(near, s.x, s.y)) {
        s.targetIndex = indexAt(s, s.target, clamp(s.x, view.left, view.right), clamp(s.y, view.top, view.bottom));
    }
};

const into = (start, end, min, max) => {
    if (start < min) {
        return start - min;
    }
    return (end > max) ? Math.min(end - max, start - min) : 0;
};

const reveal = (s, slot) => {
    let rect = { left: slot.left, top: slot.top, right: slot.left + slot.width, bottom: slot.top + slot.height };
    for (let element = s.target.element; element && (element !== document.body) && (element !== document.documentElement); element = element.parentElement) {
        const style = getComputedStyle(element);
        const box = element.getBoundingClientRect();
        const left = element.scrollLeft;
        const top = element.scrollTop;
        if (scrolls(style.overflowY) && (element.scrollHeight > element.clientHeight)) {
            const min = box.top + element.clientTop;
            element.scrollTop += into(rect.top, rect.bottom, min, min + element.clientHeight);
        }
        if (scrolls(style.overflowX) && (element.scrollWidth > element.clientWidth)) {
            const min = box.left + element.clientLeft;
            element.scrollLeft += into(rect.left, rect.right, min, min + element.clientWidth);
        }
        const dx = element.scrollLeft - left;
        const dy = element.scrollTop - top;
        rect = { left: rect.left - dx, top: rect.top - dy, right: rect.right - dx, bottom: rect.bottom - dy };
    }
    window.scrollBy(into(rect.left, rect.right, 0, window.innerWidth), into(rect.top, rect.bottom, 0, window.innerHeight));
};

const layout = (s) => {
    if (s.mode !== "keyboard") {
        locate(s);
    }
    highlight(s, s.target);
    pad(s, s.target);
    for (const zone of candidates(s)) {
        const horizontal = zone.options.horizontal;
        remainingOf(s, zone).forEach((element, index) => {
            let offset = 0;
            if ((zone === s.source) && (index >= s.index)) {
                offset -= sizeOf(s, zone);
            }
            if ((zone === s.target) && (index >= s.targetIndex)) {
                offset += sizeOf(s, zone);
            }
            setOffset(s, element, horizontal ? offset : 0, horizontal ? 0 : offset);
        });
    }
    const slot = slotOf(s);
    const view = visibleRect(s.target);
    const clip = [view.top - slot.top, (slot.left + slot.width) - view.right, (slot.top + slot.height) - view.bottom, view.left - slot.left].map((value) => Math.max(value, 0));
    const hidden = ((clip[0] + clip[2]) >= slot.height) || ((clip[1] + clip[3]) >= slot.width);
    Object.assign(s.placeholder.style, {
        left: `${slot.left}px`,
        top: `${slot.top}px`,
        width: `${slot.width}px`,
        height: `${slot.height}px`,
        clipPath: hidden ? "inset(50%)" : `inset(${clip.map((value) => `${value}px`).join(" ")})`
    });
    if (s.mode === "keyboard") {
        s.ghost.style.translate = `${slot.left - s.rect.left}px ${slot.top - s.rect.top}px`;
        if (s.reveal) {
            s.reveal = false;
            reveal(s, slot);
        }
    } else {
        s.ghost.style.translate = `${s.x - s.startX}px ${s.y - s.startY}px`;
    }
};

const speedOf = (distance) => ((distance < scrollEdge) ? Math.ceil(((scrollEdge - Math.max(distance, 0)) / scrollEdge) * scrollSpeed) : 0);

const scrollBy = (s, element, root) => {
    const rect = root ? { left: 0, top: 0, right: window.innerWidth, bottom: window.innerHeight } : element.getBoundingClientRect();
    if (!contains(rect, s.x, s.y)) {
        return false;
    }
    const style = root ? null : getComputedStyle(element);
    const left = element.scrollLeft;
    const top = element.scrollTop;
    if ((root || scrolls(style.overflowY)) && (element.scrollHeight > element.clientHeight)) {
        element.scrollTop += speedOf(rect.bottom - s.y) - speedOf(s.y - rect.top);
    }
    if ((root || scrolls(style.overflowX)) && (element.scrollWidth > element.clientWidth)) {
        element.scrollLeft += speedOf(rect.right - s.x) - speedOf(s.x - rect.left);
    }
    return (element.scrollTop !== top) || (element.scrollLeft !== left);
};

const autoScroll = (s) => {
    for (let element = s.target.element; element && (element !== document.body) && (element !== document.documentElement); element = element.parentElement) {
        if (scrollBy(s, element, false)) {
            return true;
        }
    }
    return scrollBy(s, document.scrollingElement ?? document.documentElement, true);
};

const tick = () => {
    const s = session;
    if (!s || s.ending) {
        return;
    }
    if ((s.mode !== "keyboard") && autoScroll(s)) {
        s.dirty = true;
    }
    if (s.dirty) {
        s.dirty = false;
        layout(s);
    }
    s.frame = requestAnimationFrame(tick);
};

const strip = (root) => {
    for (const element of [root, ...root.querySelectorAll("*")]) {
        for (const name of element.getAttributeNames()) {
            if (name.startsWith("_bl_") || ["id", "name", "form", "tabindex", "autofocus"].includes(name)) {
                element.removeAttribute(name);
            }
        }
    }
};

const createGhost = (item, rect, options) => {
    const ghost = document.createElement("div");
    ghost.setAttribute("aria-hidden", "true");
    ghost.inert = true;
    Object.assign(ghost.style, {
        position: "fixed",
        left: `${rect.left}px`,
        top: `${rect.top}px`,
        width: `${rect.width}px`,
        height: `${rect.height}px`,
        margin: "0",
        padding: "0",
        border: "0",
        pointerEvents: "none",
        zIndex: "10000",
        translate: "0px 0px",
        transition: "none"
    });
    const style = getComputedStyle(item);
    for (let i = 0; i < style.length; i++) {
        const name = style[i];
        if (name.startsWith("--")) {
            ghost.style.setProperty(name, style.getPropertyValue(name));
        }
    }
    const copy = item.cloneNode(true);
    strip(copy);
    Object.assign(copy.style, {
        width: "100%",
        height: "100%",
        margin: "0",
        boxSizing: "border-box",
        translate: "none",
        transition: "none",
        visibility: "visible"
    });
    ghost.appendChild(copy);
    const ghostClasses = classes(options.ghostClass);
    if (ghostClasses.length > 0) {
        copy.classList.add(...ghostClasses);
    } else {
        Object.assign(copy.style, {
            backgroundColor: style.backgroundColor,
            color: style.color,
            fontFamily: style.fontFamily,
            fontSize: style.fontSize,
            fontWeight: style.fontWeight,
            lineHeight: style.lineHeight,
            borderRadius: style.borderRadius,
            opacity: "0.9",
            boxShadow: "0 6px 16px rgba(0, 0, 0, 0.25)"
        });
    }
    document.body.appendChild(ghost);
    return ghost;
};

const createPlaceholder = (item, options, animation) => {
    const placeholder = document.createElement("div");
    placeholder.setAttribute("aria-hidden", "true");
    Object.assign(placeholder.style, {
        position: "fixed",
        margin: "0",
        boxSizing: "border-box",
        pointerEvents: "none",
        zIndex: "9999",
        transition: (animation > 0) ? ["left", "top", "width", "height"].map((name) => `${name} ${animation}ms ease`).join(", ") : "none"
    });
    const placeholderClasses = classes(options.placeholderClass);
    if (placeholderClasses.length > 0) {
        placeholder.classList.add(...placeholderClasses);
    } else {
        Object.assign(placeholder.style, {
            border: "2px dashed rgba(128, 128, 128, 0.6)",
            borderRadius: getComputedStyle(item).borderRadius,
            backgroundColor: "rgba(128, 128, 128, 0.08)"
        });
    }
    document.body.appendChild(placeholder);
    return placeholder;
};

const countOf = (s, zone) => remainingOf(s, zone).length + 1;

const announceMove = (s) => announce(format(s.source.options.announcements.moved, s.targetIndex + 1, countOf(s, s.target), s.target.options.name));

const moveBy = (s, step) => {
    if (!s.target.options.sort) {
        return;
    }
    const index = Math.min(Math.max(s.targetIndex + step, 0), remainingOf(s, s.target).length);
    if (index === s.targetIndex) {
        return;
    }
    s.targetIndex = index;
    s.dirty = true;
    s.reveal = true;
    announceMove(s);
};

const switchZone = (s, step) => {
    const list = candidates(s);
    const next = list[list.indexOf(s.target) + step];
    if (!next) {
        return;
    }
    s.target = next;
    const count = remainingOf(s, next).length;
    s.targetIndex = !next.options.sort ? ((next === s.source) ? s.index : count) : Math.min(s.targetIndex, count);
    s.dirty = true;
    s.reveal = true;
    announceMove(s);
};

const onSessionKeyDown = (e) => {
    const s = session;
    if (!s || s.ending) {
        return;
    }
    if (e.key === "Escape") {
        e.preventDefault();
        e.stopPropagation();
        finish(false);
        return;
    }
    if (s.mode !== "keyboard") {
        return;
    }
    const step = { ArrowUp: -1, ArrowDown: 1, ArrowLeft: -1, ArrowRight: 1 }[e.key];
    if (step !== undefined) {
        e.preventDefault();
        e.stopPropagation();
        const horizontal = s.target.options.horizontal;
        const along = horizontal ? ((e.key === "ArrowLeft") || (e.key === "ArrowRight")) : ((e.key === "ArrowUp") || (e.key === "ArrowDown"));
        if (along) {
            moveBy(s, step);
        } else {
            switchZone(s, step);
        }
        return;
    }
    if ((e.key === " ") || (e.key === "Enter")) {
        e.preventDefault();
        e.stopPropagation();
        finish(true);
        return;
    }
    if (e.key === "Tab") {
        e.preventDefault();
    }
};

const onSessionPointerDown = () => {
    if ((session?.mode === "keyboard") && !session.ending) {
        finish(false);
    }
};

const onSessionScroll = () => {
    if (session) {
        session.dirty = true;
    }
};

const onBlur = () => finish(false);

const listen = (add) => {
    const method = add ? "addEventListener" : "removeEventListener";
    window[method]("keydown", onSessionKeyDown, true);
    window[method]("pointerdown", onSessionPointerDown, true);
    window[method]("blur", onBlur);
    window[method]("scroll", onSessionScroll, { capture: true, passive: true });
    window[method]("resize", onSessionScroll);
};

const onMutation = (s, records) => {
    if (s.settled) {
        return;
    }
    if (s.ending) {
        if (records.some((record) => s.source.element.contains(record.target) || s.target.element.contains(record.target))) {
            settle(s, true);
        }
        return;
    }
    if (records.some((record) => (record.type === "childList") && zones.has(record.target))) {
        finish(false);
    }
};

const begin = (zone, item, mode, x, y) => {
    const items = itemsOf(zone);
    const index = items.indexOf(item);
    if (index < 0) {
        return;
    }
    flushAll();
    const options = zone.options;
    const rect = item.getBoundingClientRect();
    const s = {
        mode,
        source: zone,
        item,
        index,
        rect,
        startX: x,
        startY: y,
        x,
        y,
        target: zone,
        targetIndex: index,
        animation: options.animation,
        offsets: new Map(),
        saved: new Map(),
        animated: new Set(),
        gaps: new Map(),
        padded: null,
        over: null,
        frame: 0,
        dirty: true,
        reveal: false,
        ending: false,
        settled: false,
        commit: false,
        focused: document.activeElement === item,
        observer: null
    };
    session = s;
    lockPage(mode === "touch");
    if (mode !== "keyboard") {
        document.body.style.cursor = "grabbing";
    }
    s.ghost = createGhost(item, rect, options);
    if (mode === "keyboard") {
        s.ghost.style.transition = (s.animation > 0) ? `translate ${s.animation}ms ease` : "none";
    }
    s.placeholder = createPlaceholder(item, options, s.animation);
    save(s, item);
    item.style.visibility = "hidden";
    s.observer = new MutationObserver((records) => onMutation(s, records));
    for (const candidate of candidates(s)) {
        s.observer.observe(candidate.element, { childList: true, subtree: true, characterData: true });
    }
    listen(true);
    layout(s);
    s.dirty = false;
    s.frame = requestAnimationFrame(tick);
    if (mode === "keyboard") {
        announce(format(options.announcements.grabbed, index + 1, items.length, options.name));
    }
};

const flipFrom = (element, from, duration) => {
    const to = element.getBoundingClientRect();
    const dx = from.left - to.left;
    const dy = from.top - to.top;
    if ((Math.abs(dx) < 1) && (Math.abs(dy) < 1)) {
        return;
    }
    flush(element);
    const transition = element.style.transition;
    const translate = element.style.translate;
    element.style.transition = "none";
    element.style.translate = `${dx}px ${dy}px`;
    void element.offsetWidth;
    element.style.transition = `translate ${duration}ms ease`;
    element.style.translate = translate;
    defer(element, () => {
        element.style.transition = transition;
    }, duration + 50);
};

const settle = (s, applied) => {
    if (s.settled) {
        return;
    }
    s.settled = true;
    s.ending = true;
    if (session === s) {
        session = null;
    }
    cancelAnimationFrame(s.frame);
    listen(false);
    s.observer.disconnect();
    const ghost = translateOf(s.ghost);
    const from = { left: s.rect.left + ghost.x, top: s.rect.top + ghost.y };
    s.ghost.remove();
    s.placeholder.remove();
    const glide = !applied && (s.animation > 0);
    const instant = [];
    for (const [element, saved] of s.saved) {
        element.style.visibility = saved.visibility;
        if (glide && s.animated.has(element)) {
            element.style.translate = saved.translate;
            defer(element, () => {
                element.style.transition = saved.transition;
            }, s.animation + 50);
        } else {
            element.style.transition = "none";
            element.style.translate = saved.translate;
            instant.push([element, saved]);
        }
    }
    if (instant.length > 0) {
        void document.body.offsetHeight;
        for (const [element, saved] of instant) {
            element.style.transition = saved.transition;
        }
    }
    unpad(s);
    highlight(s, null);
    unlockPage();
    const zone = applied ? s.target : s.source;
    const element = applied ? itemsOf(s.target)[s.targetIndex] : s.item;
    if (element?.isConnected && (s.animation > 0)) {
        flipFrom(element, from, s.animation);
    }
    if (s.mode === "keyboard") {
        const announcements = s.source.options.announcements;
        const position = (applied ? s.targetIndex : s.index) + 1;
        announce(format(s.commit ? announcements.dropped : announcements.canceled, position, itemsOf(zone).length, zone.options.name));
    }
    if (s.focused && element?.isConnected) {
        element.focus({ preventScroll: true });
    }
};

const finish = (commit) => {
    const s = session;
    if (!s || s.ending) {
        return;
    }
    if (commit && s.dirty && (s.mode !== "keyboard")) {
        s.dirty = false;
        layout(s);
    }
    s.ending = true;
    s.commit = commit;
    cancelAnimationFrame(s.frame);
    listen(false);
    const moved = commit && ((s.target !== s.source) || (s.targetIndex !== s.index));
    if (!moved) {
        settle(s, false);
        return;
    }
    if (s.mode !== "keyboard") {
        const slot = slotOf(s);
        s.ghost.style.transition = (s.animation > 0) ? `translate ${s.animation}ms ease` : "none";
        s.ghost.style.translate = `${slot.left - s.rect.left}px ${slot.top - s.rect.top}px`;
    }
    s.target.dotNetRef.invokeMethodAsync("NotifyMove", s.source.options.name, s.index, s.targetIndex)
        .then(() => settle(s, false), () => settle(s, false));
};

const clearPending = () => {
    if (!pending) {
        return;
    }
    clearTimeout(pending.timer);
    window.removeEventListener("pointermove", onPointerMove, true);
    window.removeEventListener("pointerup", onPointerUp, true);
    window.removeEventListener("pointercancel", onPointerCancel, true);
    document.removeEventListener("touchmove", onTouchMove, { passive: false });
    document.removeEventListener("touchend", onTouchEnd, { passive: false });
    document.removeEventListener("touchcancel", onTouchEnd, { passive: false });
    document.removeEventListener("selectstart", preventDefault, true);
    document.removeEventListener("dragstart", preventDefault, true);
    document.removeEventListener("contextmenu", preventDefault, true);
    pending = null;
    if (!session) {
        unlockPage();
    }
};

const suppressClick = () => {
    const onClick = (e) => {
        e.preventDefault();
        e.stopPropagation();
    };
    window.addEventListener("click", onClick, true);
    setTimeout(() => window.removeEventListener("click", onClick, true), 0);
};

const release = (x, y, commit) => {
    const s = (session?.mode === "pointer") ? session : null;
    if (s && !s.ending) {
        s.x = x;
        s.y = y;
        s.dirty = true;
    }
    clearPending();
    if (s) {
        if (commit) {
            suppressClick();
        }
        finish(commit);
    }
};

function onPointerMove(e) {
    if (!pending || (e.pointerId !== pending.pointerId)) {
        return;
    }
    if ((e.pointerType === "mouse") && ((e.buttons & 1) === 0)) {
        release(e.clientX, e.clientY, true);
        return;
    }
    if (!session && (Math.hypot(e.clientX - pending.x, e.clientY - pending.y) >= mouseThreshold)) {
        begin(pending.zone, pending.item, "pointer", pending.x, pending.y);
    }
    if (session && !session.ending) {
        session.x = e.clientX;
        session.y = e.clientY;
        session.dirty = true;
    }
}

function onPointerUp(e) {
    if (pending && (e.pointerId === pending.pointerId)) {
        release(e.clientX, e.clientY, true);
    }
}

function onPointerCancel(e) {
    if (pending && (e.pointerId === pending.pointerId)) {
        release(e.clientX, e.clientY, false);
    }
}

const touchOf = (e) => Array.from(e.changedTouches).find((touch) => touch.identifier === pending?.touchId);

function onTouchMove(e) {
    const touch = touchOf(e);
    if (!touch) {
        return;
    }
    if (session?.mode === "touch") {
        e.preventDefault();
        if (!session.ending) {
            session.x = touch.clientX;
            session.y = touch.clientY;
            session.dirty = true;
        }
        return;
    }
    if (Math.hypot(touch.clientX - pending.x, touch.clientY - pending.y) > touchTolerance) {
        clearPending();
    }
}

function onTouchEnd(e) {
    const touch = touchOf(e);
    if (!touch) {
        return;
    }
    const s = (session?.mode === "touch") ? session : null;
    if (s && !s.ending) {
        s.x = touch.clientX;
        s.y = touch.clientY;
        s.dirty = true;
    }
    clearPending();
    if (s) {
        if (e.cancelable) {
            e.preventDefault();
        }
        finish(e.type === "touchend");
    }
}

const startTouch = () => {
    if (pending && !session) {
        begin(pending.zone, pending.item, "touch", pending.x, pending.y);
    }
};

class Zone {
    constructor(element, dotNetRef) {
        this.element = element;
        this.dotNetRef = dotNetRef;
        this.options = null;
        this.onPointerDown = (e) => this.pointerDown(e);
        this.onTouchStart = (e) => this.touchStart(e);
        this.onKeyDown = (e) => this.keyDown(e);
        element.addEventListener("pointerdown", this.onPointerDown);
        element.addEventListener("touchstart", this.onTouchStart, { passive: true });
        element.addEventListener("keydown", this.onKeyDown);
    }

    pointerDown(e) {
        if (session || pending || (e.pointerType === "touch") || (e.button !== 0)) {
            return;
        }
        const item = itemOf(this, e.target);
        if (!draggable(this, item, e.target, false)) {
            return;
        }
        pending = { zone: this, item, x: e.clientX, y: e.clientY, pointerId: e.pointerId, timer: 0 };
        window.addEventListener("pointermove", onPointerMove, true);
        window.addEventListener("pointerup", onPointerUp, true);
        window.addEventListener("pointercancel", onPointerCancel, true);
        document.addEventListener("selectstart", preventDefault, true);
        document.addEventListener("dragstart", preventDefault, true);
    }

    touchStart(e) {
        if (session || pending || (e.touches.length !== 1)) {
            return;
        }
        const touch = e.touches[0];
        const item = itemOf(this, e.target);
        if (!draggable(this, item, e.target, false)) {
            return;
        }
        pending = { zone: this, item, x: touch.clientX, y: touch.clientY, touchId: touch.identifier, timer: 0 };
        lockPage(true);
        document.addEventListener("touchmove", onTouchMove, { passive: false });
        document.addEventListener("touchend", onTouchEnd, { passive: false });
        document.addEventListener("touchcancel", onTouchEnd, { passive: false });
        document.addEventListener("contextmenu", preventDefault, true);
        document.addEventListener("dragstart", preventDefault, true);
        if (this.options.handle || (this.options.touchDelay <= 0)) {
            startTouch();
        } else {
            pending.timer = setTimeout(startTouch, this.options.touchDelay);
        }
    }

    keyDown(e) {
        if (session || pending || ((e.key !== " ") && (e.key !== "Enter")) || e.altKey || e.ctrlKey || e.metaKey) {
            return;
        }
        const item = itemOf(this, e.target);
        if ((item !== e.target) || !draggable(this, item, e.target, true)) {
            return;
        }
        e.preventDefault();
        e.stopPropagation();
        begin(this, item, "keyboard", 0, 0);
    }

    update(options) {
        this.options = options;
        const s = session;
        if (s && !s.ending && options.disabled && ((s.source === this) || (s.target === this))) {
            finish(false);
        }
    }

    dispose() {
        const s = session;
        if (s && ((s.source === this) || (s.target === this))) {
            if (s.ending) {
                settle(s, false);
            } else {
                finish(false);
            }
        }
        if (pending?.zone === this) {
            clearPending();
        }
        this.element.removeEventListener("pointerdown", this.onPointerDown);
        this.element.removeEventListener("touchstart", this.onTouchStart, { passive: true });
        this.element.removeEventListener("keydown", this.onKeyDown);
        zones.delete(this.element);
    }
}

const toOptions = (name, group, sort, disabled, horizontal, handle, filter, touchDelay, animation, ghostClass, placeholderClass, overClass, grabbed, moved, dropped, canceled) => ({
    name,
    group: group || null,
    sort,
    disabled,
    horizontal,
    handle: handle || null,
    filter: filter || null,
    touchDelay,
    animation: Math.max(animation, 0),
    ghostClass,
    placeholderClass,
    overClass,
    announcements: { grabbed, moved, dropped, canceled }
});

export function attach(dotNetRef, element, ...options) {
    const zone = new Zone(element, dotNetRef);
    zone.update(toOptions(...options));
    zones.set(element, zone);
    return {
        update: (...values) => zone.update(toOptions(...values)),
        dispose: () => zone.dispose()
    };
}
