const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function source(file) {
    return fs.readFileSync(path.join(__dirname, '..', file), 'utf8');
}

function initializeGrid(showLocationTotals) {
    let options;
    const grid = {
        length: 1,
        data: () => options,
        addGrid(value) { options = value; }
    };
    const jquery = () => grid;
    jquery.fn = { addGrid: grid.addGrid };
    const window = {};
    vm.runInNewContext(source('Scripts/inventory-dashboard-grid.js'), { jQuery: jquery, window });
    assert.ok(window.inventoryDashboardAddGrid.initialize([], { showLocationTotals }));
    return options;
}

for (const showLocationTotals of [true, false, undefined]) {
    const options = initializeGrid(showLocationTotals);
    const location = options.columns.find(column => column.field === 'Location');
    const attributes = options.rowAttributes({ LocationFilter: 'TEST-LOCATION', Stock: '123' });
    assert.ok(location, 'LOC remains displayed and sortable without the totals grant');
    assert.equal(location.sortField, 'LocationSort');
    assert.equal(location.template, '#dashboard-add-grid-location-template');
    assert.equal(location.filterField, 'LocationFilter');
    assert.equal(location.filterable, true);
    assert.equal(location.searchable, showLocationTotals === true);
    assert.equal(options.showRecordCount({}), true);
    assert.equal(options.showRecordCount({ Status: ['Active'] }), true);
    assert.equal(options.showRecordCount({ Location: ['TEST-LOCATION'] }), showLocationTotals === true);
    assert.equal(options.groupable, false);
    assert.equal(Object.hasOwn(attributes, 'data-location-code'), showLocationTotals === true);
    assert.equal(attributes['data-stock'], '123');
    if (showLocationTotals) {
        assert.equal(attributes['data-location-code'], 'TEST-LOCATION');
    }
}

const view = source('Views/InventoryManagement/Dashboard.cshtml');
const start = view.indexOf('function getDashboardVehicleSearchObject(');
const end = view.indexOf('function isDashboardVehicleCurrent(', start);
assert.ok(start >= 0 && end > start, 'Locate the dashboard search functions');
// Razor writes each escaped pair of @ characters as a single @.
const searchFunctions = view.slice(start, end).replace(/@@/g, '@');
const rowStart = view.indexOf('function buildDashboardGridRow(');
const rowEnd = view.indexOf('function renderStatusGrid(', rowStart);
assert.ok(rowStart >= 0 && rowEnd > rowStart, 'Locate the grid row builder');
const buildRow = view.slice(rowStart, rowEnd);
const vehicle = { Stock: '123', Make: 'Honda', LocationCode: 'TEST-LOCATION', HistoryStatuses: [] };

for (const showLocationTotals of [true, false]) {
    const window = {};
    const context = vm.createContext({
        window, showLocationTotals,
        normalizeDashboardVehicle: value => value,
        normalizeLocationFilterCode: value => value,
        formatDate: () => '',
        normalizeDashboardSearchText: value => value.replace(/\s+/g, ' ').trim().toUpperCase(),
        getVehicleStatusCodes: () => [],
        locationBadgeInfo: value => ({ label: value, toneClass: 'test-badge' }),
        normalizeStockKey: value => value,
        dashboardHistoryRowId: () => 'history-123',
        expandedDashboardStocks: {},
        formatNumber: String,
        getDashboardDaysToneClass: () => '',
        getDashboardDaysIconClass: () => '',
        asNumber: () => 0,
        escapeDashboardHtml: value => String(value ?? ''),
        renderDashboardStatusDots: () => '',
        renderDashboardHistoryPanel: () => ''
    });
    vm.runInContext(source('Scripts/inventory-search.js'), context);
    vm.runInContext(searchFunctions, context);
    vm.runInContext(buildRow, context);
    const row = context.buildDashboardGridRow(vehicle, 0);
    assert.equal(row.Location, 'TEST-LOCATION');
    assert.equal(row.LocationFilter, 'TEST-LOCATION');
    assert.equal(row.LocationSort, 'test-location');
    assert.match(row.LocationHtml, />TEST-LOCATION<\/span>$/);
    const text = context.getDashboardVehicleSearchText(vehicle);
    const tokens = context.getDashboardVehicleTokenSearchText(vehicle);
    assert.ok(window.gtxInventorySearch.matches(text, 'Honda', tokens));
    assert.equal(window.gtxInventorySearch.matches(text, 'TEST-LOCATION', tokens), showLocationTotals);
    assert.equal(window.gtxInventorySearch.matches(text, '@@OW', tokens), showLocationTotals);
    window.gtxInventorySearch = null;
    assert.equal(context.getDashboardVehicleSearchText(vehicle).includes('TEST-LOCATION'), showLocationTotals);
    assert.equal(context.getDashboardVehicleTokenSearchText(vehicle).includes('TEST-LOCATION'), showLocationTotals);
}

const loadStart = view.indexOf('function loadDashboard(days, forceRefresh)');
const loadEnd = view.indexOf('function bindDashboardPicker(', loadStart);
assert.ok(loadStart >= 0 && loadEnd > loadStart, 'Locate the Dashboard period loader');
const loadDashboard = view.slice(loadStart, loadEnd);

for (const initialGrant of [true, false]) {
    for (const updatedGrant of [true, false, undefined]) {
        let onSuccess;
        const actions = [];
        const request = {
            done(handler) { onSuccess = handler; return this; },
            fail() { return this; },
            always() { return this; }
        };
        const context = vm.createContext({
            $: { get: () => request },
            window: { location: { reload: () => actions.push('reload') } },
            showLocationTotals: initialGrant,
            dashboardRequest: null,
            dashboardRoot: () => '/',
            setDashboardBusy: () => {},
            valueOf: (value, key, fallback) => value[key] ?? fallback,
            cacheDashboard: () => actions.push('cache'),
            renderDashboard: () => actions.push('render')
        });
        vm.runInContext(loadDashboard, context);
        context.loadDashboard(30, true);
        onSuccess({ success: true, showLocationTotals: updatedGrant, dashboard: {} });
        assert.deepEqual(actions, updatedGrant !== undefined && updatedGrant !== initialGrant
            ? ['reload']
            : ['cache', 'render']);
    }
}

console.log('PASS: Dashboard LOC filtering, count visibility, and period reload after a location permission change.');
