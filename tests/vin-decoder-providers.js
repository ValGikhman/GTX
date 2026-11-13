const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const view = fs.readFileSync(path.join(__dirname, '../Views/VinDecoder/Index.cshtml'), 'utf8');
const providerMarkup = view.match(/<select\b[^>]*id="vinProvider"[^>]*>([\s\S]*?)<\/select>/);
assert.ok(providerMarkup, 'The decoder page must offer a labeled provider dropdown');
assert.match(view, /<label\b[^>]*for="vinProvider"/);
const options = [...providerMarkup[1].matchAll(/<option\b([^>]*)>([^<]*)<\/option>/g)].map(match => ({
    value: match[1].match(/\bvalue="([^"]*)"/)[1],
    label: match[2],
    selected: /\bselected(?:=|\s|$)/.test(match[1])
}));
assert.equal(options.length, 2);
const defaultProvider = options.find(option => option.selected) || options[0];
assert.equal(defaultProvider.value, 'dataone', 'DataOne must remain the initial provider');
assert.equal(defaultProvider.label, 'DataOne');
assert.equal(options.find(option => option.value === 'neovin').label, 'MarketCheck NeoVIN');

class Element {
    constructor(value = '', classes = []) {
        this.value = value;
        this.classes = new Set(classes);
        this.handlers = {};
        this.visible = true;
        this.disabled = false;
        this.reportHtml = '';
        this.textValue = '';
    }
    val(value) { if (!arguments.length) return this.value; this.value = value; return this; }
    text(value) { if (!arguments.length) return this.textValue; this.textValue = String(value); return this; }
    prop(name, value) { if (arguments.length === 1) return this[name]; this[name] = value; return this; }
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
    add(element) { this.elements.push(element); return this; }
    prop(name, value) { this.elements.forEach(element => element.prop(name, value)); return this; }
}

const nodes = {
    vin: new Element(), decode: new Element(), clear: new Element(), loadSaved: new Element(),
    vinProvider: new Element(defaultProvider.value),
    vindecoderOverlay: new Element('', ['spinner-hidden']),
    vinError: new Element('', ['d-none']), errorMessage: new Element(),
    dataOneVinDecoder: new Element(), emptyState: new Element(), token: new Element('offline-token'),
    vinReportStatus: new Element(), vinWorkingMessage: new Element()
};
const inputGroup = new Element();
const details = new Element();
const requests = [];
const safeHtmlCalls = [];
let spinnerStarts = 0;
let spinnerStops = 0;
const $ = argument => {
    if (typeof argument === 'function') { argument(); return; }
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
    assert.ok(nodes.vindecoderOverlay.classes.has('spinner-hidden'));
}
function checkRequest(index, provider, loadSaved = false) {
    const request = requests[index];
    assert.equal(request.url, loadSaved ? '/VinDecoder/LoadSavedReport'
        : provider === 'dataone' ? '/VinDecoder/DecodeDataOneByAnyVin' : '/VinDecoder/DecodeMarketCheckByAnyVin');
    assert.equal(request.options.method, 'POST');
    assert.equal(request.options.headers['X-Requested-With'], 'XMLHttpRequest');
    assert.match(request.options.headers['Content-Type'], /^application\/x-www-form-urlencoded/);
    const body = new URLSearchParams(request.options.body);
    assert.equal(body.get('vin'), '1HGCM82633A004352');
    assert.equal(body.get('__RequestVerificationToken'), 'offline-token');
    assert.equal(body.get('provider'), loadSaved ? provider : null);
    return request;
}
function response(status, html = '<details class="avd-style">Offline report</details>', redirected = false, saved = null) {
    return { ok: status >= 200 && status < 300, status, redirected,
        headers: { get(name) { return name === 'X-GTX-Vin-Saved' ? saved : null; } },
        async text() { return html; } };
}

(async () => {
    assert.equal(requests.length, 0, 'Opening the page must not decode anything');
    assert.equal(nodes.decode.disabled, true);
    assert.equal(nodes.loadSaved.disabled, true);
    nodes.vinProvider.val('neovin').fire('change');
    assert.equal(requests.length, 0, 'Selecting a provider must not request a report');
    nodes.vinProvider.val('dataone').fire('change');
    nodes.vin.val('1hgcm82633a004352').trigger('input');
    assert.equal(nodes.decode.disabled, false);
    assert.equal(nodes.loadSaved.disabled, false);

    let pending = nodes.decode.fire('click');
    const first = checkRequest(0, 'dataone');
    assert.equal(nodes.vin.val(), '1HGCM82633A004352');
    for (const name of ['vin', 'decode', 'clear', 'vinProvider', 'loadSaved']) assert.equal(nodes[name].disabled, true, name + ' must be disabled while decoding');
    assert.equal(nodes.vinWorkingMessage.text(), 'Decoding VIN…');
    assert.equal(nodes.emptyState.visible, false);
    assert.equal(nodes.vindecoderOverlay.classes.has('spinner-hidden'), false);
    await nodes.decode.fire('click');
    await nodes.loadSaved.fire('click');
    nodes.vin.fire('keydown', { key: 'Enter', preventDefault() {} });
    nodes.clear.fire('click');
    assert.equal(requests.length, 1, 'Click and Enter retries must share the busy guard');
    assert.equal(nodes.vin.val(), '1HGCM82633A004352', 'A busy clear must not change the report VIN');
    nodes.vin.val('1HGCM82633A004353').trigger('input');
    assert.equal(nodes.vin.val(), '1HGCM82633A004352', 'A busy input event must not relabel the requested VIN');
    nodes.vinProvider.val('neovin').fire('change');
    assert.equal(nodes.vinProvider.val(), 'dataone', 'A programmatic busy provider change must not relabel an in-flight report');
    first.resolve(response(200, '<details class="avd-style">Offline report</details>', false, 'true'));
    await pending;
    assertReady();
    assert.equal(safeHtmlCalls.length, 1);
    assert.equal(details.open, true);
    assert.match(nodes.dataOneVinDecoder.reportHtml, /Offline report/);
    assert.equal(nodes.vinReportStatus.text(), 'Saved for this inventory vehicle.');

    nodes.errorMessage.text('Old provider error');
    nodes.vinError.removeClass('d-none');
    nodes.vinProvider.val('neovin').fire('change');
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

    pending = nodes.loadSaved.fire('click');
    const savedReport = checkRequest(requests.length - 1, 'neovin', true);
    assert.equal(nodes.vinWorkingMessage.text(), 'Loading saved report…');
    assert.equal(nodes.loadSaved.disabled, true);
    await nodes.decode.fire('click');
    await nodes.loadSaved.fire('click');
    assert.equal(requests.at(-1), savedReport, 'Loading must share the decode busy guard');
    const storedHtml = '<details class="avd-style">Trusted saved NeoVIN report</details>';
    savedReport.resolve(response(200, storedHtml, false, 'true'));
    await pending;
    assertReady();
    assert.equal(nodes.dataOneVinDecoder.reportHtml, storedHtml);
    assert.equal(nodes.vinReportStatus.text(), 'Saved report loaded.');

    const paidBeforeMissing = requests.filter(request => request.url !== '/VinDecoder/LoadSavedReport').length;
    pending = nodes.loadSaved.fire('click');
    const missing = checkRequest(requests.length - 1, 'neovin', true);
    missing.resolve({ ok: false, status: 404, redirected: false, async text() { assert.fail('A missing saved report must not inject upstream HTML'); } });
    await pending;
    assertReady();
    assert.equal(nodes.dataOneVinDecoder.reportHtml, '');
    assert.equal(nodes.vinReportStatus.text(), '');
    assert.equal(nodes.emptyState.visible, true);
    assert.match(nodes.errorMessage.text(), /No saved report/);
    assert.equal(requests.filter(request => request.url !== '/VinDecoder/LoadSavedReport').length, paidBeforeMissing,
        'A missing stored report must never fall back to a paid decode');

    nodes.vinProvider.val('dataone').fire('change');
    pending = nodes.loadSaved.fire('click');
    const savedDataOne = checkRequest(requests.length - 1, 'dataone', true);
    savedDataOne.resolve(response(200, '<details class="avd-style">Saved DataOne report</details>', false, 'true'));
    await pending;
    assert.equal(nodes.vinReportStatus.text(), 'Saved report loaded.');
    nodes.vinProvider.val('neovin').fire('change');
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
    assert.equal(nodes.decode.disabled, true);
    assert.equal(nodes.loadSaved.disabled, true);
    assert.equal(nodes.dataOneVinDecoder.reportHtml, '');
    assert.equal(nodes.emptyState.visible, true);
    assert.equal(nodes.vinReportStatus.text(), '');
    assert.ok(nodes.vinError.classes.has('d-none'));
    const beforeInvalid = requests.length;
    for (const invalid of ['', 'SHORT', '1HGCM82633A00435I']) {
        nodes.vin.val(invalid).trigger('input');
        assert.equal(nodes.decode.disabled, true);
        assert.equal(nodes.loadSaved.disabled, true);
        await nodes.decode.fire('click');
        await nodes.loadSaved.fire('click');
    }
    assert.equal(requests.length, beforeInvalid, 'Invalid VINs must not reach either provider');
    assert.equal(spinnerStarts, spinnerStops, 'Success and all failure paths must release the busy overlay');
    console.log('PASS: actual VIN decoder UI, DataOne/NeoVIN POSTs, inventory autosave headers, saved-report POST/loading/404 without paid fallback, VIN/provider clearing, busy guards, safe HTML and error/clear behavior.');
})().catch(error => { console.error(error); process.exitCode = 1; });
