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
            return $('<a>', { class: 'btn btn-gradient-grey border-0 shadow appraisal-listing-button', href: url.href, target: '_blank', rel: 'noopener noreferrer', text: 'View' })[0].outerHTML;
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
            const outsideRadius = distance !== null && distance > request.Radius;
            const outsideMileage = miles !== null && Math.abs(miles - request.Miles) > 10000;
            const original = !subject && distance !== null && !outsideRadius && miles !== null && !outsideMileage;
            const scopeLabel = subject ? 'Appraised VIN' : outsideRadius && outsideMileage ? 'Wider radius + mileage'
                : outsideRadius ? 'Wider radius' : outsideMileage ? 'Wider mileage'
                    : original ? 'Within scope' : 'Scope unverified';
            return {
                Vehicle: row.vehicle || [row.year, row.make, row.model, row.trim].filter(value => value != null && value !== '').join(' '),
                Vin: String(row.vin || '—'), Dealer: String(row.dealer_name || ''),
                Price: price, PriceDisplay: price === null ? '—' : money.format(price),
                Mileage: miles, MileageDisplay: displayNumber(miles),
                Distance: distance, DistanceDisplay: distance === null ? 'Unknown' : integer.format(distance) + ' mi',
                Days: days, DaysDisplay: displayNumber(days), Listing: listingLink(row.vdp_url),
                Scope: $('<span>', { class: 'appraisal-scope-badge' + (outsideRadius || outsideMileage ? ' is-expanded' : ''), text: scopeLabel })[0].outerHTML,
                IsSubject: subject, IsLocal: original
            };
        });
    }

    function columns() {
        return [
            { field: 'Vin', title: 'VIN', width: '17%', searchable: false, filterable: false },
            { field: 'Dealer', title: 'Dealer', width: '19%', searchable: false, filterable: false },
            { field: 'Price', title: 'Ask', width: '9.75%', filterable: false, headerClass: 'text-end', cellClass: 'text-end', template: row => row.PriceDisplay, searchable: false },
            { field: 'Mileage', title: 'Mileage', width: '10%', filterable: false, headerClass: 'text-end', cellClass: 'text-end', template: row => row.MileageDisplay, searchable: false },
            { field: 'Distance', title: 'Distance', width: '10%', filterable: false, headerClass: 'text-end', cellClass: 'text-end', template: row => row.DistanceDisplay, searchable: false },
            { field: 'Days', title: 'Days', width: '10.25%', filterable: false, headerClass: 'text-end', cellClass: 'text-end', template: row => row.DaysDisplay, searchable: false },
            { field: 'Scope', title: 'Match', width: '14%', encoded: false, sortable: false, filterable: false, searchable: false, cellClass: 'text-center' },
            { field: 'Listing', title: 'Listing', width: '10%', headerClass: 'text-center', encoded: false, sortable: false, filterable: false, searchable: false, cellClass: 'text-center' }
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
                defaultSort: [{ field: 'Price', dir: 'asc' }]
            });
            $grid.find('.pg-grid-scroll').attr({ tabindex: '0', role: 'region', 'aria-labelledby': section.getAttribute('aria-labelledby') });
            grid.setAttribute('aria-label', label + ' comparable vehicle listings');
            caption.textContent = 'MarketCheck · ' + label + ' · ' + competitors.length + ' within original scope · ' + listings.length + ' rows shown';
            description.textContent = recent.checked
                ? 'MarketCheck listing history. Earlier advertisements do not confirm a sale.'
                : 'MarketCheck vehicles advertised for sale now. Asking prices are not confirmed sale prices.';
            note.textContent = (recent.checked ? rows.recentNote : rows.currentNote) + ' Wider-radius and wider-mileage rows are buyer references.';
        }

        [current, recent].forEach(option => option.addEventListener('change', function () {
            if (option.checked && rows) render();
        }));

        return {
            show: function (data, request) {
                const returnedCurrent = makeRows(data.comparables && data.comparables.listings, request);
                const returnedRecent = makeRows(data.recent_comparables && data.recent_comparables.listings, request);
                const references = data.marketCheckReferences || {};
                const currentReferences = references.current;
                const recentReferences = references.recent;
                const currentGrid = currentReferences ? makeRows(currentReferences.listings, request) : returnedCurrent.filter(row => row.IsLocal);
                const recentGrid = recentReferences ? makeRows(recentReferences.listings, request) : returnedRecent.filter(row => row.IsLocal);
                rows = {
                    subject: returnedCurrent.concat(returnedRecent).find(row => row.IsSubject),
                    current: currentGrid.filter(row => row.IsLocal),
                    recent: recentGrid.filter(row => row.IsLocal),
                    currentGrid: currentGrid.concat(returnedCurrent.filter(row => row.IsSubject)),
                    recentGrid: recentGrid.concat(returnedRecent.filter(row => row.IsSubject)),
                    currentNote: currentReferences && currentReferences.note || '',
                    recentNote: recentReferences && recentReferences.note || ''
                };
                current.checked = true;
                recent.checked = false;
                section.hidden = false;
                render();
                return rows;
            },
            clear: function () {
                rows = null;
                section.hidden = true;
                destroyGrid();
            }
        };
    }

    window.majordomeAppraisalComparables = { create: create, numeric: numeric };
}(window.jQuery, window));
