;(function (window, document) {
    'use strict';

    const root = document.getElementById('price-research');
    if (!root) return;

    const research = window.majordomeAppraisalResearch;
    const market = research.createMarket(root);
    const results = document.getElementById('pr-results');
    const empty = document.getElementById('pr-empty');
    const error = document.getElementById('pr-error');
    const progress = document.getElementById('pr-progress');
    const resultsPanel = root.querySelector('.pr-results-panel');

    function notify(type, detail) {
        root.dispatchEvent(new CustomEvent('pr:research-' + type, { detail: detail }));
    }

    function clearResults() {
        market.clear();
        results.hidden = true;
        empty.hidden = false;
        error.hidden = true;
    }

    const form = research.bindForm(root.querySelector('[data-appraisal-research-form]'), {
        busy: function (busy) {
            resultsPanel.setAttribute('aria-busy', String(busy));
            progress.hidden = !busy;
        },
        input: function (hadRequest) {
            const hadResults = !results.hidden || hadRequest;
            clearResults();
            if (hadResults) {
                error.textContent = 'Vehicle details changed. Run price research again to update the results.';
                error.hidden = false;
            }
        },
        start: function () {
            clearResults();
            notify('start');
        },
        error: function (message) {
            clearResults();
            error.textContent = message;
            error.hidden = false;
            notify('error');
        },
        result: function (data, scope, demand) {
            results.hidden = false;
            empty.hidden = true;
            const estimate = market.show(data, scope, demand);
            const auctionAssumptions = linkedBid && linkedBid.vin === scope.Vin ? linkedBid : null;
            linkedBid = null;
            notify('result', {
                expectedSellingPriceUsd: estimate,
                auctionAssumptions: auctionAssumptions
            });
        }
    });

    function linkedNumber(params, key, input) {
        const value = (params.get(key) || '').trim();
        const pattern = input.step === '1' ? /^\d+$/ : /^\d+(?:\.\d{1,2})?$/;
        if (!pattern.test(value)) return null;
        const amount = Number(value);
        return amount >= Number(input.min) && amount <= Number(input.max) ? amount : null;
    }

    function prefillFromLink() {
        const params = new URLSearchParams(window.location.search);
        const vin = (params.get('vin') || '').trim().toUpperCase();
        if (!/^[A-HJ-NPR-Z0-9]{17}$/.test(vin)) return null;
        form.fields.vin.value = vin;
        const zip = (params.get('zip') || '').trim();
        if (/^[0-9]{5}$/.test(zip)) form.fields.zip.value = zip;
        ['miles', 'radius'].forEach(function (key) {
            const field = form.fields[key];
            const amount = linkedNumber(params, key, field);
            if (amount !== null) field.value = String(amount);
        });
        return {
            vin: vin,
            expectedSellingPriceUsd: linkedNumber(params, 'resaleUsd', document.getElementById('pr-bid-resale')),
            currentHammerBidUsd: linkedNumber(params, 'hammerBidUsd', document.getElementById('pr-bid-current'))
        };
    }

    let linkedBid = prefillFromLink();
}(window, document));
