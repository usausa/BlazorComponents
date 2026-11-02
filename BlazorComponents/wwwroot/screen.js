export function create(dotNetRef) {
    let keepScreenOn = false;
    let keepFullscreen = false;
    let sentinel = null;
    let acquiring = null;
    let disposed = false;

    const notify = (value) => {
        if (!disposed) {
            dotNetRef.invokeMethodAsync("NotifyScreenKeptOn", value);
        }
    };

    const acquire = async () => {
        try {
            const acquired = await navigator.wakeLock.request("screen");
            if (!keepScreenOn || disposed) {
                await acquired.release();
                return;
            }
            sentinel = acquired;
            acquired.addEventListener("release", () => {
                if (sentinel === acquired) {
                    sentinel = null;
                    notify(false);
                }
            });
            notify(true);
        } catch {
            sentinel = null;
        }
    };

    const request = () => {
        if (keepScreenOn && (sentinel === null) && (acquiring === null) && navigator.wakeLock && (document.visibilityState === "visible")) {
            acquiring = acquire().finally(() => {
                acquiring = null;
            });
        }
        return acquiring ?? Promise.resolve();
    };

    const release = async () => {
        const released = sentinel;
        sentinel = null;
        if (released !== null) {
            await released.release();
        }
    };

    const enterFullscreen = () => {
        if (keepFullscreen && document.fullscreenEnabled && (document.fullscreenElement === null)) {
            document.documentElement.requestFullscreen({ navigationUI: "hide" }).catch(() => { });
        }
    };

    const onVisibilityChange = () => {
        if (document.visibilityState === "visible") {
            request();
        }
    };

    document.addEventListener("visibilitychange", onVisibilityChange);
    document.addEventListener("pointerup", enterFullscreen, true);

    return {
        keepScreenOn: async (value) => {
            keepScreenOn = value;
            await (value ? request() : release());
            return sentinel !== null;
        },
        keepFullscreen: (value) => {
            keepFullscreen = value;
            if (value) {
                enterFullscreen();
            } else if (document.fullscreenElement === document.documentElement) {
                document.exitFullscreen().catch(() => { });
            }
        },
        dispose: () => {
            disposed = true;
            keepScreenOn = false;
            keepFullscreen = false;
            document.removeEventListener("visibilitychange", onVisibilityChange);
            document.removeEventListener("pointerup", enterFullscreen, true);
            release();
        }
    };
}
