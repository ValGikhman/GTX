const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Scripts/inventory.js'), 'utf8');
const main = fs.readFileSync(path.join(__dirname, '../Scripts/main.js'), 'utf8');
function between(text, start, end) {
    const from = text.indexOf(start);
    const to = text.indexOf(end, from);
    assert.ok(from >= 0 && to > from, 'Locate the shipped event handlers');
    return text.slice(from, to);
}

function fixture({ serverFilter = true, storageDenied = false, routeType = '' } = {}) {
    const handlers = new Map();
    const pending = [];
    const session = new Map([['term', 'BMW'], ['lastVisitedPage', '/Inventory/Index']]);
    const local = new Map([['matchedStocks', '["STOCK1"]'], ['likedCars', '["FAVORITE"]'], ['lastCars', '["RECENT"]']]);
    const window = { location: { href: '/current' }, applyInventoryPanelFilters() { this.filtered = true; } };
    const document = {};
    const term = { value: 'BMW' };
    const filters = {
        '#filterTerm': { value: 'BMW' },
        '.inventory-filter-check': { checked: true },
        '#filterLiked': { classes: new Set(['bi-heart-fill']) },
        '#filterLast': { classes: new Set(['bi-journal-album']) },
        '.filterLiked': { attributes: { 'aria-pressed': 'true' } }
    };
    const state = { rangesReset: 0 };
    function $(target) {
        const key = target === document ? 'document' : target === window ? 'window' : target;
        const elements = typeof target === 'string'
            ? target.split(',').map(selector => selector.trim()).map(selector => {
                if (selector === '#term') return term;
                return filters[selector] || (filters[selector] = {});
            }) : [target || {}];
        return {
            on(event, selector, callback) {
                handlers.set(`${key}:${event}:${typeof selector === 'string' ? selector : ''}`,
                    callback || selector);
                return this;
            },
            trigger(event) { handlers.get(`${key}:${event}:`)(); return this; },
            data(name) { return name === 'server-stock-filter' ? serverFilter : '/Inventory/All'; },
            closest() { return { length: target && target.isMenu ? 1 : 0 }; },
            val(value) {
                if (!arguments.length) return elements[0].value || '';
                elements.forEach(element => { element.value = value; });
                return this;
            },
            prop(name, value) { elements.forEach(element => { element[name] = value; }); return this; },
            removeClass(names) {
                elements.forEach(element => names.split(/\s+/).forEach(name => element.classes?.delete(name)));
                return this;
            },
            addClass(names) {
                elements.forEach(element => {
                    element.classes ||= new Set();
                    names.split(/\s+/).forEach(name => element.classes.add(name));
                });
                return this;
            },
            attr(name, value) {
                elements.forEach(element => {
                    element.attributes ||= {};
                    element.attributes[name] = value;
                });
                return this;
            },
            focus() { return this; }
        };
    }
    $.post = function (url, data) {
        const request = { url, data };
        pending.push(request);
        return {
            done(callback) { request.complete = callback; return this; },
            fail() { return this; }
        };
    };
    function storage(values) {
        return { removeItem(key) { if (storageDenied) throw new Error('Storage disabled'); values.delete(key); } };
    }
    const context = vm.createContext({
        $, window, document, root: '/', console,
        sessionStorage: storage(session), localStorage: storage(local),
        routeTypeFilter: () => routeType, resetInventoryRanges: () => { state.rangesReset++; }
    });
    vm.runInContext(source.slice(0, source.indexOf('function applyFilterTerm')), context);
    vm.runInContext(between(source,
        '        var hasServerInventoryFilter =', '        syncRouteTypeFilter();'), context);
    vm.runInContext(between(source,
        '        $(document).on("click", "#inventoryMobileClearFilters, #inventoryEmptyReset"',
        '        $(document).on("click", "#inventoryMobileCloseCanvas"'), context);
    vm.runInContext(between(main, '    $("#term")', '    $(document).on("click", "#btnTermSearch"'), context);
    return { handlers, context, pending, window, term, session, local, filters, state };
}

function event(overrides = {}) {
    return {
        prevented: false, stopped: false,
        preventDefault() { this.prevented = true; },
        stopPropagation() { this.stopped = true; },
        ...overrides
    };
}

for (const storageDenied of [false, true]) {
    const f = fixture({ storageDenied });
    f.context.applyTerm('BMW');
    const click = event();
    f.handlers.get('document:click:a[data-inventory-reset]')(click);
    assert.equal(click.prevented, false, 'The menu retains native link navigation');
    assert.equal(f.context.inventoryAllNavigationPending, true);
    if (!storageDenied) assert.equal(f.session.has('term'), false);
    assert.equal(f.session.get('lastVisitedPage'), '/Inventory/Index');
    assert.equal(f.local.get('likedCars'), '["FAVORITE"]');
    assert.equal(f.local.get('matchedStocks'), '["STOCK1"]');
    f.pending[0].complete({ redirectUrl: '/Inventory?search=old' });
    assert.equal(f.window.location.href, '/current', 'A late search cannot override All Inventory navigation');
    f.handlers.get('window:pageshow:')();
    f.context.applyTerm('AUDI');
    f.pending[1].complete({ redirectUrl: '/Inventory?search=new' });
    assert.equal(f.window.location.href, '/Inventory?search=new', 'Search works after returning through browser history');
}

for (const modifier of ['ctrlKey', 'metaKey', 'shiftKey', 'altKey', 'which']) {
    const f = fixture();
    f.handlers.get('document:click:a[data-inventory-reset]')(event({ [modifier]: modifier === 'which' ? 2 : true }));
    assert.equal(f.context.inventoryAllNavigationPending, false, 'Opening a separate tab does not disable this tab\'s search');
    assert.equal(f.session.get('term'), 'BMW');
}

const blur = fixture();
blur.handlers.get('#term:blur:').call(blur.term, event({ relatedTarget: { isMenu: true } }));
assert.equal(blur.pending.length, 0, 'Focusing the inventory menu does not submit a blur search');
blur.handlers.get('#term:blur:').call(blur.term, event({ relatedTarget: {} }));
assert.equal(blur.pending.length, 1, 'Ordinary blur search is preserved');
blur.handlers.get('#term:keydown:').call(blur.term, event({ key: 'Enter' }));
assert.equal(blur.pending.length, 2, 'Enter still submits search');

for (const storageDenied of [false, true]) {
    for (const mode of ['client', 'server', 'category']) {
        const f = fixture({ storageDenied, serverFilter: mode === 'server', routeType: mode === 'category' ? 'SUV' : '' });
        const click = event();
        f.handlers.get('document:click:#inventoryMobileClearFilters, #inventoryEmptyReset')(click);
        assert.equal(f.filters['#filterTerm'].value, '', `${mode}: Reset clears the in-page search text`);
        if (!storageDenied) assert.equal(f.session.has('term'), false, `${mode}: Reset clears the saved search text`);
        assert.equal(f.filters['.inventory-filter-check'].checked, false, `${mode}: Reset clears checked filters`);
        assert.equal(f.state.rangesReset, 1, `${mode}: Reset restores filter ranges`);
        assert.equal(f.filters['#filterLiked'].classes.has('bi-heart-fill'), false, `${mode}: Liked-only filtering is disabled`);
        assert.equal(f.filters['#filterLast'].classes.has('bi-journal-album'), false, `${mode}: Recent-only filtering is disabled`);
        assert.equal(f.filters['.filterLiked'].attributes['aria-pressed'], 'false');
        assert.equal(f.local.get('likedCars'), '["FAVORITE"]', 'Reset preserves saved favorites');
        assert.equal(f.local.get('lastCars'), '["RECENT"]', 'Reset preserves browsing history');
        assert.equal(click.prevented, true);
        assert.equal(click.stopped, true);
        if (mode === 'client') {
            assert.equal(f.window.location.href, '/current', 'An in-page zero-match Reset restores matches without navigation');
            assert.equal(f.window.filtered, true, 'The displayed inventory is filtered again after clearing');
        } else {
            assert.equal(f.window.location.href, '/Inventory/All', 'Server and category Reset reloads full inventory even without storage');
        }
    }
}

const ordinary = fixture({ serverFilter: false });
ordinary.handlers.get('#inventoryClearFilters:click:')();
assert.equal(ordinary.window.location.href, '/current', 'Ordinary client filters clear without a reload');
assert.equal(ordinary.window.filtered, true);

const view = fs.readFileSync(path.join(__dirname, '../Views/Inventory/Index.cshtml'), 'utf8');
const emptyMessage = between(view, '<div class="inventory-empty-filter', '<ul id="inventory"');
assert.doesNotMatch(emptyMessage, /if\s*\(/, 'Every empty search exposes Reset, including client-side no matches');
assert.match(emptyMessage, /id="inventoryEmptyReset"/, 'Reset is outside the nonempty mobile toolbar');
const layout = fs.readFileSync(path.join(__dirname, '../Views/Shared/_Layout.cshtml'), 'utf8');
assert.equal((layout.match(/href="~\/Inventory\/All" data-inventory-reset/g) || []).length, 2,
    'Desktop and mobile inventory menus opt into reset navigation');
console.log('PASS: All Inventory navigation, preserved searches, late response suppression, zero-match Reset and denied storage.');
