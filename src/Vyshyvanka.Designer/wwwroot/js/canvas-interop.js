// Canvas interop functions for coordinate transformation
window.canvasInterop = {
    // Get element dimensions
    getElementDimensions: function (element) {
        if (!element) return { width: 800, height: 600 };
        const rect = element.getBoundingClientRect();
        return { width: rect.width, height: rect.height };
    },

    // Set up resize observer and call .NET method when size changes
    observeResize: function (element, dotNetRef) {
        if (!element || !dotNetRef) return null;

        const observer = new ResizeObserver(entries => {
            for (const entry of entries) {
                const { width, height } = entry.contentRect;
                dotNetRef.invokeMethodAsync('OnCanvasResized', width, height);
            }
        });

        observer.observe(element);
        return observer;
    },

    // Disconnect resize observer
    disconnectObserver: function (observer) {
        if (observer) {
            observer.disconnect();
        }
    }
};
