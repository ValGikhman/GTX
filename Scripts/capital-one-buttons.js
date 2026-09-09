(function () {
    'use strict';

    if (!window.customElements || !window.MutationObserver) return;

    var selector = 'capital-one-button-widget[data-widget-type="check-availability"], ' +
        'capital-one-button-widget[data-widget-type="trade-in"]';

    function styleButtons(root) {
        if (root.nodeType !== 1 && root.nodeType !== 9) return;
        if (root.nodeType === 1 && root.matches(selector)) applyStyle(root);
        root.querySelectorAll(selector).forEach(applyStyle);
    }

    function applyStyle(button) {
        // Use the provider's reactive property; its closed Shadow DOM is untouched.
        // CSS variables keep hover/focus colors and all theme choices in the CSS file.
        button.styleOverrides = Object.assign({}, button.styleOverrides, {
            background: 'var(--c1-button-bg)',
            color: 'var(--c1-button-color)',
            border: 'var(--c1-button-border)',
            'border-radius': 'var(--c1-button-border-radius)',
            'font-size': 'var(--capital-one-button-font-size)',
            'font-weight': '900'
        });
    }

    window.customElements.whenDefined('capital-one-button-widget').then(function () {
        // Inventory filtering inserts new cards after the initial page load.
        var observer = new MutationObserver(function (records) {
            records.forEach(function (record) {
                record.addedNodes.forEach(styleButtons);
            });
        });
        observer.observe(document.body, { childList: true, subtree: true });
        styleButtons(document);
    });
}());
