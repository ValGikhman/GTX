// Run with node tests/blog-media-preview.js; no media or network requests are made.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const editSource = fs.readFileSync(path.join(__dirname, '../Views/Shared/_BlogPostEdit.cshtml'), 'utf8');
const modalSource = fs.readFileSync(path.join(__dirname, '../Views/Shared/_BlogPostModal.cshtml'), 'utf8');
const previewStart = editSource.indexOf("        $form.triggerHandler('blog:dispose');");
const previewEnd = editSource.lastIndexOf('        syncContent();');
assert.ok(previewStart >= 0 && previewEnd > previewStart);
const previewCode = editSource.slice(previewStart, previewEnd);
const destroyStart = modalSource.indexOf('    function destroyBlogEditors()');
const destroyEnd = modalSource.indexOf('    function openModal(', destroyStart);
assert.ok(destroyStart >= 0 && destroyEnd > destroyStart);

class Node {
    constructor(value = '') {
        this.value = value;
        this.attributes = {};
        this.style = {};
        this.handlers = new Map();
        this.pauses = 0;
        this.loads = 0;
    }
    get src() { return this.attributes.src; }
    set src(value) { this.attributes.src = value; }
    get type() { return this.attributes.type; }
    set type(value) { this.attributes.type = value; }
    removeAttribute(name) { delete this.attributes[name]; }
    hasAttribute(name) { return Object.hasOwn(this.attributes, name); }
    pause() { this.pauses++; }
    load() { this.loads++; }
}

function load(initialValue) {
    const input = new Node(initialValue);
    const image = new Node();
    const video = new Node();
    const source = new Node();
    const form = new Node();
    const modal = new Node();
    const modalBody = new Node();
    const editorField = new Node();
    editorField.editor = {};
    const timers = new Map();
    let nextTimer = 0;
    function wrap(node) {
        return {
            get() { return node; },
            val(value) { if (arguments.length) { node.value = value; return this; } return node.value; },
            on(events, handler) { events.split(/\s+/).forEach(event => node.handlers.set(event, handler)); return this; },
            off(event) {
                for (const key of node.handlers.keys()) {
                    if (key === event || (event.startsWith('.') && key.endsWith(event))) node.handlers.delete(key);
                }
                return this;
            },
            triggerHandler(event) {
                for (const [key, callback] of [...node.handlers]) {
                    if (key === event || key.startsWith(event + '.')) callback.call(node);
                }
                return this;
            },
            each(callback) { callback.call(node); return this; },
            data(key) { assert.equal(key, 'add-edit'); return node.editor; },
            addEdit(command) { assert.equal(command, 'destroy'); node.editor = null; return this; },
            empty() { node.emptied = true; return this; },
            find(selector) {
                const found = { '#MediaUrl': input, '#imagePreview': image, '#videoPreview': video, '[data-blog-video-source]': source }[selector];
                assert.ok(found, 'Recognize form-scoped preview element: ' + selector);
                return wrap(found);
            }
        };
    }
    const $ = value => {
        if (value instanceof Node) return wrap(value);
        const found = {
            '#blogModalBody form[data-blog-ajax="true"]': form,
            '#blogModalBody textarea': editorField,
            '#blogModal': modal,
            '#blogModalBody': modalBody
        }[value];
        assert.ok(found, 'Recognize modal cleanup selector: ' + value);
        return wrap(found);
    };
    const context = vm.createContext({ $, $form: wrap(form), URL, window: {
        location: { href: 'https://gtx.example/Blogs/Index' },
        setTimeout(callback) { const id = ++nextTimer; timers.set(id, callback); return id; },
        clearTimeout(id) { timers.delete(id); }
    } });
    function initialize() {
        vm.runInContext('(function () {\n' + previewCode
            + '\nthis.previewApi = { describePreview, renderPreviewFromInputValue, disposeMediaPreview };\n}).call(this);', context);
    }
    initialize();
    vm.runInContext(modalSource.slice(destroyStart, destroyEnd), context);
    return {
        context, input, image, video, source, form, modal, modalBody, timers, initialize,
        render(value) { context.previewApi.renderPreviewFromInputValue(value); },
        type(value) { input.value = value; wrap(input).triggerHandler('input'); },
        fire(node, event) { wrap(node).triggerHandler(event); },
        flush() { for (const [id, callback] of [...timers]) { timers.delete(id); callback(); } }
    };
}

const signed = 'https://cdn.example/Clip.MP4?token=a%2Fb&expires=123#t=2';
const preview = load(signed);
assert.equal(preview.source.src, signed, 'Video URLs remain exactly as entered, including signatures and fragments');
assert.equal(preview.source.type, 'video/mp4');
assert.equal(preview.video.style.display, '');
assert.equal(preview.image.style.display, 'none');
const beforeRepeat = preview.video.loads;
preview.render(signed);
assert.equal(preview.video.loads, beforeRepeat, 'Repeated events for the same URL keep playback intact');

for (const [extension, type] of [['m4v', 'video/mp4'], ['webm', 'video/webm'], ['ogv', 'video/ogg']]) {
    const loadsBeforeSwitch = preview.video.loads;
    preview.render('https://cdn.example/car.' + extension + '?v=1#preview');
    assert.equal(preview.video.loads, loadsBeforeSwitch + 1, 'Switching videos performs one reload');
    assert.equal(preview.source.type, type);
    assert.equal(preview.image.style.display, 'none');
    assert.equal(preview.video.style.display, '');
}

const beforeImage = preview.video.pauses;
const imageUrl = 'https://cdn.example/Photo.JpG?signature=keep%2Bthis#photo';
preview.render(imageUrl);
assert.ok(preview.video.pauses > beforeImage, 'Switching to an image pauses video playback');
assert.equal(preview.source.src, undefined);
assert.equal(preview.source.type, undefined);
assert.equal(preview.video.style.display, 'none');
assert.equal(preview.image.src, imageUrl, 'Image URLs also keep existing query strings intact');
assert.equal(preview.image.style.display, '');
for (const extension of ['jpeg', 'png', 'gif', 'webp', 'bmp', 'svg']) {
    preview.render('/images/car.' + extension);
    assert.equal(preview.image.src, '/images/car.' + extension, 'Legacy image extensions remain supported');
}
preview.render('/videos/car.WEBM?token=1');
assert.equal(preview.source.type, 'video/webm', 'Root-relative media URLs work like server-rendered media');

for (const invalid of ['', ' ', 'not a url', 'https://', 'javascript:alert(1).mp4', 'data:video/mp4;base64,123',
    'ftp://cdn.example/car.mp4', '//cdn.example/car.mp4', '/\\cdn.example/car.mp4', 'https://youtube.com/watch?v=123',
    'https://cdn.example/file.mov', 'https://cdn.example/file.constructor', 'https://cdn.example/car.mp%34']) {
    preview.render(invalid);
    assert.equal(preview.image.src, undefined, 'Invalid/unsupported input hides images: ' + invalid);
    assert.equal(preview.source.src, undefined, 'Invalid/unsupported input clears video resources: ' + invalid);
    assert.equal(preview.video.style.display, 'none');
}
preview.render(signed);
const loadsBeforeError = preview.video.loads;
preview.source.onerror({ type: 'error' });
assert.equal(preview.source.src, undefined, 'Failed video sources are cleared');
assert.equal(preview.video.loads, loadsBeforeError + 1, 'Source errors still reset the video element');
preview.render(imageUrl);
preview.image.onerror();
assert.equal(preview.image.src, undefined, 'Failed image previews are cleared');

preview.type('https://cdn.example/first.mp4');
preview.type('https://cdn.example/last.mp4?signature=final');
assert.equal(preview.timers.size, 1, 'Typing debounces metadata requests');
preview.flush();
assert.equal(preview.source.src, 'https://cdn.example/last.mp4?signature=final');
preview.type('https://cdn.example/pending.webm');
const beforeClose = preview.video.pauses;
preview.fire(preview.modal, 'hide.bs.modal');
assert.ok(preview.video.pauses > beforeClose, 'Closing the modal stops playback immediately');
assert.equal(preview.source.src, undefined);
assert.equal(preview.timers.size, 0, 'Closing cancels pending preview work');
assert.equal(preview.input.handlers.size, 0, 'Disposed forms do not retain input handlers');
preview.flush();
assert.equal(preview.source.src, undefined, 'A disposed form cannot restart playback');
preview.fire(preview.modal, 'hidden.bs.modal');
assert.equal(preview.modalBody.emptied, true);

const reopened = load(signed);
reopened.type('https://cdn.example/pending.mp4');
reopened.initialize();
assert.equal(reopened.timers.size, 0, 'Reinitialization disposes any old pending callback');
assert.equal(reopened.input.handlers.size, 2, 'Reinitializing a form retains just one handler per event');
reopened.context.destroyBlogEditors();
assert.equal(reopened.source.src, undefined, 'Replacing the editor stops media through the shared cleanup path');
assert.equal(reopened.input.handlers.size, 0);

console.log('PASS: image/video preview selection, exact signed URLs, MIME types, safe URL validation, error handling, debounce and modal/replacement cleanup.');
