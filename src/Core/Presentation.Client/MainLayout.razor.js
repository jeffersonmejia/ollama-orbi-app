let onlineHandler;
let offlineHandler;
let connectivityTimer;
let dotNet;
let lastKnownStatus;

async function checkConnectivity() {
    if (!navigator.onLine) return false;

    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), 5000);
    try {
        const response = await fetch('/health/live', {
            method: 'GET',
            cache: 'no-store',
            credentials: 'same-origin',
            signal: controller.signal
        });
        return response.ok;
    } catch {
        return false;
    } finally {
        window.clearTimeout(timeout);
    }
}

async function publishConnectivity() {
    const isOnline = await checkConnectivity();
    if (isOnline !== lastKnownStatus) {
        lastKnownStatus = isOnline;
        await dotNet.invokeMethodAsync('OnConnectionChanged', isOnline).catch(() => {});
    }
    return isOnline;
}

export async function subscribe(dotNetReference) {
    dotNet = dotNetReference;
    lastKnownStatus = null;
    onlineHandler = () => { void publishConnectivity(); };
    offlineHandler = () => {
        lastKnownStatus = false;
        void dotNet.invokeMethodAsync('OnConnectionChanged', false).catch(() => {});
    };

    window.addEventListener('online', onlineHandler);
    window.addEventListener('offline', offlineHandler);
    connectivityTimer = window.setInterval(() => { void publishConnectivity(); }, 15000);

    lastKnownStatus = await checkConnectivity();
    return lastKnownStatus;
}

export function isOnline() { return checkConnectivity(); }

export function unsubscribe() {
    if (onlineHandler) window.removeEventListener('online', onlineHandler);
    if (offlineHandler) window.removeEventListener('offline', offlineHandler);
    if (connectivityTimer) window.clearInterval(connectivityTimer);
    onlineHandler = undefined;
    offlineHandler = undefined;
    connectivityTimer = undefined;
    dotNet = undefined;
    lastKnownStatus = undefined;
}
