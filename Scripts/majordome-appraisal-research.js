;(function ($, window) {
    'use strict';

    function bindForm(form, handlers) {
        const fields = Object.fromEntries(['vin', 'miles', 'zip', 'radius'].map(name => [name, form.elements.namedItem(name)]));
        const submit = form.querySelector('[data-appraisal-submit]');
        const label = submit.querySelector('span');
        const idleLabel = label.textContent;
        let request = null;

        function setBusy(busy) {
            submit.disabled = busy;
            label.textContent = busy ? 'Researching…' : idleLabel;
            if (handlers.lockInputs) Object.values(fields).forEach(field => { field.disabled = busy; });
            handlers.busy(busy);
        }

        function cancel() {
            const active = request;
            if (!active) return;
            request = null;
            active.abort();
            setBusy(false);
        }

        Object.values(fields).forEach(field => field.addEventListener('input', function () {
            const hadRequest = request !== null;
            cancel();
            handlers.input(hadRequest);
        }));
        fields.vin.addEventListener('input', function () { this.value = this.value.toUpperCase().replace(/\s/g, '').slice(0, 17); });

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            if (request || !form.reportValidity()) return;

            const vin = fields.vin.value.trim().toUpperCase();
            const token = form.elements.namedItem('__RequestVerificationToken');
            if (!/^[A-HJ-NPR-Z0-9]{17}$/.test(vin)) { handlers.error('Enter a valid 17-character VIN.'); return; }
            if (!token || !token.value) { handlers.error('Refresh the page and sign in again before researching a price.'); return; }
            if (typeof $.fn.addGrid !== 'function') { handlers.error('The comparables grid could not be loaded. Refresh the page and try again.'); return; }

            const submitted = { vin: vin, miles: Number(fields.miles.value), zip: fields.zip.value.trim(), radius: Number(fields.radius.value) };
            const body = new URLSearchParams({ vin: submitted.vin, miles: submitted.miles, __RequestVerificationToken: token.value });
            if (submitted.zip) body.set('zip', submitted.zip);
            if (submitted.radius) body.set('radius', submitted.radius);

            handlers.start();
            setBusy(true);
            const active = $.ajax({ url: form.action, method: 'POST', data: body.toString(), dataType: 'json', timeout: 150000 });
            request = active;

            active.done(function (report) {
                if (request !== active) return;
                const data = report && (report.data || report.Data);
                const scope = report && (report.request || submitted);
                if (!data || typeof data !== 'object' || !scope || !Number.isInteger(scope.miles)
                    || !Number.isInteger(scope.radius) || scope.radius < 1 || !/^[0-9]{5}$/.test(scope.zip)) {
                    handlers.error('The selected sources did not return price research for this vehicle.');
                    return;
                }

                const effective = { Vin: String(scope.vin || vin).toUpperCase(), Miles: scope.miles, Zip: scope.zip, Radius: scope.radius };
                try {
                    handlers.result(data, effective, report.demand);
                } catch (_) {
                    handlers.error('The research results could not be displayed. Please try again.');
                }
            }).fail(function (xhr, status) {
                if (request !== active) return;
                if (status === 'timeout') { handlers.error('The appraisal request timed out. Please try again.'); return; }
                if (xhr.status === 401 || xhr.status === 403 || status === 'parsererror') {
                    handlers.error('Sign in to Majordome again before researching a price.');
                    return;
                }
                const message = xhr.responseJSON && xhr.responseJSON.message;
                handlers.error(message || (xhr.status
                    ? 'GTX could not load the appraisal results (HTTP ' + xhr.status + '). Please try again.'
                    : 'The appraisal connection did not return a response. Please try again.'));
            }).always(function () {
                if (request !== active) return;
                request = null;
                setBusy(false);
            });
        });

        return { fields: fields, cancel: cancel };
    }

    function createMarket(root, gridHeight) {
        const grid = window.majordomeAppraisalComparables;
        const comparables = grid.create(root.querySelector('[data-appraisal-comparables]'), gridHeight);
        const integer = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
        const days = new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 });
        const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
        const overview = root.querySelector('[data-appraisal-market-overview]');
        const demandSection = root.querySelector('[data-appraisal-market-demand]');
        const helpPopovers = window.bootstrap && window.bootstrap.Popover
            ? Array.from(root.querySelectorAll('[data-appraisal-help]'), button => {
                const popover = window.bootstrap.Popover.getOrCreateInstance(button, { container: 'body' });
                button.addEventListener('blur', () => popover.hide());
                return popover;
            })
            : [];

        function text(section, name, value) { section.querySelector('[data-appraisal-' + name + ']').textContent = value; }
        function validCount(value) { return Number.isFinite(value) && value >= 0; }

        function showDemand(demand) {
            const values = demand || {};
            text(demandSection, 'demand-days', values.status === 'ready' && validCount(values.daysSupply) ? days.format(values.daysSupply) + ' days' : 'Unavailable');
            text(demandSection, 'demand-active', validCount(values.activeCount) ? integer.format(values.activeCount) : '—');
            text(demandSection, 'demand-sales', validCount(values.sales45Days) ? integer.format(values.sales45Days) : '—');
            text(demandSection, 'demand-message', values.message || 'Local demand data is unavailable. Pricing remains available.');
            const timestamp = values.checkedAtUtc ? new Date(values.checkedAtUtc) : null;
            text(demandSection, 'demand-time', timestamp && !isNaN(timestamp.getTime()) ? 'Checked ' + timestamp.toLocaleString() : '');
        }

        return {
            show: function (data, scope, demand, fallbackTitle) {
                const rows = comparables.show(data, scope);
                const priced = rows.current.filter(row => row.Price > 0);
                const estimate = grid.numeric(data.marketcheck_price);
                text(overview, 'vehicle-title', (rows.subject && rows.subject.Vehicle) || fallbackTitle || (data.marketCheckReferences && data.marketCheckReferences.vehicle) || 'Vehicle price research');
                text(overview, 'request-summary', scope.Vin + ' · ' + integer.format(scope.Miles) + ' miles · ZIP ' + scope.Zip + ' · ' + scope.Radius + '-mile radius · Independent dealer');
                text(overview, 'prediction', estimate > 0 ? money.format(estimate) : 'Unavailable');
                text(overview, 'local-mean', priced.length ? money.format(priced.reduce((sum, row) => sum + row.Price, 0) / priced.length) : '—');
                text(overview, 'current-count', integer.format(rows.current.length));
                text(overview, 'recent-count', integer.format(rows.recent.length));
                root.querySelector('[data-appraisal-note]').textContent += ' The current asking-price mean uses ' + priced.length
                    + ' priced current comparables without mileage or condition adjustments. The MarketCheck estimate is the provider prediction.';
                showDemand(demand);
                return estimate > 0 ? estimate : null;
            },
            clear: function () {
                helpPopovers.forEach(popover => popover.hide());
                comparables.clear();
            }
        };
    }

    window.majordomeAppraisalResearch = { bindForm: bindForm, createMarket: createMarket };
}(window.jQuery, window));
