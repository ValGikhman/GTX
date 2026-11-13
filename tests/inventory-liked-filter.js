const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const read = file => fs.readFileSync(path.join(__dirname, '..', file), 'utf8');
const inventorySource = read('Scripts/inventory.js');
const mainSource = read('Scripts/main.js');
const viewSource = read('Views/Inventory/Index.cshtml');
const detailsSource = read('Views/Inventory/Details.cshtml');

// Evaluate the real functions and bindings, with only their DOM, cookie and storage boundaries doubled.
function functionEnd(source, start) {
    let depth = 0, quote = '', escaped = false;
    for (let index = source.indexOf('{', start); index < source.length; index++) {
        const character = source[index];
        if (quote) {
            if (escaped) escaped = false;
            else if (character === '\\') escaped = true;
            else if (character === quote) quote = '';
        } else if (character === '"' || character === "'" || character === '`') quote = character;
        else if (character === '{') depth++;
        else if (character === '}' && --depth === 0) return index + 1;
    }
    throw new Error('Unclosed function in actual source');
}
function actualFunction(source, name) {
    const start = source.indexOf('function ' + name + '(');
    assert.notEqual(start, -1, 'Missing actual function ' + name);
    return source.slice(start, functionEnd(source, start));
}
class Node {
    constructor(id = '', classes = [], data = {}) {
        this.id = id; this.classes = new Set(classes); this.dataset = data;
        this.attributes = {}; this.style = { display: '' }; this.handlers = [];
        this.value = ''; this.parent = null;
    }
}
const documentNode = new Node('document');
const windowNode = new Node('window');
const button = new Node('', ['filterLiked']); button.parent = documentNode;
const icon = new Node('filterLiked', ['bi-heart']); icon.parent = button;
const last = new Node('filterLast', ['bi-journal']);
const detailsLike = new Node('like-btn', ['bi-heart-fill']);
const term = new Node('filterTerm');
const grid = new Node('inventory');
const empty = new Node('', ['inventory-empty-filter']);
const pageSize = new Node('', ['inventory-page-size-select']); pageSize.value = '20';
const pager = new Node('', ['inventory-pager']);
let cards = [];
function card(stock, badgeDisplay, data = {}) {
    const node = new Node('', ['card'], { stock, ...data });
    node.badge = badgeDisplay === null ? null : new Node('', ['liked']);
    if (node.badge) node.badge.style.display = badgeDisplay;
    return node;
}
function matches(node, selector) {
    return selector.split(',').some(part => {
        part = part.trim();
        return part[0] === '#' ? node.id === part.slice(1) : part[0] === '.' && node.classes.has(part.slice(1));
    });
}
class Collection {
    constructor(nodes) { this.nodes = nodes; this.length = nodes.length; nodes.forEach((node, index) => this[index] = node); }
    each(callback) { this.nodes.forEach((node, index) => callback.call(node, index, node)); return this; }
    map(callback) { return new Collection(this.nodes.map((node, index) => callback.call(node, index, node))); }
    get() { return this.nodes; }
    filter(callback) { return new Collection(this.nodes.filter((node, index) => callback.call(node, index, node))); }
    slice(start, end) { return new Collection(this.nodes.slice(start, end)); }
    first() { return new Collection(this.nodes.slice(0, 1)); }
    data(name) { return this.nodes[0]?.dataset[name]; }
    attr(name, value) { if (arguments.length === 1) return this.nodes[0]?.attributes[name]; return this.each(function () { this.attributes[name] = value; }); }
    val(value) { if (!arguments.length) return this.nodes[0]?.value; return this.each(function () { this.value = value; }); }
    hasClass(name) { return !!this.nodes[0]?.classes.has(name); }
    addClass(names) { return this.each(function () { names.split(/\s+/).forEach(name => this.classes.add(name)); }); }
    removeClass(names) { return this.each(function () { names.split(/\s+/).forEach(name => this.classes.delete(name)); }); }
    show() { return this.each(function () { this.style.display = this.classes.has('liked') ? 'inline-flex' : ''; }); }
    hide() { return this.each(function () { this.style.display = 'none'; }); }
    toggle(show) { return show ? this.show() : this.hide(); }
    find(selector) { assert.equal(selector, '.liked'); return new Collection(this.nodes.flatMap(node => node.badge ? [node.badge] : [])); }
    focus() { return this; }
    on(events, selector, callback) {
        if (typeof selector === 'function') { callback = selector; selector = null; }
        return this.each(function () { events.split(/\s+/).forEach(event => this.handlers.push({ event: event.split('.')[0], selector, callback })); });
    }
}
function $(selector) {
    if (selector === context.window) return new Collection([windowNode]);
    if (selector === context.document) return new Collection([documentNode]);
    if (selector instanceof Node) return new Collection([selector]);
    if (selector === '#inventory > li.card' || selector === '#inventory > li' || selector === '.card') return new Collection(cards);
    return new Collection([icon, button, last, detailsLike, term, grid, empty, pageSize, pager].filter(node => matches(node, selector)));
}
$.trim = value => String(value).trim();
$.inArray = (value, values) => values.indexOf(value);
$.each = (values, callback) => { for (const [key, value] of Object.entries(values)) if (callback(key, value) === false) break; };
const cookies = new Map();
const storage = new Map();
const timers = [];
let selected = {}, ranges = {}, applies = 0, toggles = 0, pagerState;
const context = vm.createContext({
    $, console, likedCars: [], cookieLike: 'fixtureLiked', lastCars: [], detailsNavigationStarted: false,
    Cookies: {
        get: name => cookies.get(name),
        set(name, value, options) { assert.equal(options.path, '/'); assert.equal(options.expires, 30); cookies.set(name, value); }
    },
    localStorage: { setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
    document: { getElementById: id => id === 'inventory' ? grid : null, querySelectorAll: selector => $(selector).get() },
    window: { setTimeout: callback => timers.push(callback) },
    selectedByField: () => selected, selectedRangeByField: () => ranges,
    syncInventoryRangeLabels() {}, syncInventoryModelPanel() {}, syncInventoryVisibleCount() {},
    syncInventoryMobileFilterState() {}, updateInventoryFilterCounts() {}, scheduleInventoryCardImageRefresh() {},
    isInventoryDesktopLayout: () => true, syncInventoryPager: (...args) => pagerState = args,
    inventoryAllPageSize: 'all', inventoryPageSizes: [20, 40, 60, 80], inventoryDefaultPageSize: 'all',
    inventoryCurrentPage: 1, inventoryPageSizeSelector: '.inventory-page-size-select', inventoryPagerSelector: '.inventory-pager',
    isCarLast: stock => context.lastCars.includes(stock), loadLastCars() {}, hideSpinner() {}
});
for (const name of ['parseCarCookie', 'saveCarCookie', 'loadLikedCars', 'saveLikedCars', 'isCarLiked', 'updateLiked', 'updateFilterLiked'])
    vm.runInContext(actualFunction(mainSource, name), context);
for (const name of ['applyFilterLiked', 'normalizeInventoryValue', 'parseInventoryNumber', 'splitInventoryTerms',
    'inventoryContainsAllTerms', 'inventoryTermMatches', 'inventoryCheckboxMatches', 'inventoryRangeMatches',
    'inventoryLikedMatches', 'inventoryLastMatches', 'isInventoryCardMatched', 'inventoryMatchedCards',
    'inventoryMatchedStocks', 'syncInventoryMatchedStocks', 'isInventoryAllPageSize', 'validInventoryPageSize',
    'inventoryPageSize', 'inventoryTotalPages', 'isInventoryPagingEnabled', 'applyInventoryPagination'])
    vm.runInContext(actualFunction(inventorySource, name), context);
const panelStart = inventorySource.indexOf('window.applyInventoryPanelFilters = function');
assert.notEqual(panelStart, -1);
vm.runInContext(inventorySource.slice(panelStart, functionEnd(inventorySource, panelStart)) + ';', context);
const applyPanel = context.window.applyInventoryPanelFilters;
context.window.applyInventoryPanelFilters = () => { applies++; applyPanel(); };
const applyLiked = context.applyFilterLiked;
context.applyFilterLiked = () => { toggles++; applyLiked(); };
const active = value => { icon.classes.delete(value ? 'bi-heart' : 'bi-heart-fill'); icon.classes.add(value ? 'bi-heart-fill' : 'bi-heart'); };
const visible = () => cards.filter(node => node.style.display !== 'none').map(node => node.dataset.stock);
const matched = () => storage.has('matchedStocks') ? JSON.parse(storage.get('matchedStocks')) : [];
const plain = value => JSON.parse(JSON.stringify(value));

assert.deepEqual(plain(context.parseCarCookie(undefined)), []);
assert.deepEqual(plain(context.parseCarCookie('')), []);
assert.deepEqual(plain(context.parseCarCookie('0')), ['0']);
assert.deepEqual(plain(context.parseCarCookie(' a-1 |B2, a-1 |../bad| 001 |<bad>')), ['A-1', 'B2', '001']);
cookies.set('fixtureLiked', ' a-1 |B2|321'); context.loadLikedCars();
assert.equal(context.isCarLiked(' a-1 '), true);
assert.equal(context.isCarLiked(321), true, 'Numeric jQuery stock data must match its string cookie');
assert.equal(context.isCarLiked(null), false);
assert.equal(cookies.get('fixtureLiked'), 'A-1|B2|321');
context.likedCars = [' a-1 ', 'A-1', ' b2 ']; context.saveLikedCars();
assert.deepEqual(plain(context.likedCars), ['A-1', 'B2']);

active(true);
for (const display of ['', 'inline', 'inline-flex', 'none', null]) {
    assert.equal(context.inventoryLikedMatches($(card(' a-1 ', display))), true, 'Cookie-liked stock must match independently of badge style ' + display);
    assert.equal(context.inventoryLikedMatches($(card('UNLIKED', display))), false, 'A stale visible badge must not create a saved like');
}
context.likedCars.push('0');
assert.equal(context.isCarLiked(0), true);
assert.equal(context.inventoryLikedMatches($(card(0, 'none'))), true, 'Valid numeric stock zero must survive the panel membership check');
context.likedCars.pop();
active(false);
assert.equal(context.inventoryLikedMatches($(card('UNLIKED', 'none'))), true);

// Exercise the legacy fallback as well as the current panel engine.
cards = [card(' a-1 ', 'inline-flex'), card('B2', 'none'), card('UNLIKED', '')];
context.window.applyInventoryPanelFilters = null;
context.applyFilterLiked(); assert.deepEqual(visible(), [' a-1 ', 'B2']);
context.applyFilterLiked(); assert.deepEqual(visible(), [' a-1 ', 'B2', 'UNLIKED']);
context.window.applyInventoryPanelFilters = () => { applies++; applyPanel(); };

cards = [
    card(' a-1 ', 'inline-flex', { make: 'Toyota', model: 'Camry', color: 'red', price: 10000 }),
    card('B2', 'none', { make: 'Toyota', model: 'Camry', color: 'blue', price: 12000 }),
    card('C3', '', { make: 'Ford', model: 'Focus', color: 'red', price: 9000 }),
    card('D4', '', { make: 'Toyota', model: 'Camry', color: 'red', price: 50000 }),
    card('E5', null, { make: 'Toyota', model: 'Camry', color: 'red', price: 11000 }),
    card('F6', 'inline-flex', { make: 'Toyota', model: 'Camry', color: 'red', price: 11000 })
];
context.likedCars = ['A-1', 'B2', 'D4', 'E5'];
selected = { make: ['TOYOTA'] }; ranges = { price: { min: 8000, max: 15000 } }; term.value = 'RED CAMRY';
context.applyFilterLiked(); assert.deepEqual(matched(), [' a-1 ', 'E5']);
assert.deepEqual(visible(), [' a-1 ', 'E5'], 'Liked, checkbox, range and text filters must intersect');
last.classes.add('bi-journal-album'); context.lastCars = ['E5'];
context.window.applyInventoryPanelFilters(); assert.deepEqual(matched(), ['E5']);
last.classes.delete('bi-journal-album');
context.applyFilterLiked(); assert.deepEqual(matched(), [' a-1 ', 'E5', 'F6'], 'Turning likes off must preserve the other filters');

selected = {}; ranges = {}; term.value = '';
cards = Array.from({ length: 25 }, (_, index) => card('STOCK-' + index, index % 2 ? 'none' : 'inline-flex'));
context.likedCars = cards.slice(0, 22).map(node => node.dataset.stock);
context.applyFilterLiked();
assert.equal(visible().length, 20); assert.equal(matched().length, 22);
context.inventoryCurrentPage = 2; context.applyInventoryPagination();
assert.deepEqual(visible(), ['STOCK-20', 'STOCK-21']);
assert.equal(matched().length, 22, 'Details navigation must retain every matching liked stock, including other pages');
assert.equal(pagerState[0], 22);

// Use actual main and inventory bindings, including event bubbling from icon to button/document.
const bindingsStart = mainSource.indexOf('    $("#filterTerm").on("input"');
const bindingsEnd = mainSource.indexOf('    const i18n', bindingsStart);
assert.ok(bindingsStart >= 0 && bindingsEnd > bindingsStart);
vm.runInContext(mainSource.slice(bindingsStart, bindingsEnd), context);
const delegateStart = inventorySource.indexOf('        $(document).on("input", "#filterTerm"');
const delegateEnd = inventorySource.indexOf('        $(document).on("click", "#inventoryMobileFilterToggle"', delegateStart);
assert.ok(delegateStart >= 0 && delegateEnd > delegateStart);
vm.runInContext(inventorySource.slice(delegateStart, delegateEnd), context);
function click(target) {
    const event = { target, prevented: false, stopped: false, preventDefault() { this.prevented = true; }, stopPropagation() { this.stopped = true; } };
    for (let node = target; node; node = event.stopped ? null : node.parent) {
        for (const handler of node.handlers.filter(handler => handler.event === 'click')) {
            if (!handler.selector) handler.callback.call(node, event);
            else for (let matchedNode = target; matchedNode && matchedNode !== node; matchedNode = matchedNode.parent)
                if (matches(matchedNode, handler.selector)) handler.callback.call(matchedNode, event);
        }
    }
    while (timers.length) timers.shift()();
    return event;
}
for (const target of [button, icon, button, icon]) {
    const before = { applies, toggles, active: icon.classes.has('bi-heart-fill') };
    assert.equal(click(target).prevented, true);
    assert.equal(toggles, before.toggles + 1, 'Padding and icon clicks must each toggle exactly once');
    assert.equal(applies, before.applies + 1, 'The click must apply the filter once, without a second delegated refresh');
    assert.equal(icon.classes.has('bi-heart-fill'), !before.active);
    assert.equal(button.attributes['aria-pressed'], String(!before.active));
}
const toolbar = read('Views/Shared/_FilterByTerm.cshtml');
assert.match(toolbar, /<button\b(?=[^>]*\bclass="filterLiked\b)(?=[^>]*\btype="button")(?=[^>]*\baria-pressed="false")[^>]*>/);

// A back/forward restoration reloads the fixture cookie and reapplies only changed, active likes.
vm.runInContext(actualFunction(viewSource, 'refreshCarLikes'), context);
const pageshowStart = viewSource.indexOf("$(window).on('pageshow', function");
assert.notEqual(pageshowStart, -1);
vm.runInContext(viewSource.slice(pageshowStart, functionEnd(viewSource, pageshowStart)) + ');', context);
const pageshow = () => windowNode.handlers.filter(handler => handler.event === 'pageshow').forEach(handler => handler.callback({ originalEvent: { persisted: true } }));
context.likedCars = ['STOCK-0', 'STOCK-1']; cookies.set('fixtureLiked', 'STOCK-1|STOCK-0');
active(true); context.inventoryCurrentPage = 2;
let before = applies; pageshow();
assert.equal(applies, before, 'Unchanged likes, including a reordered cookie, must preserve the current page');
assert.equal(context.inventoryCurrentPage, 2);
cookies.set('fixtureLiked', 'STOCK-2'); pageshow();
assert.equal(applies, before + 1); assert.deepEqual(matched(), ['STOCK-2']);
assert.deepEqual(visible(), ['STOCK-2']); assert.equal(context.inventoryCurrentPage, 1);
active(false); before = applies; cookies.set('fixtureLiked', 'STOCK-3'); pageshow();
assert.equal(applies, before, 'A restored page with likes disabled must not reset unrelated filtering/pagination');
assert.equal(context.isCarLiked('STOCK-3'), true);
active(true); cookies.set('fixtureLiked', ''); context.loadLikedCars(); context.window.applyInventoryPanelFilters();
assert.deepEqual(matched(), []); assert.deepEqual(visible(), []);
assert.equal(storage.has('matchedStocks'), false, 'Empty likes must clear stale details-navigation matches');
assert.equal(empty.style.display, '');

// Details must canonicalize only the liked-cookie key; its shared stock still identifies raw navigation entries.
const stockInitialization = detailsSource.match(/^\s*var stock = ".+$/m);
assert.ok(stockInitialization);
assert.match(stockInitialization[0], /JavaScriptStringEncode\(Model\.CurrentVehicle\.VehicleDetails\.Stock \?\? string\.Empty\)/,
    'Keep the original escaped stock value for matchedStocks navigation');
const detailsHandlerStart = detailsSource.indexOf('$("#like-btn").on("click"');
assert.notEqual(detailsHandlerStart, -1);
vm.runInContext(detailsSource.slice(detailsHandlerStart, functionEnd(detailsSource, detailsHandlerStart)) + ');', context);
const rawStock = ' abc-1 ';
context.stock = rawStock;
context.likedCars = ['ABC-1', 'OTHER'];
const savedNavigationStocks = [rawStock, 'next-stock'];
click(detailsLike);
assert.deepEqual(plain(context.likedCars), ['OTHER'], 'A padded lowercase Details stock must remove its existing canonical like');
assert.equal(context.stock, rawStock);
assert.equal(savedNavigationStocks.indexOf(context.stock), 0, 'Toggling a like must preserve the raw Details navigation key');
assert.equal(detailsLike.classes.has('bi-heart'), true);
assert.equal(cookies.get('fixtureLiked'), 'OTHER');
click(detailsLike);
assert.deepEqual(plain(context.likedCars), ['OTHER', 'ABC-1']);
assert.equal(detailsLike.classes.has('bi-heart-fill'), true);
assert.equal(context.stock, rawStock);
assert.equal(cookies.get('fixtureLiked'), 'OTHER|ABC-1');

console.log('PASS inventory liked filter: badge-independent cookie membership, numeric/zero stock normalization, legacy/panel intersections, pagination/matched stocks, single button/icon toggles, restored-page state, and Details likes without changing the navigation stock');
