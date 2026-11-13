const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const view = fs.readFileSync(path.join(__dirname, '../Views/VinDecoder/Index.cshtml'), 'utf8');
assert.doesNotMatch(view, /loadSaved|LoadSavedReport|Load saved/, 'Saved reports must be checked automatically through Decode');
const providerMarkup = view.match(/<input\b[^>]*id="vinProvider"[^>]*>/);
assert.ok(providerMarkup, 'The decoder page must retain its provider value');
assert.match(providerMarkup[0], /\btype="hidden"/);
const providerToggleMarkup = view.match(/<button\b[^>]*id="vinProviderDropdown"[^>]*data-bs-toggle="dropdown"[^>]*>([\s\S]*?)<\/button>/);
assert.ok(providerToggleMarkup, 'The provider picker must use a dropdown button');
assert.match(providerToggleMarkup[1], /<span id="vinProviderName">DataOne<\/span>/, 'The button must display the selected provider');
assert.doesNotMatch(providerToggleMarkup[1], /Providers/);
assert.equal([...view.matchAll(/id="vinProviderName"/g)].length, 1, 'The selected provider must appear only inside the button');
assert.match(view, /id="vinProviderDropdown"[^>]*class="[^"]*btn-gradient/);
assert.match(view, /id="vinProviderMenu"[^>]*aria-labelledby="vinProviderDropdown"/);
const options = [...view.matchAll(/<button\b([^>]*class="[^"]*\bvin-provider-option\b[^"]*"[^>]*)>([\s\S]*?)<\/button>/g)].map(match => {
    const attribute = name => match[1].match(new RegExp('\\b' + name + '="([^"]*)"'))[1];
    assert.match(match[2], /class="majordome-batch-action-icon"/);
    assert.match(match[2], /<small>[^<]+<\/small>/, 'Each provider needs a short description');
    assert.doesNotMatch(match[2], /neovin|neon/i, 'Internal product names must not appear in provider labels');
    return { id: attribute('id'), value: attribute('value'), label: attribute('data-provider-name'), pressed: attribute('aria-pressed') };
});
assert.equal(options.length, 2);
const defaultProvider = options.find(option => option.value === providerMarkup[0].match(/\bvalue="([^"]*)"/)[1]);
assert.equal(defaultProvider.value, 'dataone', 'DataOne must remain the initial provider');
assert.equal(defaultProvider.label, 'DataOne');
assert.equal(options.find(option => option.value === 'neovin').label, 'MarketCheck');

class Element {
    constructor(value = '', classes = []) {
        this.value = value;
        this.classes = new Set(classes);
        this.handlers = {};
        this.visible = true;
        this.disabled = false;
        this.reportHtml = '';
        this.textValue = '';
        this.attributes = {};
    }
    val(value) { if (!arguments.length) return this.value; this.value = value; return this; }
    text(value) { if (!arguments.length) return this.textValue; this.textValue = String(value); return this; }
    prop(name, value) { if (arguments.length === 1) return this[name]; this[name] = value; return this; }
    attr(name, value) { if (arguments.length === 1) return this.attributes[name]; this.attributes[name] = value; return this; }
    add(other) { return new Collection([this, other]); }
    addClass(name) { this.classes.add(name); return this; }
    removeClass(name) { this.classes.delete(name); return this; }
    empty() { this.reportHtml = ''; return this; }
    hide() { this.visible = false; return this; }
    show() { this.visible = true; return this; }
    on(event, handler) { this.handlers[event] = handler; return this; }
    fire(event, args = {}) { return this.handlers[event] && this.handlers[event].call(this, args); }
    trigger(event) { this.fire(event); return this; }
    select() { return this; }
    closest(selector) { assert.equal(selector, '.input-group'); return inputGroup; }
    find(selector) { assert.equal(selector, 'details.avd-style'); return details; }
    html() { assert.fail('Provider reports must pass through the existing safe HTML helper'); }
}
class Collection {
    constructor(elements) { this.elements = elements; }
    add(element) { this.elements.push(...(element instanceof Collection ? element.elements : [element])); return this; }
    prop(name, value) { this.elements.forEach(element => element.prop(name, value)); return this; }
    on(event, handler) { this.elements.forEach(element => element.on(event, handler)); return this; }
    each(handler) { this.elements.forEach((element, index) => handler.call(element, index, element)); return this; }
}

const nodes = {
    vin: new Element(), decode: new Element(), clear: new Element(),
    vinProvider: new Element(defaultProvider.value),
    vinProviderDropdown: new Element(), vinProviderName: new Element(),
    vindecoderOverlay: new Element('', ['spinner-hidden']),
    vinError: new Element('', ['d-none']), errorMessage: new Element(),
    dataOneVinDecoder: new Element(), emptyState: new Element(), token: new Element('offline-token'),
    vinReportStatus: new Element(), vinWorkingMessage: new Element()
};
for (const option of options) {
    nodes[option.id] = new Element(option.value)
        .attr('data-provider-name', option.label).attr('aria-pressed', option.pressed);
}
const providerChoices = new Collection(options.map(option => nodes[option.id]));
const inputGroup = new Element();
const details = new Element();
const requests = [];
const safeHtmlCalls = [];
let spinnerStarts = 0;
let spinnerStops = 0;
const $ = argument => {
    if (typeof argument === 'function') { argument(); return; }
    if (argument instanceof Element) return argument;
    if (argument === '#vinProviderMenu .vin-provider-option') return providerChoices;
    if (argument === '#vinError .msg') return nodes.errorMessage;
    if (argument === ".vin-decoder-controls input[name='__RequestVerificationToken']") return nodes.token;
    assert.ok(typeof argument === 'string' && argument.startsWith('#'), 'Use actual page nodes');
    const element = nodes[argument.slice(1)];
    assert.ok(element, 'Unknown page selector: ' + argument);
    return element;
};
$.param = values => new URLSearchParams(values).toString();
const script = view.match(/<script>([\s\S]*?)<\/script>/)[1]
    .replace(/@Url\.Action\("([^"]+)",\s*"([^"]+)"\)/g, (_, action, controller) => '/' + controller + '/' + action);
assert.ok(!script.includes('@Url.'), 'All actual action URLs must be resolved for the offline fixture');
vm.runInNewContext(script, {
    $, console,
    fetch(url, options) {
        return new Promise((resolve, reject) => requests.push({ url, options, resolve, reject }));
    },
    window: { gtxSecurity: { setSafeHtml(target, html) {
        assert.equal(target, nodes.dataOneVinDecoder);
        safeHtmlCalls.push(html);
        target.reportHtml = html;
    } } },
    showSpinner(target) { assert.equal(target, nodes.vindecoderOverlay); spinnerStarts++; target.removeClass('spinner-hidden'); },
    hideSpinner(target) { assert.equal(target, nodes.vindecoderOverlay); spinnerStops++; target.addClass('spinner-hidden'); },
    setTimeout(callback) { callback(); }
});

function assertReady() {
    assert.equal(nodes.vin.disabled, false);
    assert.equal(nodes.clear.disabled, false);
    assert.equal(nodes.vinProvider.disabled, false);
    assert.equal(nodes.vinProviderDropdown.disabled, false);
    for (const option of options) assert.equal(nodes[option.id].disabled, false);
    assert.ok(nodes.vindecoderOverlay.classes.has('spinner-hidden'));
}
function assertSelected(provider) {
    assert.equal(nodes.vinProvider.val(), provider);
    for (const option of options) {
        assert.equal(nodes[option.id].attr('aria-pressed'), String(option.value === provider));
    }
    assert.equal(nodes.vinProviderName.text(), options.find(option => option.value === provider).label);
}
function checkRequest(index, provider) {
    const request = requests[index];
    assert.equal(request.url, provider === 'dataone' ? '/VinDecoder/DecodeDataOneByAnyVin' : '/VinDecoder/DecodeMarketCheckByAnyVin');
    assert.equal(request.options.method, 'POST');
    assert.equal(request.options.headers['X-Requested-With'], 'XMLHttpRequest');
    assert.match(request.options.headers['Content-Type'], /^application\/x-www-form-urlencoded/);
    const body = new URLSearchParams(request.options.body);
    assert.equal(body.get('vin'), '1HGCM82633A004352');
    assert.equal(body.get('__RequestVerificationToken'), 'offline-token');
    assert.equal(body.get('provider'), null);
    return request;
}
function response(status, html = '<details class="avd-style">Offline report</details>', redirected = false, saved = null, source = 'provider') {
    return { ok: status >= 200 && status < 300, status, redirected,
        headers: { get(name) { return name === 'X-GTX-Vin-Saved' ? saved : name === 'X-GTX-Vin-Source' ? source : null; } },
        async text() { return html; } };
}

(async () => {
    assert.equal(requests.length, 0, 'Opening the page must not decode anything');
    assert.equal(nodes.decode.disabled, true);
    assertSelected('dataone');
    nodes.vinProviderMarketCheck.fire('click');
    assertSelected('neovin');
    assert.equal(requests.length, 0, 'Selecting a provider must not request a report');
    nodes.vinProviderDataOne.fire('click');
    assertSelected('dataone');
    nodes.vin.val('1hgcm82633a004352').trigger('input');
    assert.equal(nodes.decode.disabled, false);

    let pending = nodes.decode.fire('click');
    const first = checkRequest(0, 'dataone');
    assert.equal(nodes.vin.val(), '1HGCM82633A004352');
    for (const name of ['vin', 'decode', 'clear', 'vinProvider', 'vinProviderDropdown', ...options.map(option => option.id)]) assert.equal(nodes[name].disabled, true, name + ' must be disabled while loading a report');
    assert.equal(nodes.vinWorkingMessage.text(), 'Loading VIN report…');
    assert.equal(nodes.emptyState.visible, false);
    assert.equal(nodes.vindecoderOverlay.classes.has('spinner-hidden'), false);
    await nodes.decode.fire('click');
    nodes.vin.fire('keydown', { key: 'Enter', preventDefault() {} });
    nodes.clear.fire('click');
    assert.equal(requests.length, 1, 'Click and Enter retries must share the busy guard');
    assert.equal(nodes.vin.val(), '1HGCM82633A004352', 'A busy clear must not change the report VIN');
    nodes.vin.val('1HGCM82633A004353').trigger('input');
    assert.equal(nodes.vin.val(), '1HGCM82633A004352', 'A busy input event must not relabel the requested VIN');
    nodes.vinProvider.val('neovin').fire('change');
    assert.equal(nodes.vinProvider.val(), 'dataone', 'A programmatic busy provider change must not relabel an in-flight report');
    nodes.vinProviderMarketCheck.fire('click');
    assertSelected('dataone');
    assert.equal(requests.length, 1, 'A busy provider click must not issue another request');
    first.resolve(response(200, '<details class="avd-style">Offline report</details>', false, 'true'));
    await pending;
    assertReady();
    assert.equal(safeHtmlCalls.length, 1);
    assert.equal(details.open, true);
    assert.match(nodes.dataOneVinDecoder.reportHtml, /Offline report/);
    assert.equal(nodes.vinReportStatus.text(), 'Saved for this inventory vehicle.');
    nodes.vinProviderDataOne.fire('click');
    assert.match(nodes.dataOneVinDecoder.reportHtml, /Offline report/, 'Selecting the current provider must preserve the report');

    nodes.errorMessage.text('Old provider error');
    nodes.vinError.removeClass('d-none');
    nodes.vinProviderMarketCheck.fire('click');
    assertSelected('neovin');
    assert.equal(requests.length, 1);
    assert.equal(nodes.dataOneVinDecoder.reportHtml, '', 'Changing providers must clear the old report');
    assert.equal(nodes.emptyState.visible, true);
    assert.ok(nodes.vinError.classes.has('d-none'));
    assert.equal(nodes.errorMessage.text(), '', 'Changing providers must clear the old error');
    assert.equal(nodes.vinReportStatus.text(), '', 'Changing providers must clear the saved status');
    assert.equal(nodes.vin.val(), '1HGCM82633A004352');
    pending = nodes.decode.fire('click');
    const neo = checkRequest(1, 'neovin');
    neo.resolve(response(200, '<details class="avd-style">NeoVIN offline report</details>', false, 'false'));
    await pending;
    assertReady();
    assert.equal(safeHtmlCalls.length, 2, 'Both providers must use the same safe HTML boundary');
    assert.match(nodes.dataOneVinDecoder.reportHtml, /NeoVIN offline report/);
    assert.equal(nodes.vinReportStatus.text(), 'Decoded. Saving is available for inventory vehicles.');

    const beforeVinChange = requests.length;
    nodes.vin.val('1HGCM82633A004353').trigger('input');
    assert.equal(nodes.dataOneVinDecoder.reportHtml, '', 'Editing the VIN must clear the previous VIN report');
    assert.equal(nodes.vinReportStatus.text(), '', 'Editing the VIN must clear inventory-save status');
    assert.equal(requests.length, beforeVinChange, 'Editing the VIN must not trigger a decode or load');
    nodes.vin.val('1HGCM82633A004352').trigger('input');

    pending = nodes.decode.fire('click');
    const failedSave = checkRequest(requests.length - 1, 'neovin');
    const requestCount = requests.length;
    const failedSaveHtml = '<details class="avd-style">Decoded report retained after save failure</details>';
    failedSave.resolve(response(200, failedSaveHtml, false, 'failed'));
    await pending;
    assertReady();
    assert.equal(nodes.dataOneVinDecoder.reportHtml, failedSaveHtml);
    assert.equal(nodes.vinReportStatus.text(), 'Decoded, but saving failed. Please try again later.');
    assert.equal(requests.length, requestCount, 'A persistence failure must never automatically repeat a paid decode');

    const beforeSaved = requests.length;
    pending = nodes.decode.fire('click');
    const savedReport = checkRequest(requests.length - 1, 'neovin');
    assert.equal(nodes.vinWorkingMessage.text(), 'Loading VIN report…');
    await nodes.decode.fire('click');
    assert.equal(requests.at(-1), savedReport, 'Cached responses must share the decode busy guard');
    const storedHtml = '<details class="avd-style">Trusted saved NeoVIN report</details>';
    savedReport.resolve(response(200, storedHtml, false, 'true', 'database'));
    await pending;
    assertReady();
    assert.equal(nodes.dataOneVinDecoder.reportHtml, storedHtml);
    assert.equal(nodes.vinReportStatus.text(), 'Saved report loaded.');
    assert.equal(requests.length, beforeSaved + 1, 'Decode must use a single browser request for a cached report');

    nodes.vinProviderDataOne.fire('click');
    assertSelected('dataone');
    pending = nodes.decode.fire('click');
    const savedDataOne = checkRequest(requests.length - 1, 'dataone');
    savedDataOne.resolve(response(200, '<details class="avd-style">Saved DataOne report</details>', false, 'true', 'database'));
    await pending;
    assert.equal(nodes.vinReportStatus.text(), 'Saved report loaded.');
    nodes.vinProviderMarketCheck.fire('click');
    assertSelected('neovin');
    const successfulReports = safeHtmlCalls.length;

    for (const [status, redirected, expected] of [
        [429, false, /limit has been reached/], [504, false, /timed out/],
        [503, false, /administrator to check Services/],
        [401, false, /sign in again/], [403, false, /sign in again/],
        [200, true, /sign in again/], [502, false, /Failed to decode VIN/]
    ]) {
        pending = nodes.decode.fire('click');
        const request = checkRequest(requests.length - 1, 'neovin');
        request.resolve({ ok: status === 200, status, redirected, async text() { assert.fail('Never render upstream error or login HTML'); } });
        await pending;
        assertReady();
        assert.equal(nodes.emptyState.visible, true);
        assert.equal(nodes.dataOneVinDecoder.reportHtml, '');
        assert.equal(nodes.vinReportStatus.text(), '');
        assert.equal(nodes.vinError.classes.has('d-none'), false);
        assert.match(nodes.errorMessage.text(), expected);
    }
    assert.equal(safeHtmlCalls.length, successfulReports, 'Failures must never inject report HTML');
    pending = nodes.decode.fire('click');
    requests.at(-1).reject(new Error('Offline network failure'));
    await pending;
    assertReady();
    assert.equal(nodes.errorMessage.text(), 'Offline network failure');

    nodes.vinReportStatus.text('Previous status');
    nodes.clear.fire('click');
    assert.equal(nodes.vin.val(), '');
    assert.equal(nodes.vinProvider.val(), 'neovin', 'Clear VIN must preserve the selected provider');
    assertSelected('neovin');
    assert.equal(nodes.decode.disabled, true);
    assert.equal(nodes.dataOneVinDecoder.reportHtml, '');
    assert.equal(nodes.emptyState.visible, true);
    assert.equal(nodes.vinReportStatus.text(), '');
    assert.ok(nodes.vinError.classes.has('d-none'));
    const beforeInvalid = requests.length;
    for (const invalid of ['', 'SHORT', '1HGCM82633A00435I']) {
        nodes.vin.val(invalid).trigger('input');
        assert.equal(nodes.decode.disabled, true);
        await nodes.decode.fire('click');
    }
    assert.equal(requests.length, beforeInvalid, 'Invalid VINs must not reach either provider');
    assert.equal(spinnerStarts, spinnerStops, 'Success and all failure paths must release the busy overlay');
    console.log('PASS: selected-provider menu, one-request DataOne/MarketCheck decode and cached report responses, inventory autosave headers, busy guards, VIN/provider clearing, safe HTML and error/clear behavior.');
})().catch(error => { console.error(error); process.exitCode = 1; });
