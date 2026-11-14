const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const gallerySource = fs.readFileSync(path.join(__dirname, '../Scripts/majordome.js'), 'utf8');

function galleryContext(flag) {
    const context = vm.createContext({ window: { gtx: { removeBgEnabled: flag } }, console, Promise });
    vm.runInContext(gallerySource, context);
    return context;
}

async function checkBackgroundRemoval() {
    for (const flag of [false, undefined]) {
        const context = galleryContext(flag);
        context.$ = () => { throw new Error('Disabled removal reached the DOM or a request'); };
        const choice = await context.showRemoveBackgroundConfirmation({});
        assert.equal(choice.useImage, false);
        assert.equal(choice.previewToken, '');
        await context.removeImageBackground('photo.jpg');
        delete context.window.gtx;
        await context.removeImageBackground('photo.jpg');
    }

    for (const flag of [false, undefined, true]) {
        const context = galleryContext(flag);
        let markup = '';
        const container = {
            empty() {},
            html(value) { markup = value; },
            find() { return { each() {} }; }
        };
        context.$ = () => container;
        context.escapeHtml = value => String(value);
        context.toInventoryImageUrl = value => value;
        context.inventoryImageVariantUrl = value => value;
        context.getMajordomeFileNameOnly = value => value;
        context.updateGalleryDisplay = () => {};
        context.loadGallery({ Images: [{ Id: 'photo-1', Source: 'photo.jpg' }] });
        assert.equal(markup.includes('class="remove-image-background'), flag === true,
            'Gallery removal button must follow the service switch');
        assert.ok(markup.includes('class="delete-image'), 'Other gallery actions must remain available');
        assert.ok(markup.includes('class="rotate-image '), 'Image rotation must remain available');
    }
}

function checkHealthWithoutServiceCards() {
    const view = fs.readFileSync(path.join(__dirname, '../Views/Health/Index.cshtml'), 'utf8');
    assert.doesNotMatch(view, /dataOneEnabled|health-card-dataone|dataOneCall|refreshDataOneQuota|renderDataOneSummary/,
        'Health must not display or refresh an unsupported DataOne quota');
    const script = view.slice(view.lastIndexOf('<script>') + '<script>'.length, view.lastIndexOf('</script>'))
        .replace(/@Url\.Action\("([^"]+)",\s*"([^"]+)"\)/g, '/$2/$1');
    let ready;
    const requests = [];
    const emptyCollection = {
        length: 0,
        prop(name, value) { return arguments.length === 1 ? false : this; }
    };
    for (const method of ['on', 'text', 'html', 'css', 'show', 'hide', 'removeClass', 'addClass', 'toggleClass'])
        emptyCollection[method] = function () { return this; };
    const pending = { done() { return this; }, fail() { return this; }, always() { return this; } };
    const $ = value => {
        if (typeof value === 'function') ready = value;
        return emptyCollection;
    };
    $.each = (values, callback) => Object.keys(values).forEach(key => callback(key, values[key]));
    $.getJSON = () => pending;
    $.get = () => pending;
    $.ajax = options => { requests.push(options); return pending; };
    const context = vm.createContext({
        $, window: {}, document: { visibilityState: 'visible' },
        setTimeout: () => 1, clearTimeout() {}, console
    });
    vm.runInContext(script, context);
    assert.equal(typeof ready, 'function');
    ready();
    context.refreshRemoveBgTokenBalance();
    context.refreshCloudflareBucketStats();
    context.refreshMarketCheckQuota();
    assert.equal(requests.length, 0, 'Missing service cards must not trigger optional service requests');

    context.refreshNow();
    assert.equal(requests.length, 1, 'General system health must keep polling');
    assert.equal(requests[0].url, '/Health/HealthJson');
    requests[0].success({ Totals: {}, IIS: {}, OpenAI: { Configured: false } });
    requests[0].complete();
}

(async () => {
    await checkBackgroundRemoval();
    checkHealthWithoutServiceCards();
    console.log('PASS: remove.bg switch hides generated controls and blocks UI requests; absent Health service cards skip optional requests while system polling still renders safely.');
})().catch(error => { console.error(error); process.exitCode = 1; });
