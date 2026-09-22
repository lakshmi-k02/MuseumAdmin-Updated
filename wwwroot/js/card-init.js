window.getInitialCard = () => {
    return {
        id: crypto.randomUUID(),
        title: "Default Card (Injected)",
        isChecked: false
    };
};
