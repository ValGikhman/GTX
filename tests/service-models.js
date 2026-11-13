const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Run the shipped page script with a small DOM/transport adapter. No browser or API requests.
function fixture(key = 'key-A', current = 'saved-model') {
    const page = { events: {}, props: {}, attrs: {}, options: [] };
    const form = { events: {}, props: {}, attrs: {}, options: [] };
    const picker = { events: {}, props: {}, attrs: { 'data-models-url': '/Services/OpenAiModels' }, options: [] };
    const api = { events: {}, props: {}, attrs: {}, options: [], value: key };
    const model = { events: {}, props: {}, attrs: {}, value: current, options: [{ value: current, text: current }, { value: 'user-choice', text: 'user-choice' }] };
    const refresh = { events: {}, props: {}, attrs: {}, options: [] };
    const status = { events: {}, props: {}, attrs: {}, options: [], text: '' };
    const token = { events: {}, props: {}, attrs: {}, options: [], value: 'synthetic-antiforgery' };
    const window = { events: {}, props: {}, attrs: {}, options: [] };
    const requests = [];

    class Collection {
        constructor(items) { this.items = items; this.length = items.length; }
        find(selector) {
            const first = this.items[0];
            if (first === page && selector === '.service-settings-card') return new Collection([]);
            const mapping = new Map([
                [page, { '[data-openai-model-picker]': picker }],
                [picker, { '[data-openai-model]': model, '[data-refresh-models]': refresh, '[data-models-status]': status }],
                [form, { '[data-setting-key="OpenAI:ApiKey"]': api, '[name="__RequestVerificationToken"]': token }]
            ]);
            const found = mapping.get(first)?.[selector];
            assert.ok(found, `Unexpected DOM selector ${selector}`);
            return new Collection([found]);
        }
        closest(selector) { assert.equal(this.items[0], picker); assert.equal(selector, 'form'); return new Collection([form]); }
        each(callback) { this.items.forEach((item, index) => callback.call(item, index, item)); return this; }
        on(event, selectorOrCallback) {
            if (typeof selectorOrCallback === 'function') this.items.forEach(item => { item.events[event] = selectorOrCallback; });
            return this;
        }
        prop(name, value) {
            if (value === undefined) return this.items[0]?.props[name];
            this.items.forEach(item => { item.props[name] = value; }); return this;
        }
        attr(name) { return this.items[0]?.attrs[name]; }
        val(value) {
            if (value === undefined) return this.items[0]?.value;
            this.items.forEach(item => { item.value = value; }); return this;
        }
        text(value) {
            if (value === undefined) return this.items[0]?.text;
            this.items.forEach(item => { item.text = value; }); return this;
        }
        empty() { this.items.forEach(item => { item.options = []; item.value = null; }); return this; }
        append(collection) { this.items.forEach(item => { item.options.push(...collection.items); }); return this; }
    }
    function $(item) {
        if (typeof item === 'function') { item(); return; }
        if (item === '#servicesPage') return new Collection([page]);
        if (item === '<option>') return new Collection([{ value: '', text: '', props: {}, attrs: {}, events: {} }]);
        return item instanceof Collection ? item : new Collection([item]);
    }
    $.trim = value => value.trim();
    $.each = (items, callback) => items.forEach((item, index) => callback(index, item));
    $.ajax = parameters => {
        const done = [], fail = [], always = [];
        const request = {
            parameters, aborted: false,
            done(callback) { done.push(callback); return this; },
            fail(callback) { fail.push(callback); return this; },
            always(callback) { always.push(callback); return this; },
            resolve(result) { done.forEach(callback => callback(result)); always.forEach(callback => callback()); },
            reject(reason = 'error') { fail.forEach(callback => callback({}, reason)); always.forEach(callback => callback()); },
            abort() { this.aborted = true; this.reject('abort'); }
        };
        requests.push(request); return request;
    };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../Scripts/services.js'), 'utf8'), { $, window });
    return {
        api, model, refresh, status, window, requests,
        event(element, event) { assert.equal(typeof element.events[event], 'function'); element.events[event].call(element); },
        choose(value) { model.value = value; }
    };
}

const ui = fixture();
assert.equal(ui.requests.length, 1, 'Models load automatically when a key exists');
const first = ui.requests[0];
assert.equal(first.parameters.url, '/Services/OpenAiModels');
assert.equal(first.parameters.type, 'POST');
assert.equal(first.parameters.data.apiKey, 'key-A');
assert.equal(first.parameters.data.__RequestVerificationToken, 'synthetic-antiforgery');
assert.equal(first.parameters.url.includes('key-A'), false, 'Credentials never enter the request URL');
assert.equal(ui.refresh.props.disabled, true);
assert.equal(ui.model.value, 'saved-model', 'Loading preserves the current selection');
ui.choose('user-choice');
const unsafeLabel = '<img src=x onerror=alert(1)>';
first.resolve({ ok: true, models: ['audio-model', 'image-model', 'custom-model', unsafeLabel] });
assert.equal(ui.model.value, 'user-choice', 'A choice made while loading must survive the response');
assert.deepEqual(ui.model.options.map(option => option.value), ['user-choice', 'audio-model', 'image-model', 'custom-model', unsafeLabel]);
assert.equal(ui.model.options.at(-1).text, unsafeLabel, 'Provider model names are inserted as text, never HTML');
assert.equal(ui.refresh.props.disabled, false);

let previousOptions = ui.model.options.slice();
ui.event(ui.refresh, 'click');
ui.requests.at(-1).reject();
assert.deepEqual(ui.model.options, previousOptions, 'Transport failures must not clear model choices');
assert.equal(ui.model.value, 'user-choice');
assert.match(ui.status.text, /Could not load models/);
ui.event(ui.refresh, 'click');
ui.requests.at(-1).resolve({ ok: false, message: 'Safe provider error' });
assert.deepEqual(ui.model.options, previousOptions, 'Provider errors must preserve available choices');
assert.equal(ui.status.text, 'Safe provider error');
ui.event(ui.refresh, 'click');
ui.requests.at(-1).resolve({ ok: true, models: [] });
assert.equal(ui.model.value, 'user-choice');
assert.deepEqual(ui.model.options.map(option => option.value), ['user-choice']);
assert.match(ui.status.text, /Current selection kept/);

ui.event(ui.refresh, 'click');
const oldRequest = ui.requests.at(-1);
ui.api.value = 'key-B';
ui.event(ui.api, 'input');
assert.equal(oldRequest.aborted, true, 'Changing the key cancels its previous request');
assert.equal(ui.refresh.props.disabled, false);
ui.event(ui.api, 'change');
const newRequest = ui.requests.at(-1);
assert.equal(newRequest.parameters.data.apiKey, 'key-B');
previousOptions = ui.model.options.slice();
oldRequest.resolve({ ok: true, models: ['stale-key-A-model'] });
assert.deepEqual(ui.model.options, previousOptions, 'Late responses from a previous key must be ignored');
assert.equal(ui.refresh.props.disabled, true, 'A stale completion must not release the newer request state');
newRequest.resolve({ ok: true, models: ['fresh-key-B-model'] });
assert.deepEqual(ui.model.options.map(option => option.value), ['user-choice', 'fresh-key-B-model']);

ui.event(ui.refresh, 'click');
previousOptions = ui.model.options.slice();
ui.api.value = 'silently-changed-key';
ui.requests.at(-1).resolve({ ok: true, models: ['wrong-key-result'] });
assert.deepEqual(ui.model.options, previousOptions, 'Response handling also checks the current key independently of input events');
assert.match(ui.status.text, /API key changed/);
assert.equal(ui.refresh.props.disabled, false);

ui.api.value = '';
ui.event(ui.api, 'input');
const requestCount = ui.requests.length;
ui.event(ui.api, 'change');
assert.equal(ui.requests.length, requestCount, 'Blank keys must not cause a metadata request');
assert.match(ui.status.text, /Enter an API key/);
assert.equal(ui.model.value, 'user-choice');
ui.api.value = 'key-C';
ui.event(ui.api, 'change');
const departing = ui.requests.at(-1);
ui.event(ui.window, 'pagehide');
assert.equal(departing.aborted, true, 'Leaving the page cancels model discovery');
assert.equal(ui.refresh.props.disabled, false);
assert.equal(fixture('').requests.length, 0, 'An initially blank key does not call the endpoint');
console.log('PASS: shipped model picker uses private POST/token, preserves selection/options on errors, safe text options, all model families, empty lists, key-change/late-response race guards and page-exit cancellation.');
