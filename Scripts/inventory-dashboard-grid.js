;(function ($, window) {
    'use strict';

    const GRID_SELECTOR = '#inventoryDashboardStatusGrid';
    let callbacks = {};

    function invoke(name, detail) {
        if (typeof callbacks[name] === 'function') callbacks[name](detail || {});
    }

    function columns() {
        return [
            {
                field: 'ExpandKey', title: '', width: 52,
                sortable: false, filterable: false, searchable: false,
                resizable: false, reorderable: false,
                headerClass: 'inventory-dashboard-col-expand',
                cellClass: 'inventory-dashboard-expand-cell',
                template: '#dashboard-add-grid-expand-template'
            },
            {
                field: 'ImageCount', title: 'Image', width: 81,
                sortable: false, filterable: false, searchable: false,
                headerClass: 'inventory-dashboard-col-image',
                template: '#dashboard-add-grid-image-template'
            },
            {
                field: 'VehicleLabel', title: 'Vehicle', width: 187,
                sortField: 'VehicleSort', filterField: 'VehicleLabel', searchField: 'SearchText',
                filterable: false,
                headerClass: 'inventory-dashboard-col-vehicle',
                cellClass: 'majordome-row-details inventory-dashboard-vehicle-cell',
                template: '#dashboard-add-grid-vehicle-template'
            },
            {
                field: 'Status', title: 'Status', width: 106,
                filterField: 'StatusFilter', searchField: 'StatusFilter',
                filterable: false,
                headerClass: 'inventory-dashboard-col-status text-center',
                cellClass: 'inventory-dashboard-status-dot-cell',
                template: '#dashboard-add-grid-status-template'
            },
            {
                field: 'Location', title: 'LOC', width: 105,
                sortField: 'LocationSort', filterField: 'LocationFilter',
                headerClass: 'inventory-dashboard-col-location text-center',
                cellClass: 'inventory-dashboard-main-location-cell',
                template: '#dashboard-add-grid-location-template'
            },
            {
                field: 'Days', title: 'Days', width: 94,
                filterField: 'DaysFilter',
                filterable: false,
                headerClass: 'inventory-dashboard-col-days text-end',
                cellClass: 'inventory-dashboard-days-cell',
                template: '#dashboard-add-grid-days-template'
            },
            {
                field: 'DetailsKey', title: 'Details', width: 90,
                sortable: false, filterable: false, searchable: false,
                resizable: false, reorderable: false,
                headerClass: 'inventory-dashboard-col-details text-center',
                cellClass: 'inventory-dashboard-details-cell',
                template: '#dashboard-add-grid-details-template'
            }
        ];
    }

    function initialize(rows, options) {
        const $grid = $(GRID_SELECTOR);
        if (!$grid.length || typeof $.fn.addGrid !== 'function') return false;

        callbacks = options || callbacks;
        if ($grid.data('add-grid')) {
            $grid.addGrid('setData', rows || [], { preserveState: true });
            return true;
        }

        $grid.addGrid({
            data: rows || [],
            columns: columns(),
            tableId: 'inventoryDashboardStatusTable',
            tableClass: 'table align-middle m-0',
            tbodyId: 'inventoryDashboardStatusBody',
            rowClass: function (item) {
                return 'inventory-dashboard-main-row' + (item.HistoryExpanded ? ' is-expanded' : '');
            },
            rowAttributes: function (item) {
                return {
                    'data-status-code': item.StatusCodes,
                    'data-location-code': item.LocationFilter,
                    'data-stock': item.Stock,
                    'data-stock-key': item.StockKey,
                    'data-history-target': item.HistoryRowId,
                    'aria-expanded': item.HistoryExpanded ? 'true' : 'false'
                };
            },
            detailTemplate: '#dashboard-add-grid-history-template',
            detailExpanded: function (item) { return item.HistoryExpanded; },
            detailRowId: function (item) { return item.HistoryRowId; },
            detailRowClass: 'inventory-dashboard-history-row',
            detailRowAttributes: function (item) {
                return { 'data-history-row-for': item.StockKey };
            },
            filterDropdownClass: 'dashboard-add-grid-filter',
            height: null,
            pageSize: 100,
            pageable: false,
            showRecordCount: true,
            recordType: { singular: 'vehicle', plural: 'vehicles', icon: 'bi bi-car-front-fill' },
            emptyText: 'No vehicles match the selected filter.',
            emptyHint: 'Adjust the dashboard or column filters to see more inventory.',
            sortable: true,
            filterable: true,
            resizable: true,
            reorderable: true,
            searchable: true,
            showSearch: true,
            searchTerm: window.gtxInventorySearch ? window.gtxInventorySearch.readStoredTerm() : '',
            searchPlaceholder: 'Search inventory…',
            searchPredicate: function (item, term) {
                if (window.gtxInventorySearch) {
                    return window.gtxInventorySearch.matches(item.SearchText, term, item.SearchTokens);
                }
                const terms = String(term || '').trim().toLowerCase().split(/\s+/).filter(Boolean);
                const text = String(item.SearchText || '').toLowerCase();
                return terms.every(function (part) { return text.indexOf(part) !== -1; });
            },
            showFilterChips: true,
            exportToExcel: false,
            exportToPdf: false,
            groupable: false,
            alternateRows: true,
            onRender: function (detail) { invoke('onRender', detail); },
            onSearchChange: function (detail) { invoke('onSearchChange', detail); },
            onFilterChange: function (detail) { invoke('onFilterChange', detail); },
            onSortChange: function (detail) { invoke('onSortChange', detail); },
            onColumnReorder: function (detail) { invoke('onColumnReorder', detail); }
        });
        return true;
    }

    window.inventoryDashboardAddGrid = {
        initialize: initialize,
        setData: function (rows) { return initialize(rows, callbacks); },
        setSearch: function (term) {
            const $grid = $(GRID_SELECTOR);
            if ($grid.data('add-grid')) $grid.addGrid('setSearch', term || '');
        },
        getSearch: function () {
            const $grid = $(GRID_SELECTOR);
            return $grid.data('add-grid') ? $grid.addGrid('getSearch') : '';
        },
        refresh: function () {
            const $grid = $(GRID_SELECTOR);
            if ($grid.data('add-grid')) $grid.addGrid('refresh');
        },
        findDataItem: function (stockKey) {
            const $grid = $(GRID_SELECTOR);
            if (!$grid.data('add-grid')) return null;
            const normalized = String(stockKey || '').trim().toUpperCase();
            const rows = $grid.addGrid('getData');
            for (let i = 0; i < rows.length; i++) {
                if (String(rows[i].StockKey || '').trim().toUpperCase() === normalized) return rows[i];
            }
            return null;
        }
    };
}(jQuery, window));
