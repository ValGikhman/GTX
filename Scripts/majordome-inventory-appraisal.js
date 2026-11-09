;(function ($, window, document) {
    'use strict';

    $(function () {
        const modal = document.getElementById('majordomeInventoryAppraisalModal');
        if (!modal) return;

        const $modal = $(modal);
        const form = modal.querySelector('[data-appraisal-research-form]');
        const vehicleLabel = modal.querySelector('[data-inventory-appraisal-vehicle]');
        const inventoryPrice = modal.querySelector('[data-inventory-appraisal-inventory-price]');
        const priceAmount = modal.querySelector('[data-inventory-appraisal-price-amount]');
        const fee = Number(modal.dataset.documentaryFee);
        const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
        const status = modal.querySelector('[data-inventory-appraisal-status]');
        const error = modal.querySelector('[data-inventory-appraisal-error]');
        const results = modal.querySelector('[data-inventory-appraisal-result]');
        const research = window.majordomeAppraisalResearch;
        const market = research.createMarket(results, 'clamp(220px, 40vh, 510px)');
        let opener = null;
        let vehicleTitle = '';
        let findInventoryVehicle;

        function showInventoryPrice(vin) {
            const match = /^[A-HJ-NPR-Z0-9]{17}$/.test(vin) && findInventoryVehicle && findInventoryVehicle(vin);
            const price = match ? Number(match.InternetPrice) : 0;
            inventoryPrice.hidden = !Number.isFinite(price) || price <= 0 || !Number.isFinite(fee);
            priceAmount.textContent = inventoryPrice.hidden ? '' : money.format(price + fee);
        }

        function clearResults() {
            market.clear();
            results.hidden = true;
            error.hidden = true;
        }

        const search = research.bindForm(form, {
            lockInputs: true,
            busy: function (busy) {
                form.setAttribute('aria-busy', String(busy));
                status.textContent = busy ? 'Checking the market for you…' : '';
            },
            start: clearResults,
            input: clearResults,
            error: function (message) {
                clearResults();
                error.textContent = message;
                error.hidden = false;
            },
            result: function (data, scope, demand) {
                results.hidden = false;
                market.show(data, scope, demand, search.fields.vin.readOnly ? vehicleTitle : '');
            }
        });

        search.fields.vin.addEventListener('input', function () { showInventoryPrice(this.value); });

        $modal.on('shown.bs.modal', function () {
            if (search.fields.vin.readOnly && form.checkValidity()) {
                form.requestSubmit();
                return;
            }
            (search.fields.vin.readOnly ? search.fields.miles : search.fields.vin).focus();
        }).on('hide.bs.modal', search.cancel).on('hidden.bs.modal', function () {
            if (opener && document.contains(opener)) opener.focus();
            opener = null;
        });

        window.majordomeInventoryAppraisal = {
            open: function (vehicle, trigger, findByVin) {
                search.cancel();
                form.reset();
                clearResults();
                opener = trigger;
                const fields = search.fields;
                fields.vin.value = String(vehicle.VIN || '').trim().toUpperCase();
                fields.vin.readOnly = /^[A-HJ-NPR-Z0-9]{17}$/.test(fields.vin.value);
                findInventoryVehicle = function (vin) {
                    return vin === String(vehicle.VIN || '').trim().toUpperCase() ? vehicle : findByVin(vin);
                };
                showInventoryPrice(fields.vin.value);
                if (Number.isInteger(vehicle.Mileage) && vehicle.Mileage >= 0 && vehicle.Mileage <= 2000000) {
                    fields.miles.value = vehicle.Mileage;
                }

                vehicleTitle = [vehicle.Year, vehicle.Make, vehicle.Model].filter(Boolean).join(' ');
                const stock = vehicle.Stock ? 'Stock #' + vehicle.Stock : '';
                vehicleLabel.textContent = [vehicleTitle, stock].filter(Boolean).join(' · ') || 'Selected vehicle';
                window.bootstrap.Modal.getOrCreateInstance(modal).show(trigger);
            }
        };
    });
}(jQuery, window, document));
