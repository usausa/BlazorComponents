export function create(dotNetRef) {
    const channels = new Map();
    const listening = new Set();

    const open = (name) => {
        if (!self.BroadcastChannel) {
            return null;
        }
        let channel = channels.get(name);
        if (!channel) {
            channel = new BroadcastChannel(name);
            channel.onmessage = (e) => {
                if (listening.has(name)) {
                    dotNetRef.invokeMethodAsync("NotifyMessage", name, String(e.data));
                }
            };
            channels.set(name, channel);
        }
        return channel;
    };

    return {
        runLocked: async (name, timeout, action) => {
            if (!navigator.locks) {
                return false;
            }
            const controller = new AbortController();
            const timer = (timeout >= 0) ? setTimeout(() => controller.abort(), timeout) : null;
            let granted = false;
            try {
                await navigator.locks.request(name, { signal: controller.signal }, async () => {
                    granted = true;
                    clearTimeout(timer);
                    await action.invokeMethodAsync("RunAsync");
                });
                return true;
            } catch (e) {
                if (granted) {
                    throw e;
                }
                return false;
            } finally {
                clearTimeout(timer);
            }
        },
        listen: (name) => {
            listening.add(name);
            open(name);
        },
        unlisten: (name) => {
            listening.delete(name);
        },
        tell: (name, message) => {
            open(name)?.postMessage(message);
        },
        dispose: () => {
            listening.clear();
            for (const channel of channels.values()) {
                channel.close();
            }
            channels.clear();
        }
    };
}
