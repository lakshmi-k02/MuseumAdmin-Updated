window.usersFamilies = {
    dotnetRef: null,

    registerDotNet: function (ref) {
        this.dotnetRef = ref;
    },

    notifySaveCompleted: function () {
        if (this.dotnetRef) {
            this.dotnetRef.invokeMethodAsync("OnUserContentSaved");
        }
    }
};
