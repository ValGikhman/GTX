const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Scripts/chat-bot.js'), 'utf8');
const textNode = value => ({ nodeType: 3, value: String(value), parent: null });
class Element {
    constructor(tag, attributes = {}) {
        this.nodeType = 1;
        this.tag = tag;
        this.attributes = { ...attributes };
        this.classes = new Set((attributes.class || '').split(' ').filter(Boolean));
        this.children = [];
        this.length = 1;
        if (attributes.text != null) this.append(textNode(attributes.text));
    }
    append(...children) {
        children.flatMap(child => child instanceof Collection ? child.elements : [child]).forEach(child => {
            if (typeof child === 'string') child = textNode(child);
            child.parent = this;
            this.children.push(child);
        });
        return this;
    }
    prepend(...children) {
        const original = this.children;
        this.children = [];
        this.append(...children);
        this.children.push(...original);
        return this;
    }
    appendTo(parent) { parent.append(this); return this; }
    addClass(value) { this.classes.add(value); return this; }
    toggleClass(value, active) { active ? this.classes.add(value) : this.classes.delete(value); return this; }
    attr(name, value) { if (arguments.length === 1) return this.attributes[name]; this.attributes[name] = value; return this; }
    text(value) {
        if (!arguments.length) return this.children.map(child => child.nodeType === 3 ? child.value : child.text()).join('');
        this.children = [];
        return this.append(textNode(value));
    }
    find(selector) { return new Collection(walk(this).filter(element => selector[0] === '.' ? element.classes.has(selector.slice(1)) : element.tag === selector)); }
    contents() { return new Collection(this.children.slice()); }
    remove() { this.removed = true; if (this.parent) this.parent.children = this.parent.children.filter(child => child !== this); return this; }
    on() { return this; }
    prop(name, value) { this[name] = value; return this; }
    trigger(event) { this.lastEvent = event; return this; }
    val(value) { if (!arguments.length) return this.value; this.value = value; return this; }
    data(name) { return this.attributes[name]; }
}
class Collection {
    constructor(elements) { this.elements = elements; this.length = elements.length; }
    filter(predicate) { return new Collection(this.elements.filter(element => predicate.call(element))); }
    remove() { this.elements.forEach(element => {
        if (element.nodeType === 1) element.remove();
        else if (element.parent) element.parent.children = element.parent.children.filter(child => child !== element);
    }); return this; }
    text(value) { return arguments.length ? (this.elements.forEach(element => element.text(value)), this) : this.elements[0]?.text() || ''; }
    find(selector) { return new Collection(this.elements.flatMap(element => element.find(selector).elements)); }
    contents() { return new Collection(this.elements.flatMap(element => element.children)); }
    prepend(...children) { this.elements.forEach(element => element.prepend(...children)); return this; }
}
function walk(root) { return root.children.flatMap(child => child.nodeType === 1 ? [child, ...walk(child)] : []); }
function elements(root, tag) { return walk(root).filter(element => element.tag === tag); }
function hasClass(root, name) { return walk(root).filter(element => element.classes.has(name)); }

class Request {
    constructor(options) { this.options = options; }
    done(callback) { this.success = callback; return this; }
    fail(callback) { this.failure = callback; return this; }
    always(callback) { this.finished = callback; return this; }
    resolve(data) { this.success?.(data); this.finished?.(); }
    reject(error) { this.failure?.(error); this.finished?.(); }
    abort() { this.aborted = true; this.failure?.({ statusText: 'abort' }); }
}
const requests = [];
const $ = (markup, attributes) => {
    assert.match(markup, /^<\w+>$/, 'Build literal DOM elements; never interpolate provider HTML');
    return new Element(markup.slice(1, -1), attributes);
};
$.trim = value => String(value || '').trim();
$.ajax = options => { const request = new Request(options); requests.push(request); return request; };
const timers = new Map();
let timerId = 0;
const messages = [];
const token = new Element('input').val('test-request-token');
const form = new Element('form');
form.find = selector => { assert.equal(selector, "input[name='ChatRequestToken']"); return token; };
const messageInput = new Element('input');
const context = vm.createContext({
    $, marketTabSequence: 0, sending: false, responseId: null,
    $chatForm: form, $message: messageInput,
    $widget: new Element('widget', { 'market-progress-url': '/ChatProgress/Market', 'message-url': '/Chat/Message' }),
    document: { createTextNode: textNode },
    window: {
        crypto: { getRandomValues(bytes) { bytes.fill(171); return bytes; } },
        setTimeout(callback) { const id = ++timerId; timers.set(id, callback); return id; },
        clearTimeout(id) { timers.delete(id); }
    },
    rememberRequest() {}, requestsContactForm() { return false; },
    isSavedCommandPhrase() { return false; }, navigationRequest() { return null; },
    setSending(value) { context.sending = value; },
    addMessage(role, text, extraClass, vehicles, totalMatches, persist) {
        const element = new Element('message');
        const copy = new Element('div', { class: 'gtx-chat-copy', text });
        element.append(copy);
        if (extraClass === 'is-thinking') copy.append(new Element('span', { class: 'gtx-chat-thinking-spinner' }));
        messages.push({ role, text, extraClass, persist, element });
        return element;
    },
    errorMessage(xhr, fallback) { return fallback; }, handleServerNavigation() {}
});
function load(startMarker, endMarker) {
    const start = source.indexOf(startMarker), end = source.indexOf(endMarker, start);
    assert.ok(start >= 0 && end > start, 'Exercise actual shipped functions');
    vm.runInContext(source.slice(start, end), context);
}
load('        function appendMarketSource(', '        function openVinReport(');
load('        function updateMarketProgress(', '        function resetChat(');

const listings = [
    { Vehicle: '<img src=x onerror=alert(1)> Accord EX-L', Vin: '1HGCV1F53MA093993', Ask: '$25,563', Odometer: '39,137', Distance: '20', DaysListed: '102', Location: 'First Dealer', Url: 'https://dealer.example/car', Scope: 'Within scope' },
    { Vehicle: '2021 Honda Accord EX-L', Vin: '1HGCV1F53MA050691', Ask: '$18,995', Odometer: '33,697', Distance: '5', DaysListed: '0', Location: 'Second Dealer', Url: 'javascript:alert(1)' }
];
const original = JSON.stringify(listings);
const container = new Element('container');
context.appendMarketReply(container, {
    Kicker: 'MarketCheck appraisal', Title: '2021 Honda Accord EX-L', Context: '42,000 miles',
    Metrics: [{ Label: 'Retail estimate', Value: '$25,444', IsPrimary: true }],
    Sections: [], Listings: listings, ListingGroups: [], PreserveListingOrder: true,
    Note: 'Prices on display,\nConfirm them today.'
});
const card = container.children[0];
assert.ok(card.children.findIndex(child => child.classes?.has('bot-market-metrics')) < card.children.findIndex(child => child.classes?.has('bot-market-listings-wrap')), 'Appraisal metrics precede comparables');
const table = elements(card, 'table')[0];
assert.equal(table.parent.attributes.tabindex, 0, 'Wide appraisal comparables remain keyboard-scrollable');
const rows = table.children.find(child => child.tag === 'tbody').children;
assert.equal(elements(rows[0], 'a')[0].attributes.href, 'https://dealer.example/car');
assert.ok(rows[0].text().includes('$25,563') && rows[2].text().includes('$18,995'), 'Backend mileage ranking survives even when price order differs');
assert.deepEqual(elements(table, 'thead')[0].children[0].children.map(cell => cell.text()), ['Vehicle', 'Ask', 'Miles', 'Distance', 'Days listed']);
assert.ok(!elements(table, 'th').some(cell => cell.attributes['aria-sort']), 'Preserved order does not falsely claim price sorting');
assert.equal(hasClass(rows[0], 'bot-market-listing-vin')[0].text(), listings[0].Vin);
assert.equal(elements(rows[2], 'td').at(-1).text(), '0', 'A newly listed vehicle keeps zero days');
assert.equal(rows[1].children[0].attributes.colspan, 5, 'Scope badges span the optional days column');
assert.equal(elements(card, 'img').length, 0, 'Provider text remains text');
assert.equal(elements(rows[2], 'a').length, 0, 'Unsafe listing URLs cannot become links');
assert.ok(hasClass(card, 'bot-market-note')[0].classes.has('is-multiline'), 'Appraisal verse line breaks stay visible');
assert.equal(JSON.stringify(listings), original, 'Rendering never mutates the provider ranking');

for (const sortByDistance of [false, true]) {
    const legacy = new Element('container');
    const rowsWithoutDays = listings.map(({ DaysListed, ...listing }) => listing);
    context.appendMarketListings(legacy, rowsWithoutDays, 'Existing retail results', sortByDistance);
    const legacyTable = elements(legacy, 'table')[0];
    const first = elements(legacyTable, 'tbody')[0].children[0];
    assert.ok(first.text().includes('Second Dealer'), 'Existing retail keeps price/distance ordering');
    assert.equal(elements(legacyTable, 'thead')[0].children[0].children.length, 4, 'Legacy tables gain no empty days column');
    assert.equal(hasClass(legacy, 'bot-market-listing-vin').length, 0, 'Appraisal-only VIN detail does not change existing results');
}

function nextTimer() {
    const first = timers.entries().next().value;
    assert.ok(first, 'Progress polling remains scheduled');
    timers.delete(first[0]); first[1]();
}
for (const fail of [false, true]) {
    context.sendMessage('Appraise a 2021 Honda Accord EX-L with 42k miles — show 10 comparables');
    const post = requests.at(-1), thinking = messages.at(-1).element;
    assert.match(post.options.data.ProgressId, /^[a-f0-9]{32}$/);
    nextTimer();
    requests.at(-1).resolve(true);
    assert.equal(hasClass(thinking, 'gtx-chat-copy')[0].text(), 'Checking the Market for you…');
    nextTimer();
    requests.at(-1).reject({ status: 503 });
    nextTimer();
    const pending = requests.at(-1);
    pending.resolve('market-appraisal');
    assert.equal(hasClass(thinking, 'gtx-chat-copy')[0].text(), 'Checking MarketCheck AI…');
    assert.equal(hasClass(thinking, 'gtx-chat-thinking-spinner').length, 1, 'Progress transitions retain the spinner');
    if (fail) post.reject({ status: 502 });
    else post.resolve({ Reply: 'Report ready.', PrivateReply: true });
    assert.ok(thinking.removed, 'Thinking state disappears on success and failure');
    assert.equal(context.sending, false);
    assert.equal(timers.size, 0);
    assert.ok(pending.aborted);
    assert.equal(messageInput.lastEvent, 'focus');
    pending.resolve('cargurus');
    assert.equal(hasClass(thinking, 'gtx-chat-copy')[0].text(), 'Checking MarketCheck AI…', 'Late polling cannot overwrite a finished request');
    if (!fail) assert.equal(messages.at(-1).persist, false, 'Private appraisals stay out of saved chat history');
}
console.log('PASS: ranked appraisal comparables, optional VIN/days, safe text/links, legacy sorting, metrics order, progress transitions/retry and success/failure cleanup.');
