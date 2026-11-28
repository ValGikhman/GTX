const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Scripts/addTools/addGrid.js'), 'utf8');
const context = { jQuery: { fn: {} }, window: {} };
vm.runInNewContext(source.replace('}(jQuery));', 'globalThis.GridForTest = AddGrid; }(jQuery));'), context);

const statusColumn = { field: 'Status', filterField: 'StatusFilter', filterMultiple: true };
const grid = Object.create(context.GridForTest.prototype);
grid._opts = { columns: [statusColumn, { field: 'Location' }] };
grid._htmlFields = {};

const addedAndUpdated = { StatusFilter: ['Added', 'Updated'], Location: 'NY' };
const updated = { StatusFilter: ['Updated'], Location: 'NJ' };
const sold = { StatusFilter: ['Sold'], Location: 'NY' };
const none = { StatusFilter: ['None'], Location: 'NY' };
const rows = [addedAndUpdated, updated, sold, none];

grid._filterState = { Status: new Set(['Updated']) };
assert.deepEqual(grid._applyFilter(rows), [addedAndUpdated, updated]);
grid._filterState = { Status: new Set(['Updated', 'Sold']), Location: new Set(['NY']) };
assert.deepEqual(grid._applyFilter(rows), [addedAndUpdated, sold]);
grid._filterState = { Status: new Set(['None']) };
assert.deepEqual(grid._applyFilter(rows), [none]);
grid._filterState = { Location: new Set(['NJ']) };
assert.deepEqual(grid._applyFilter(rows), [updated]);

statusColumn.filterMultiple = false;
grid._filterState = { Status: new Set(['Added,Updated']) };
assert.deepEqual(grid._applyFilter(rows), [addedAndUpdated]);

console.log('PASS: AddGrid individual status matching, multiple selections, combined filters, and single-value columns.');
