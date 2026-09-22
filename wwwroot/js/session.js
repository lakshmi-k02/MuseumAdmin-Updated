let activityTimer;

function registerActivity(dotnetHelper) {
    const resetTimer = () => {
        clearTimeout(activityTimer);
        activityTimer = setTimeout(() => {
            dotnetHelper.invokeMethodAsync("Logout");
        }, 15 * 60 * 1000); // 15 mins
    };

    ["mousemove", "keydown", "click", "touchstart"].forEach(evt =>
        document.addEventListener(evt, resetTimer)
    );

    resetTimer();
}
