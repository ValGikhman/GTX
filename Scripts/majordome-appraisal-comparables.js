;(function ($, window) {
    'use strict';

    const integer = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
    const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });

    function numeric(value) {
        if (value == null || (typeof value === 'string' && value.trim() === '')) return null;
        const number = Number(value);
        return Number.isFinite(number) ? number : null;
    }

    function displayNumber(value) { return value === null ? '—' : integer.format(value); }

    function listingLink(value) {
        try {
            if (!/^https?:\/\//i.test(String(value || ''))) return 'No listing URL';
            const url = new URL(value);
            if (url.username || url.password || !/^https?:$/.test(url.protocol)) return 'No listing URL';
            return $('<a>', { class: 'btn btn-gradient-grey border-0 shadow w-100 appraisal-listing-button', href: url.href, target: '_blank', rel: 'noopener noreferrer', text: 'View' })[0].outerHTML;
        } catch (_) {
            return 'No listing URL';
        }
    }

    function makeRows(listings, request) {
        if (!Array.isArray(listings)) return [];
        return listings.filter(row => row && typeof row === 'object').map(function (row) {
            const miles = numeric(row.miles), price = numeric(row.price), days = numeric(row.dom);
            const reportedDistance = typeof row.dist === 'number' || typeof row.dist === 'string' ? numeric(row.dist) : null;
            const distance = reportedDistance !== null && reportedDistance >= 0 ? reportedDistance : null;
            const subject = String(row.vin || '').toUpperCase() === request.Vin;
            return {
                Vehicle: [row.year, row.make, row.model, row.trim].filter(value => value != null && value !== '').join(' '),
                Vin: String(row.vin || ''), Dealer: [row.dealer_name, subject ? 'Appraised VIN' : ''].filter(Boolean).join(' · '),
                Price: price, PriceDisplay: price === null ? '—' : money.format(price),
                Mileage: miles, MileageDisplay: displayNumber(miles),
                Distance: distance, DistanceDisplay: distance === null ? 'Unknown' : integer.format(distance) + ' mi',
                Days: days, DaysDisplay: displayNumber(days), Source: listingLink(row.vdp_url),
                IsSubject: subject, IsLocal: !subject && distance !== null && distance <= request.Radius
            };
        });
    }

    function columns() {
        return [
            { field: 'Vin', title: 'VIN', width: '19%', searchable: false, filterable: false },
            { field: 'Dealer', title: 'Dealer', width: '24%', searchable: false, filterable: false },
            { field: 'Price', title: 'Asking price', width: '14%', filterable: false, headerClass: 'text-end', cellClass: 'text-end', template: row => row.PriceDisplay, searchable: false },
            { field: 'Mileage', title: 'Mileage', width: '12%', filterable: false, headerClass: 'text-end', cellClass: 'text-end', template: row => row.MileageDisplay, searchable: false },
            { field: 'Distance', title: 'Distance', width: '12%', filterable: false, headerClass: 'text-end', cellClass: 'text-end', template: row => row.DistanceDisplay, searchable: false },
            { field: 'Days', title: 'Days', width: '11%', filterable: false, headerClass: 'text-end', cellClass: 'text-end', template: row => row.DaysDisplay, searchable: false },
            { field: 'Source', title: 'Source', width: '8%', encoded: false, sortable: false, filterable: false, searchable: false, cellClass: 'text-center' }
        ];
    }

    function create(section, height) {
        const grid = section.querySelector('[data-appraisal-grid]');
        const $grid = $(grid);
        const caption = section.querySelector('[data-appraisal-caption]');
        const description = section.querySelector('[data-appraisal-description]');
        const note = section.querySelector('[data-appraisal-note]');
        const current = section.querySelector('[data-appraisal-period][value="current"]');
        const recent = section.querySelector('[data-appraisal-period][value="recent"]');
        let scope = null;
        let rows = null;

        function destroyGrid() {
            if ($grid.data('add-grid')) $grid.addGrid('destroy');
        }

        function render() {
            const label = recent.checked ? 'Recent' : 'Current';
            const competitors = recent.checked ? rows.recent : rows.current;
            const listings = recent.checked ? rows.recentGrid : rows.currentGrid;
            // Reset sorting and scroll when switching between periods.
            destroyGrid();
            $grid.addGrid({
                data: listings, columns: columns(), tableClass: 'table align-middle m-0',
                rowClass: row => row.IsSubject ? 'appraisal-subject-row' : '',
                pageable: false, height: height || '510px', showSearch: false, resizable: false,
                autozoomable: false, exportToExcel: false, exportToPdf: false,
                defaultSort: [{ field: 'Distance', dir: 'asc' }]
            });
            $grid.find('.pg-grid-scroll').attr({ tabindex: '0', role: 'region', 'aria-labelledby': section.getAttribute('aria-labelledby') });
            grid.setAttribute('aria-label', label + ' comparable vehicle listings');
            caption.textContent = label + ' · ' + integer.format(competitors.length)
                + (competitors.length === 1 ? ' competitor within ' : ' competitors within ') + scope.Radius + ' miles'
                + (listings.some(row => row.IsSubject) ? ' · Includes appraised VIN for reference' : '');
            const explanation = recent.checked
                ? 'Recent shows listing history for similar cars and can include earlier advertisements; it does not confirm a sale. Current shows cars advertised for sale now.'
                : 'Current shows similar cars advertised for sale now, so you can compare your price with today’s asking prices. Recent shows listing history that can include earlier advertisements.';
            description.textContent = explanation + ' For example, imagine the same car advertised at $18,000 earlier and $20,000 now. Those are two asking prices for one car; they do not prove it sold for $18,000.';
        }

        [current, recent].forEach(option => option.addEventListener('change', function () {
            if (option.checked && rows) render();
        }));

        return {
            show: function (data, request) {
                const returnedCurrent = makeRows(data.comparables && data.comparables.listings, request);
                const returnedRecent = makeRows(data.recent_comparables && data.recent_comparables.listings, request);
                scope = request;
                rows = {
                    subject: returnedCurrent.concat(returnedRecent).find(row => row.IsSubject),
                    current: returnedCurrent.filter(row => row.IsLocal),
                    recent: returnedRecent.filter(row => row.IsLocal),
                    currentGrid: returnedCurrent.filter(row => row.IsLocal || row.IsSubject),
                    recentGrid: returnedRecent.filter(row => row.IsLocal || row.IsSubject)
                };
                current.checked = true;
                recent.checked = false;
                note.textContent = 'Showing competitors within ' + scope.Radius + ' miles of ZIP ' + scope.Zip
                    + '. Competitor listings with unknown distances or outside the radius are excluded. The appraised VIN appears for reference when returned and is excluded from competitor counts and the asking-price mean.';
                section.hidden = false;
                render();
                return rows;
            },
            clear: function () {
                rows = null;
                scope = null;
                section.hidden = true;
                destroyGrid();
            }
        };
    }

    window.majordomeAppraisalComparables = { create: create, numeric: numeric };
}(window.jQuery, window));
