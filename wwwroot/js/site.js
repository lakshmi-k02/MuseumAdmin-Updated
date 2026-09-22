// wwwroot/js/site.js
window.focusOnceOnTouch = (inputId, timeoutMs) => {
    timeoutMs = timeoutMs || 300;
    // use pointerdown so it works for touch but also avoids some double events
    function handler(e) {
        try {
            const el = document.getElementById(inputId);
            if (!el) return;
            // if already focused, stop listening
            if (document.activeElement === el) {
                cleanup();
                return;
            }
            // focus once
            el.focus();
        } catch (err) {
            // ignore
        } finally {
            // remove listener after the short timeout to avoid loops
            setTimeout(cleanup, timeoutMs);
        }
    }
    function cleanup() {
        window.removeEventListener('pointerdown', handler, { passive: true });
        window.removeEventListener('touchstart', handler, { passive: true });
    }
    // add both for maximum compatibility
    window.addEventListener('pointerdown', handler, { passive: true });
    window.addEventListener('touchstart', handler, { passive: true });
};
