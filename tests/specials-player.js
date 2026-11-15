// Run with node tests/specials-player.js. Exercise rotation without a browser or database fixtures.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync(require('node:path').join(__dirname, '../Scripts/specials-player.js'), 'utf8');

function load(offers, requestFails = false) {
    const links = ['home-hero', 'specials-player desktop', 'specials-player mobile'].map(className => {
        const player = {
            attrs: { class: className, 'data-specials-player': '' },
            controls: [-1, 1].map(step => ({ attrs: { 'data-specials-step': String(step) }, hidden: true, handlers: {} }))
        };
        return { player, attrs: { href: '/Specials/List', 'data-specials-feed': '/Specials/Links', 'data-specials-url': '/Specials/Special/__id__' }, text: 'Specials', measurements: [] };
    });
    const controls = links.flatMap(link => link.player.controls);
    const timers = [];
    function wrap(items) {
        return {
            length: items.length,
            items: items,
            text: function (value) { items.forEach(item => { item.text = value; }); return this; },
            append: function (element) { items.forEach(item => item.measurements.push(element.items[0])); return this; },
            first: () => wrap(items.slice(0, 1)),
            each: fn => items.forEach(item => fn.call(item)),
            attr: function (key, value) {
                if (typeof key === 'string' && value === undefined) return items[0].attrs[key];
                items.forEach(item => Object.assign(item.attrs, typeof key === 'string' ? { [key]: value } : key));
                return this;
            },
            closest: selector => {
                assert.equal(selector, '[data-specials-player]', 'Controls must work on the existing hero without a specials-player wrapper');
                return wrap(items.map(item => {
                    assert.ok(Object.hasOwn(item.player.attrs, 'data-specials-player'));
                    return item.player;
                }));
            },
            prop: function (name, value) { items.forEach(item => { item[name] = value; }); return this; },
            on: function (name, handler) { items.forEach(item => { item.handlers[name] = handler; }); return this; },
            find: selector => selector === '.specials-player-control'
                ? wrap(items.flatMap(item => item.controls))
                : ({ text: value => items.forEach(item => {
                    if (selector === '.specials-player-link-counter') item.counter = value;
                    else item.text = value;
                }) })
        };
    }
    function $(value, attrs) {
        if (typeof value === 'function') return value();
        if (value === '<span>') return wrap([{ attrs: attrs, text: '' }]);
        return wrap(typeof value === 'string' ? links : [value]);
    }
    $.getJSON = () => ({ done: function (callback) { if (!requestFails) callback(offers); return this; } });
    vm.runInNewContext(source, { jQuery: $, window: {
        setInterval: (callback, delay) => { timers.push({ callback, delay, active: true }); return timers.length; },
        clearInterval: id => { assert.ok(timers[id - 1]); timers[id - 1].active = false; }
    } });
    return {
        links, controls, timers,
        activeTimers: () => timers.filter(timer => timer.active),
        click: (playerIndex, step) => {
            const control = links[playerIndex].player.controls.find(item => item.attrs['data-specials-step'] === String(step));
            assert.equal(control.hidden, false, 'Only visible controls should be clickable');
            assert.equal(typeof control.handlers.click, 'function');
            let prevented = false;
            control.handlers.click.call(control, { preventDefault() { prevented = true; } });
            assert.equal(prevented, true, 'Manual navigation must not follow the offer link');
        }
    };
}

const multiple = load([{ id: 8, title: 'Warranty' }, { id: 12, title: '<b>Summer savings</b>' }]);
assert.equal(multiple.timers.length, 1);
assert.equal(multiple.timers[0].delay, 10000);
assert.ok(multiple.controls.every(control => control.hidden === false));
assert.equal(multiple.links.length, 3);
assert.equal(multiple.links[0].player.attrs.class, 'home-hero');
function expectOffer(title, id) {
    multiple.links.forEach(link => {
        assert.equal(link.text, title);
        assert.equal(link.attrs.href, '/Specials/Special/' + id);
        assert.equal(link.attrs['aria-label'], 'View special: ' + title);
    });
}
expectOffer('Warranty', 8);
assert.equal(multiple.links[0].counter, '1 / 2');
multiple.timers[0].callback();
expectOffer('<b>Summer savings</b>', 12);
assert.equal(multiple.links[0].counter, '2 / 2');
multiple.timers[0].callback();
expectOffer('Warranty', 8);
function manualStep(playerIndex, step, title, id, counter) {
    const previous = multiple.activeTimers()[0];
    multiple.click(playerIndex, step);
    expectOffer(title, id);
    multiple.links.forEach(link => assert.equal(link.counter, counter));
    assert.equal(previous.active, false, 'Manual navigation must reset the existing countdown');
    assert.equal(multiple.activeTimers().length, 1, 'Repeated clicks must never accumulate timers');
    assert.equal(multiple.activeTimers()[0].delay, 10000);
}
manualStep(0, -1, '<b>Summer savings</b>', 12, '2 / 2');
manualStep(1, 1, 'Warranty', 8, '1 / 2');
manualStep(2, -1, '<b>Summer savings</b>', 12, '2 / 2');
manualStep(2, 1, 'Warranty', 8, '1 / 2');
manualStep(0, 1, '<b>Summer savings</b>', 12, '2 / 2');
manualStep(1, 1, 'Warranty', 8, '1 / 2');
multiple.activeTimers()[0].callback();
expectOffer('<b>Summer savings</b>', 12);
assert.equal(multiple.activeTimers().length, 1);
const three = load([{ id: 1, title: 'First' }, { id: 2, title: 'Second' }, { id: 3, title: 'Third' }]);
[[0, -1, 3], [1, -1, 2], [2, 1, 3], [0, 1, 1]].forEach(([player, step, id]) => {
    three.click(player, step);
    three.links.forEach(link => assert.equal(link.attrs.href, '/Specials/Special/' + id));
});
assert.equal(three.activeTimers().length, 1);
const single = load([{ id: 8, title: 'Warranty' }]);
assert.equal(single.timers.length, 0);
assert.ok(single.controls.every(control => control.hidden && !control.handlers.click));
single.links.forEach(link => {
    assert.equal(link.counter, '');
    assert.equal(link.attrs['data-specials-position'], '');
    assert.equal(link.text, 'Warranty');
    assert.equal(link.attrs.href, '/Specials/Special/8');
});
multiple.links.forEach(link => {
    assert.deepEqual(link.measurements.map(item => item.text), ['Warranty', '<b>Summer savings</b>']);
    assert.ok(link.measurements.every(item => item.attrs['aria-hidden'] === 'true'));
});
[[], null, [{ id: -1, title: 'Invalid' }], [{ id: 8, title: '' }]].forEach(offers => {
    const result = load(offers);
    assert.equal(result.timers.length, 0);
    assert.equal(result.links[0].attrs.href, '/Specials/List');
    assert.ok(result.controls.every(control => control.hidden && !control.handlers.click));
});
const failed = load([{ id: 8, title: 'Unused' }], true);
assert.equal(failed.timers.length, 0);
assert.ok(failed.controls.every(control => control.hidden));
assert.equal(failed.links[0].attrs.href, '/Specials/List');
const filteredSingle = load([{ id: -1, title: 'Invalid' }, { id: 8, title: 'Warranty' }, { id: 9, title: '' }]);
assert.equal(filteredSingle.timers.length, 0);
assert.ok(filteredSingle.controls.every(control => control.hidden));
console.log('PASS: wrapper-free home hero and inventory controls, synchronized automatic/manual loops, previous/next wrap, countdown reset without extra timers, literal text, single-offer and empty/failed-feed fallback.');
