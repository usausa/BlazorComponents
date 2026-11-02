export function create() {
    return {
        getLocation: (highAccuracy, timeout) => new Promise((resolve) => {
            if (!navigator.geolocation || !window.isSecureContext) {
                resolve([4]);
                return;
            }

            let settled = false;
            let timer = null;
            const settle = (values) => {
                if (!settled) {
                    settled = true;
                    clearTimeout(timer);
                    resolve(values);
                }
            };

            const options = { enableHighAccuracy: highAccuracy, maximumAge: 0 };
            if (timeout >= 0) {
                options.timeout = timeout;
                timer = setTimeout(() => settle([3]), timeout);
            }
            navigator.geolocation.getCurrentPosition(
                (position) => settle([0, position.coords.latitude, position.coords.longitude, position.coords.accuracy]),
                (error) => settle([error.code]),
                options);
        }),
        dispose: () => { }
    };
}
