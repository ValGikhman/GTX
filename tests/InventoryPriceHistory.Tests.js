const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const window = {
    Chart: class {
        constructor(canvas, config) {
            this.config = config;
        }
    },
    getComputedStyle: () => ({ color: '#172033', getPropertyValue: () => '' })
};
const source = fs.readFileSync(path.join(__dirname, '../Scripts/inventory-history.js'), 'utf8');
vm.runInNewContext(source, { window });
const history = window.gtxInventoryHistory;
const options = {
    formatDateTime: value => new Date(Number(/^\/Date\((\d+)\)\/$/.exec(value)[1])).toISOString().slice(0, 10)
};

function entry(status, date, changes = []) {
    return { StatusText: status, DateCreated: `/Date(${Date.parse(date)})/`, HasPreviousState: true, Changes: changes };
}

function change(field, previous, current) {
    return { Field: field, PreviousValue: previous, CurrentValue: current };
}

function chartData(rows) {
    return JSON.parse(JSON.stringify(history.buildPriceHistory(rows, options)));
}

const rows = [
    entry('Updated', '2026-11-28T15:00:00Z', [change('Internet price', '22500', '')]),
    entry('Updated', '2026-11-28T15:00:00Z', [change('Retail price', '$24,000', '23500')]),
    entry('Updated', '2026-11-22T15:00:00Z', [change('Retail price', '25000', '24000'), change('Internet price', '23000', '22500')]),
    entry('Added', '2026-11-21T15:00:00Z')
];

const panel = history.renderPanel('TEST1', rows);
assert.equal((panel.match(/class="table inventory-dashboard-history-table w-100"/g) || []).length, 1);
assert.match(panel, /data-history-change-target/);
assert.match(panel, /inventory-dashboard-history-change-row d-none"><td colspan="4">/);
assert.match(panel, /Previous<\/th><th scope="col">Current/);
assert.doesNotMatch(panel, /Price History|<canvas/);
const chart = chartData(rows);
assert.deepEqual(chart.labels, ['Before 2026-11-22', '2026-11-22', '2026-11-28', '2026-11-28']);
assert.equal(chart.tickLabels[0], 'Before');
assert.deepEqual(chart.datasets, [
    { label: 'Retail price', data: [25000, 24000, 23500, 23500] },
    { label: 'Internet price', data: [23000, 22500, 22500, null] }
]);

const reusedStock = [
    ...rows,
    entry('Sold', '2026-11-20T15:00:00Z'),
    entry('Updated', '2026-11-15T15:00:00Z', [change('Retail price', '15000', '14000')])
];
assert.deepEqual(chartData(reusedStock).datasets[0].data,
    [15000, 14000, null, 25000, 24000, 23500, 23500]);

const mileageUpdate = [entry('Updated', '2026-11-28T15:00:00Z', [change('Mileage', '10000', '10100')])];
assert.equal(history.buildPriceHistory(mileageUpdate), null);
assert.equal(history.buildPriceHistory([rows[3]]), null);

const rendered = history.renderPriceHistoryChart({}, chart);
assert.equal(rendered.config.type, 'line');
assert.ok(rendered.config.data.datasets.every(series => series.stepped === false));
assert.equal(rendered.config.options.plugins.legend.align, 'end');
assert.equal(rendered.config.options.scales.y.ticks.callback(25000), '$25,000');

console.log('PASS: Price History line chart, same-day updates, missing prices, stock reuse, and expanded history scope.');
