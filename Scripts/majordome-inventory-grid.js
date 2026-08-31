;(function ($, window) {
    'use strict';

    const GRID_SELECTOR = '#majordomeInventoryGrid';
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
                headerClass: 'majordome-inventory-col-expand',
                cellClass: 'inventory-dashboard-expand-cell',
                template: '#majordome-add-grid-expand-template'
            },
            {
                field: 'ImageCount', title: 'Image', width: 100,
                filterable: false, searchable: false,
                headerClass: 'majordome-inventory-col-image',
                template: '#majordome-add-grid-image-template'
            },
            {
                field: 'VehicleLabel', title: 'Vehicle', width: 200,
                sortField: 'VehicleSort', filterField: 'VehicleLabel', searchField: 'SearchText',
                headerClass: 'majordome-inventory-col-vehicle',
                cellClass: 'majordome-row-details',
                template: '#majordome-add-grid-vehicle-template'
            },
            {
                field: 'PurchasedDisplay', title: 'Purchased', width: 132,
                sortField: 'PurchasedSort', filterField: 'PurchasedDisplay',
                filterable: false,
                headerClass: 'd-none d-lg-table-cell majordome-inventory-col-purchased',
                cellClass: 'd-none d-lg-table-cell text-center majordome-date-cell',
                template: '#majordome-add-grid-purchased-template'
            },
            {
                field: 'Stock', title: 'Stock', width: 97,
                filterable: false,
                headerClass: 'majordome-inventory-col-stock',
                cellClass: 'text-center',
                template: '#majordome-add-grid-stock-template'
            },
            {
                field: 'Gtx', title: 'GTX', width: 109,
                headerClass: 'text-center d-none d-xl-table-cell majordome-inventory-col-gtx',
                cellClass: 'text-center d-none d-xl-table-cell',
                template: '#majordome-add-grid-gtx-template'
            },
            {
                field: 'Status', title: 'Status', width: 110,
                filterable: false,
                headerClass: 'text-center majordome-inventory-col-status',
                cellClass: 'inventory-dashboard-status-dot-cell',
                template: '#majordome-add-grid-status-template'
            },
            {
                field: 'Days', title: 'Days', width: 98,
                filterField: 'DaysFilter',
                filterable: false,
                headerClass: 'text-end majordome-inventory-col-days',
                cellClass: 'inventory-dashboard-days-cell',
                template: '#majordome-add-grid-days-template'
            },
            {
                field: 'Counter', title: 'Views', width: 102,
                filterable: false,
                headerClass: 'text-center majordome-inventory-col-counter',
                cellClass: 'text-center',
                template: '#majordome-add-grid-counter-template'
            },
            {
                field: 'Upload', title: 'Upload', width: 132,
                headerClass: 'text-center d-none d-md-table-cell majordome-inventory-col-upload',
                cellClass: 'text-center d-none d-md-table-cell',
                template: '#majordome-add-grid-upload-template'
            },
            {
                field: 'Story', title: 'Story', width: 123,
                headerClass: 'text-center d-none d-md-table-cell majordome-inventory-col-story',
                cellClass: 'text-center d-none d-md-table-cell',
                template: '#majordome-add-grid-story-template'
            },
            {
                field: 'DataOne', title: 'DataOne', width: 138,
                headerClass: 'text-center d-none d-md-table-cell majordome-inventory-col-dataone',
                cellClass: 'text-center d-none d-md-table-cell',
                template: '#majordome-add-grid-dataone-template'
            },
            {
                field: 'ActionsKey', title: 'Actions', width: 300,
                sortable: false, filterable: false, searchable: false,
                resizable: false, reorderable: false,
                headerClass: 'text-center majordome-actions-col',
                cellClass: 'shadow text-start majordome-actions-cell',
                template: '#majordome-add-grid-actions-template'
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
            tableId: 'majordomeInventoryTable',
            tableClass: 'table align-middle m-0',
            tbodyId: 'majordomeInventoryBody',
            rowClass: function (item) {
                return 'majordome-vehicle-row inventory-dashboard-main-row'
                    + (item.HistoryExpanded ? ' is-expanded' : '');
            },
            rowAttributes: function (item) {
                return {
                    'data-stock': item.Stock,
                    'data-stock-key': item.StockKey,
                    'data-history-target': item.HistoryRowId,
                    'data-search': item.SearchText,
                    'data-search-tokens': item.SearchTokens,
                    'aria-expanded': item.HistoryExpanded ? 'true' : 'false'
                };
            },
            detailTemplate: '#majordome-add-grid-history-template',
            detailExpanded: function (item) { return item.HistoryExpanded; },
            detailRowId: function (item) { return item.HistoryRowId; },
            detailRowClass: 'majordome-inventory-history-row',
            detailRowAttributes: function (item) {
                return { 'data-history-row-for': item.StockKey };
            },
            filterDropdownClass: 'majordome-add-grid-filter',
            height: null,
            pageSize: 100,
            pageable: false,
            showRecordCount: true,
            recordType: { singular: 'vehicle', plural: 'vehicles', icon: 'bi bi-car-front-fill' },
            emptyText: 'No vehicles match the current filter.',
            emptyHint: 'Adjust the search or column filters to see more inventory.',
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

    window.majordomeInventoryAddGrid = {
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
        getDataItem: function (rowElement) {
            const $grid = $(GRID_SELECTOR);
            return $grid.data('add-grid') ? $grid.addGrid('getDataItem', rowElement) : null;
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
