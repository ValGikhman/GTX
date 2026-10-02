// Run with node tests/specials-player.js. Exercise rotation without a browser or database fixtures.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync(require('node:path').join(__dirname, '../Scripts/specials-player.js'), 'utf8');

function load(offers) {
    const links = [0, 1].map(() => ({ attrs: { href: '/Specials/List', 'data-specials-feed': '/Specials/Links', 'data-specials-url': '/Specials/Special/__id__' }, text: 'Specials', measurements: [] }));
    const timers = [];
    function wrap(items) {
        return {
            length: items.length,
            items: items,
            text: function (value) { items.forEach(item => { item.text = value; }); return this; },
            append: element => items.forEach(item => item.measurements.push(element.items[0])),
            first: () => wrap(items.slice(0, 1)),
            each: fn => items.forEach(item => fn.call(item)),
            attr: function (key) {
                if (typeof key === 'string') return items[0].attrs[key];
                items.forEach(item => Object.assign(item.attrs, key));
            },
            find: selector => ({ text: value => items.forEach(item => {
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
    $.getJSON = () => ({ done: callback => callback(offers) });
    vm.runInNewContext(source, { jQuery: $, window: { setInterval: (callback, delay) => timers.push({ callback, delay }) } });
    return { links, timers };
}

const multiple = load([{ id: 8, title: 'Warranty' }, { id: 12, title: '<b>Summer savings</b>' }]);
assert.equal(multiple.timers.length, 1);
assert.equal(multiple.timers[0].delay, 10000);
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
const single = load([{ id: 8, title: 'Warranty' }]);
assert.equal(single.timers.length, 0);
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
[[], null, [{ id: -1, title: 'Invalid' }]].forEach(offers => {
    const result = load(offers);
    assert.equal(result.timers.length, 0);
    assert.equal(result.links[0].attrs.href, '/Specials/List');
});
console.log('PASS: synchronized titles and destinations, 10-second interval, loop wrap, literal text, single-offer and empty fallback.');
