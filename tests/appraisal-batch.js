const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const path = require('node:path');

function node(extra = {}) {
    return Object.assign({ hidden: false, value: '', disabled: false, textContent: '', handlers: {}, dataset: {}, style: {},
        classList: { toggle() {}, add() {}, remove() {} },
        addEventListener(name, callback) { this.handlers[name] = callback; }, setAttribute() {}, focus() {} }, extra);
}
async function waitFor(condition, message) {
    for (let attempt = 0; attempt < 100; attempt++) {
        if (condition()) return;
        await new Promise(resolve => setImmediate(resolve));
    }
    assert.ok(condition(), message);
}
async function scenario(failFirst, extension = 'pdf', mileages = [[116589, 150000], [89675, 100000]]) {
    const [[lexusMiles, lexusBucket], [fordMiles, fordBucket]] = mileages;
    const nodes = {};
    for (const id of ['pr-batch-inputs', 'pr-pdf-file', 'pr-pdf-drop', 'pr-batch-status', 'pr-batch-error',
        'pr-batch-run', 'pr-batch-stop', 'pr-batch-zip', 'pr-batch-radius', 'pr-individual-inputs',
        'pr-individual-results', 'pr-batch-results']) nodes[id] = node();
    const form = nodes['pr-batch-inputs'];
    form.dataset = { parseUrl: 'parse', appraiseUrl: 'appraise', decodeUrl: 'decode', cargurusLogo: '/Content/images/cargurus.svg' };
    form.reportValidity = () => true;
    form.querySelector = () => ({ value: 'token' });
    for (const part of ['title', 'summary', 'badge', 'hint', 'icon']) nodes['pr-pdf-drop-' + part] = node({ hidden: part === 'badge' });
    for (const id of ['pr-batch-progress-modal', 'pr-batch-progress-message', 'pr-batch-progress', 'pr-batch-progress-bar']) nodes[id] = node();
    nodes['pr-batch-zip'].value = '45040';
    nodes['pr-batch-radius'].value = '100';
    const lexusVin = '58ABZ1B11KU026567', fordVin = '1FTBR3X81MKA59855', failedVin = 'JH4CU2F68DC006060';
    const removedVin = '1HGCM82633A004352';
    const identities = {
        [lexusVin]: { Vin: lexusVin, Year: '2019', Make: 'Lexus', Model: 'ES 350', Style: 'Sedan "Special"', Trim: 'Luxury', BodyStyle: 'Sedan', DriveType: 'FWD' },
        [fordVin]: { Vin: fordVin, Year: '2021', Make: 'Ford', Model: 'Transit', Style: 'Extended Length Cargo Van', Trim: '250', BodyStyle: 'Full-Size', DriveType: 'RWD' }
    };
    let releaseFirstDecode;
    const firstDecode = new Promise(resolve => { releaseFirstDecode = resolve; });
    const decodeCalls = [], calls = [], progressSteps = [];
    let progressVisible = false, rows = [], columns = [], ready = false, active = 0, maxActive = 0;
    let activeDecodes = 0, maxActiveDecodes = 0, retiredRows, staleRender = false, parseCalls = 0;
    const grid = {
        data() { return ready; },
        addGrid(argument, data) {
            if (typeof argument === 'object') { ready = true; rows = argument.data; columns = argument.columns; }
            else if (argument === 'setData') { if (retiredRows && data === retiredRows) staleRender = true; rows = data; }
            return this;
        },
        on() { return this; }
    };
    const $ = argument => argument === '#pr-batch-grid' ? grid : {
        text(value) { this.value = value; return this; }, html() { return this.value.replaceAll('&', '&amp;'); }
    };
    $.ajax = async options => {
        if (options.url.startsWith('https://www.cargurus.com/')) {
            const url = new URL(options.url);
            if (url.pathname.endsWith('/listMakes.action')) return { success: true, makes: [{ name: 'Lexus', id: 'm37' }, { name: 'Ford', id: 'm2' }] };
            const models = url.searchParams.get('makeId') === 'm37' ? [{ name: 'ES', id: 'd2720' }] : [{ name: 'Transit Cargo', id: 'd1067' }];
            return { success: true, models };
        }
        assert.ok(options.url === 'parse' || options.data.__RequestVerificationToken === 'token');
        if (options.url === 'parse') {
            const vehicles = [
                // Wrong import fields ensure marketplace searches use the decoded VIN.
                { Vin: lexusVin, Miles: lexusMiles, Vehicle: '2000 Toyota Wrong', Year: '2000', Make: 'Toyota', Model: 'Wrong' },
                { Vin: fordVin, Miles: fordMiles, Vehicle: '2021 Ford T250' },
                { Vin: failedVin, Miles: null, Warning: 'Check mileage' }
            ];
            if (++parseCalls === 1) vehicles.push({ Vin: removedVin, Miles: 60000, Vehicle: 'Removed from replacement file' });
            return { vehicles };
        }
        if (options.url === 'decode') {
            assert.equal(options.type, 'POST');
            decodeCalls.push(options.data.vin);
            activeDecodes++;
            maxActiveDecodes = Math.max(maxActiveDecodes, activeDecodes);
            try {
                if (options.data.vin === lexusVin) await firstDecode;
                if (options.data.vin === failedVin) throw { status: 422, responseJSON: { message: 'VIN identity could not be decoded.' } };
                return { vehicle: identities[options.data.vin] };
            } finally { activeDecodes--; }
        }
        assert.equal(options.url, 'appraise', 'Only explicit pricing requests reach the appraisal service');
        assert.equal(progressVisible, true);
        assert.match(nodes['pr-batch-progress-message'].textContent, new RegExp(options.data.vin));
        progressSteps.push(nodes['pr-batch-progress-bar'].style.width);
        calls.push(options.data);
        active++;
        maxActive = Math.max(maxActive, active);
        await new Promise(resolve => setTimeout(resolve, 1));
        active--;
        if (failFirst) throw { status: 429, responseJSON: { message: 'Quota reached' } };
        return { marketCheckRetailUsd: 16000, maximumHammerBidUsd: 10500, message: 'Complete' };
    };
    const window = { jQuery: $, bootstrap: { Modal: { getOrCreateInstance() { return { show() { progressVisible = true; }, hide() { progressVisible = false; } }; } } }, addEventListener() {} };
    const document = { getElementById: id => nodes[id], querySelectorAll: () => [] };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../Scripts/majordome-appraisal-batch.js'), 'utf8'),
        { window, document, Intl, FormData, URLSearchParams, console });
    const actions = row => columns.find(column => column.field === 'Actions').template(row);
    const marketplace = (row, hostname) => [...actions(row).matchAll(/href="([^"]+)"/g)]
        .map(match => new URL(match[1].replaceAll('&amp;', '&'))).find(url => url.hostname === hostname);
    nodes['pr-pdf-file'].files = [new File(['%PDF-test'], 'all.' + extension, { type: extension === 'csv' ? 'text/csv' : 'application/pdf' })];
    await nodes['pr-pdf-file'].handlers.change();
    assert.equal(calls.length, 0, 'Uploading must not trigger appraisals');
    assert.equal(rows.length, 4, 'Uploading must populate the grid');
    assert.equal(nodes['pr-pdf-drop-title'].textContent, 'all.' + extension);
    assert.equal(nodes['pr-pdf-drop-summary'].textContent, '4 vehicles imported');
    assert.equal(nodes['pr-pdf-drop-badge'].hidden, false);
    const firstRows = rows;
    assert.equal(marketplace(firstRows[0], 'www.cars.com'), undefined, 'No guessed Cars.com link before VIN decoding');
    assert.equal(marketplace(firstRows[0], 'www.cargurus.com'), undefined, 'No guessed CarGurus link before VIN decoding');
    await waitFor(() => decodeCalls.includes(lexusVin), 'VIN decoding must start after parsing');
    await nodes['pr-pdf-drop'].handlers.drop({ preventDefault() {}, dataTransfer: { files: [new File(['replacement'], 'replacement.' + extension)] } });
    assert.equal(nodes['pr-pdf-drop-title'].textContent, 'replacement.' + extension);
    assert.equal(calls.length, 0, 'Replacing a file must not trigger appraisals');
    const replacementRows = rows;
    assert.equal(replacementRows.length, 3);
    retiredRows = firstRows;
    assert.notEqual(replacementRows, firstRows);
    releaseFirstDecode();
    await waitFor(() => marketplace(replacementRows[0], 'www.cargurus.com') && marketplace(replacementRows[1], 'www.cargurus.com') && decodeCalls.includes(failedVin),
        'Decoded marketplace links must be populated asynchronously');
    assert.equal(rows, replacementRows, 'Stale work cannot replace the current imported batch');
    assert.equal(staleRender, false, 'Old batch results cannot be rendered after file replacement');
    assert.equal(maxActiveDecodes, 1, 'VIN decoding must use a bounded serial queue');
    assert.equal(decodeCalls.filter(vin => vin === lexusVin).length, 1, 'Repeated VINs share their cached decode');
    assert.equal(decodeCalls.filter(vin => vin === fordVin).length, 1, 'Replacement uploads reuse successful VIN decodes');
    assert.equal(decodeCalls.includes(removedVin), false, 'A queued VIN removed by the replacement file must not be decoded');
    assert.equal(calls.length, 0, 'Decoding and marketplace lookup must not trigger paid appraisals');

    const cars = marketplace(rows[0], 'www.cars.com').searchParams;
    assert.equal(cars.get('year_min'), '2019');
    assert.equal(cars.has('year_max'), false, 'Cars.com searches start at the decoded year without an upper limit');
    assert.equal(cars.get('makes[]'), 'lexus');
    assert.equal(cars.get('models[]'), 'lexus-es_350');
    assert.equal(cars.get('trims[]'), 'lexus-es_350-luxury');
    assert.equal(cars.has('stock_type'), false, 'Cars.com searches must not restrict stock type');
    assert.equal(cars.get('clean_title'), 'true');
    assert.ok(actions(rows[0]).includes('Sedan &quot;Special&quot; Luxury'), 'Decoded text must be escaped safely inside link titles');
    assert.equal(cars.has('body_style_slugs[]'), false, 'Decoder body style must not restrict Cars.com searches');
    assert.equal(cars.get('zip'), '45040');
    assert.equal(cars.get('maximum_distance'), '100');
    assert.equal(cars.get('sort'), 'list_price');
    assert.equal(cars.get('mileage_max'), String(lexusBucket), 'Cars.com uses the bucket ceiling of the uploaded odometer mileage');
    assert.equal(rows[0].Miles, lexusMiles, 'Bucketing must preserve the displayed odometer');
    assert.equal(columns.find(column => column.field === 'Miles').exportValue(rows[0], 'csv'), lexusMiles, 'Exports keep the actual odometer');
    assert.equal(marketplace(rows[1], 'www.cars.com').searchParams.get('models[]'), 'ford-transit');
    assert.equal(marketplace(rows[2], 'www.cars.com'), undefined, 'Decoder failure must not create a guessed search');
    const lexus = marketplace(rows[0], 'www.cargurus.com').searchParams;
    assert.equal(lexus.get('startYear'), '2019');
    assert.equal(lexus.get('endYear'), '2019');
    assert.equal(lexus.get('zip'), '45040');
    assert.equal(lexus.get('distance'), '100');
    assert.equal(lexus.get('maxMileage'), String(lexusBucket));
    assert.equal(lexus.get('sortDirection'), 'ASC');
    assert.equal(lexus.get('sortType'), 'PRICE');
    assert.equal(lexus.get('vehicleHistoryOptions'), 'LEMON_FREE,NON_SALVAGE');
    assert.equal(lexus.get('makeModelTrimPaths'), 'm37/d2720/350 Luxury FWD', 'Only the decoded Luxury trim is requested');
    assert.equal(marketplace(rows[1], 'www.cargurus.com').searchParams.get('makeModelTrimPaths'), 'm2/d1067');
    assert.equal(marketplace(rows[1], 'www.cargurus.com').searchParams.get('maxMileage'), String(fordBucket));
    assert.equal(marketplace(rows[1], 'www.cars.com').searchParams.get('mileage_max'), String(fordBucket));
    assert.ok(marketplace(rows[1], 'www.cargurus.com').search.includes('vehicleHistoryOptions=LEMON_FREE%2CNON_SALVAGE'));
    assert.equal(marketplace(rows[2], 'www.cargurus.com'), undefined);

    await form.handlers.submit({ preventDefault() {} });
    assert.equal(progressVisible, false);
    assert.deepEqual(progressSteps, failFirst ? ['0%'] : ['0%', '33%']);
    assert.equal(nodes['pr-batch-progress-bar'].style.width, failFirst ? '33%' : '100%');
    assert.equal(maxActive, 1, 'Appraisal requests must run sequentially');
    assert.equal(nodes['pr-batch-run'].disabled, false);
    if (failFirst) {
        assert.equal(calls.length, 1);
        assert.equal(nodes['pr-batch-error'].textContent, 'Quota reached');
        assert.equal(rows[0].Done, false);
    } else {
        assert.equal(calls.length, 2, 'A decode failure does not stop valid appraisal work');
        assert.equal(calls[0].miles, lexusBucket, 'MarketCheck receives the same bucket as marketplace links');
        assert.equal(calls[1].miles, fordBucket);
        assert.equal(rows[0].Miles, lexusMiles);
        assert.equal(rows[1].Miles, fordMiles);
        assert.equal(rows[0].Estimate, 16000);
        assert.equal(rows[0].Maximum, 10500);
        assert.equal(rows[1].Done, true);
        assert.equal(rows[2].Status, 'Mileage missing or invalid in file; skipped');
    }
}
const watchdog = setTimeout(() => {
    console.error('Batch regression did not complete; import or decoding may be blocking.');
    process.exitCode = 1;
}, 5000);
(async () => {
    await scenario(false);
    await scenario(false, 'csv');
    await scenario(true, 'csv');
    for (const mileages of [
        [[0, 100000], [1, 100000]],
        [[100000, 100000], [100001, 150000]],
        [[150000, 150000], [150001, 200000]],
        [[200000, 200000], [200001, 250000]],
        [[1999999, 2000000], [2000000, 2000000]]
    ]) await scenario(false, 'csv', mileages);
    console.log('PASS: Shared request mileage buckets, preserved odometer display/export, VIN-decoded marketplace filters, decode queue/cache/stale uploads, appraisal queue and quota handling.');
})().catch(error => { console.error(error); process.exitCode = 1; }).finally(() => clearTimeout(watchdog));
