;(function ($, window) {
    'use strict';

    const GRID_SELECTOR = '#majordomeManagementGrid';
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
                headerClass: 'majordome-management-col-expand',
                template: '#management-add-grid-expand-template'
            },
            {
                field: 'ImageCount', title: 'Image', width: 100,
                sortField: 'ImageSort',
                filterable: false, searchable: false,
                headerClass: 'majordome-management-col-image',
                template: '#management-add-grid-image-template'
            },
            {
                field: 'VehicleLabel', title: 'Vehicle', width: 280,
                sortField: 'VehicleSort', filterField: 'VehicleLabel', searchField: 'SearchText',
                filterable: false,
                headerClass: 'majordome-management-col-vehicle',
                cellClass: 'majordome-row-details',
                template: '#management-add-grid-vehicle-template'
            },
            {
                field: 'Stock', title: 'Stock', width: 110,
                filterable: false,
                headerClass: 'majordome-management-col-stock',
                cellClass: 'text-center',
                template: '#management-add-grid-stock-template'
            },
            {
                field: 'Gtx', title: 'GTX', width: 120,
                sortField: 'GtxSort', filterField: 'GtxFilter',
                headerClass: 'text-center majordome-management-col-gtx',
                cellClass: 'text-center majordome-management-col-gtx',
                template: '#management-add-grid-gtx-template'
            },
            {
                field: 'Status', title: 'Status', width: 110,
                filterField: 'StatusFilter',
                headerClass: 'text-center majordome-management-col-status',
                cellClass: 'text-center majordome-management-col-status',
                template: '#management-add-grid-status-template'
            },
            {
                field: 'DetailsKey', title: 'Details', width: 88,
                sortable: false, filterable: false, searchable: false,
                resizable: false, reorderable: false,
                headerClass: 'text-center majordome-management-col-details',
                cellClass: 'inventory-dashboard-details-cell majordome-management-details-cell',
                template: '#management-add-grid-details-template'
            },
            {
                field: 'Spacer', title: '', width: 24,
                sortable: false, filterable: false, searchable: false,
                resizable: false, reorderable: false,
                headerClass: 'majordome-management-spacer-cell',
                cellClass: 'majordome-management-spacer-cell',
                template: '#management-add-grid-spacer-template'
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
            tableId: 'majordomeManagementTable',
            tableClass: 'table align-middle m-0',
            tbodyId: 'majordomeManagementBody',
            rowClass: function (item) {
                return 'majordome-management-row' + (item.Selected ? ' is-selected' : '');
            },
            rowAttributes: function (item) {
                return {
                    'data-stock': item.StockKey,
                    'data-status': item.StatusKey,
                    'data-search': item.SearchText,
                    'data-search-tokens': item.SearchTokens
                };
            },
            detailTemplate: '#management-add-grid-detail-template',
            detailExpanded: true,
            detailRowClass: 'majordome-management-detail-row',
            detailCellClass: 'majordome-management-detail-cell',
            filterDropdownClass: 'management-add-grid-filter',
            height: null,
            pageSize: 100,
            pageable: false,
            showRecordCount: true,
            recordType: { singular: 'vehicle', plural: 'vehicles', icon: 'bi bi-car-front-fill' },
            recordSummary: function (records) {
                const soldCount = records.filter(function (item) { return item.StatusKey === 'removed'; }).length;
                return [
                    {
                        count: records.length - soldCount,
                        singular: 'vehicle',
                        plural: 'vehicles',
                        icon: 'bi bi-car-front-fill'
                    },
                    {
                        count: soldCount,
                        singular: 'sold',
                        plural: 'sold',
                        icon: 'bi bi-cart-check-fill',
                        tone: 'danger'
                    }
                ];
            },
            emptyText: 'No management records match the current filters.',
            emptyHint: 'Adjust the search, status, location, or column filters.',
            sortable: true,
            filterable: true,
            resizable: true,
            reorderable: true,
            searchable: true,
            showSearch: true,
            searchTerm: window.gtxInventorySearch ? window.gtxInventorySearch.readStoredTerm() : '',
            searchPlaceholder: 'Search management records…',
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

    window.inventoryManagementAddGrid = {
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
        selectDataItem: function (stockKey) {
            const $grid = $(GRID_SELECTOR);
            if (!$grid.data('add-grid')) return;
            const normalized = String(stockKey || '').trim().toUpperCase();
            $grid.addGrid('getData').forEach(function (item) {
                item.Selected = String(item.StockKey || '').trim().toUpperCase() === normalized;
            });
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
