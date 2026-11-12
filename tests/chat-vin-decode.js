const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Scripts/chat-bot.js'), 'utf8');

// Exercise the shipped render, modal lifecycle and AJAX-success handlers.
class Element {
    constructor(tag, attributes = {}) {
        this.tag = tag;
        this.attributes = { ...attributes };
        this.properties = {};
        this.children = [];
        this.classes = new Set((attributes.class || '').split(' ').filter(Boolean));
        this.handlers = {};
        this.focusCount = 0;
    }
    addClass(value) { this.classes.add(value); return this; }
    removeClass(value) { this.classes.delete(value); return this; }
    append(child) { this.children.push(child); return this; }
    appendTo(parent) { parent.append(this); return this; }
    prop(name, value) { this.properties[name] = value; return this; }
    get() { return this; }
    text(value) { this.textValue = value; return this; }
    empty() { this.children = []; this.reportHtml = ''; return this; }
    on(event, handler) { this.handlers[event] = handler; return this; }
    fire(event) { if (this.handlers[event]) this.handlers[event].call(this); return this; }
    focus() { this.focusCount++; }
    find(selector) {
        assert.equal(selector, 'details.avd-style');
        return this.styles || (this.styles = new Element('details'));
    }
    remove() { this.removed = true; return this; }
    html() { assert.fail('Use the existing safe HTML injection helper'); }
}
class Collection {
    constructor(elements) { this.elements = elements.slice(); }
    not(other) { return new Collection(this.elements.filter(element => !other.elements.includes(element))); }
    addClass(value) { this.elements.forEach(element => element.addClass(value)); return this; }
}
const backgroundBackdrop = new Element('backdrop');
let backdrops = [backgroundBackdrop];
const $ = (markup, attributes) => {
    if (markup === '.modal-backdrop') return new Collection(backdrops);
    assert.match(markup, /^<\w+>$/, 'Only literal element creation is allowed');
    assert.notEqual(markup, '<iframe>', 'Reports use a modal container, not an iframe');
    return new Element(markup.slice(1, -1), attributes);
};
$.trim = value => String(value || '').trim();

let saves = 0;
let savedState = '';
let navigation;
const messages = new Element('messages');
const modal = new Element('modal');
const modalBody = new Element('modal-body');
const modalTitle = new Element('modal-title');
const injections = [];
let shown = 0;
let modalVisible = false;
let reportBackdrop;
const modalInstance = {
    show(trigger) {
        assert.equal(modalBody.reportHtml, injections.at(-1).html, 'Populate the report before showing the modal');
        this.trigger = trigger;
        if (modalVisible) return;
        shown++;
        modalVisible = true;
        reportBackdrop = new Element('backdrop');
        backdrops.push(reportBackdrop);
    },
    hide() {
        modalVisible = false;
        backdrops = backdrops.filter(element => element !== reportBackdrop);
        modal.fire('hidden.bs.modal');
    }
};
const context = vm.createContext({
    $, $messages: messages, $thinking: new Element('thinking'),
    $vinReportModal: modal, $vinReportBody: modalBody, $vinReportTitle: modalTitle,
    $lead: new Element('lead'), $panel: new Element('panel'), $chatForm: new Element('form'),
    vinReportReturnFocus: null,
    window: {
        bootstrap: { Modal: {
            getOrCreateInstance(element) { assert.equal(element, modal); return modalInstance; },
            getInstance(element) { assert.equal(element, modal); return modalVisible ? modalInstance : null; }
        } },
        gtxSecurity: { setSafeHtml(target, html) {
            assert.equal(target, modalBody, 'Inject only into the dedicated modal body');
            injections.push({ target, html });
            target.reportHtml = html;
        } }
    },
    document: { documentElement: { contains(element) { return !element.removed; } } },
    conversation: [], maximumStoredMessages: 40,
    appendAssistantText(container, text) { container.append({ tag: 'copy', text }); },
    appendLinkedText(container, text) { container.append({ tag: 'linked-copy', text }); },
    appendMarketReply(container) { container.append({ tag: 'market' }); },
    appendStructuredVehicles(container) { container.append({ tag: 'vehicles' }); },
    scrollMessages() {},
    closeCommandGuide() {}, removeStoredChat() {}, requestGreeting() {},
    saveChat() { saves++; savedState = JSON.stringify(context.conversation); },
    handleServerNavigation(value) { navigation = value; }
});

function runSlice(startMarker, endMarker) {
    const start = source.indexOf(startMarker);
    const end = source.indexOf(endMarker, start);
    assert.ok(start >= 0 && end > start, 'Locate actual chat functions');
    vm.runInContext(source.slice(start, end), context);
}
runSlice('        function resultValue(', '        function formatWholeNumber(');
runSlice('        function openVinReport(', '        function errorMessage(');
runSlice('        function resetChat(', '        function requestGreeting(');
const hiddenHook = source.split(/\r?\n/).find(line => line.includes('$vinReportModal.on("hidden.bs.modal"'));
assert.ok(hiddenHook, 'Locate the actual modal close handler');
vm.runInContext(hiddenHook, context);

const ajaxStart = source.indexOf('                    ProgressId: progressId');
const successStart = source.indexOf('            }).done(function (data) {', ajaxStart);
const successBody = successStart + '            }).done(function (data) {'.length;
const successEnd = source.indexOf('            }).fail(function (xhr) {', successBody);
assert.ok(ajaxStart >= 0 && successStart > ajaxStart && successEnd > successBody, 'Locate actual message success handler');
vm.runInContext('function receive(data) {' + source.slice(successBody, successEnd) + '\n}', context);

const vin = 'ZASPAKBN9P7D56780';
const html = '<style>.avd-dataone{color:red}</style>\n<section class="avd-dataone"><details class="avd-style"><summary>' + vin
    + '</summary><strong>2023 Alfa Romeo</strong></details></section>';
const reportButtons = [];

for (const data of [
    { Success: true, Reply: 'Your report is here.', PrivateReply: true, VinDecode: { Vin: vin, Html: html } },
    { success: true, reply: 'Your report is here.', privateReply: true, vinDecode: { vin, html } },
    { Success: true, Reply: 'Your report is here.', VinDecode: { Vin: vin, Html: html } }
]) {
    context.receive(data);
    const item = messages.children.at(-1);
    assert.ok(item.classes.has('has-vin-decode'));
    assert.equal(item.children[0].text, 'Your report is here.');
    const button = item.children[1];
    reportButtons.push(button);
    assert.equal(item.children.length, 2, 'Chat contains the intro and report button only');
    assert.equal(button.tag, 'button');
    assert.equal(button.attributes.text, 'View VIN report');
    assert.equal(button.attributes['aria-haspopup'], 'dialog');
    assert.equal(button.attributes['aria-controls'], 'gtxChatVinReportModal');
    assert.equal(modalBody.reportHtml || '', '', 'Receiving a report does not inject inline HTML');
    const before = injections.length;
    button.fire('click');
    assert.equal(injections.length, before + 1, 'Clicking the button injects through the safe helper');
    assert.equal(injections.at(-1).html, html, 'Pass complete provider HTML to the existing sanitizer');
    assert.equal(modalTitle.textValue, 'VIN report · ' + vin);
    assert.equal(modalBody.styles.properties.open, true, 'Top-level vehicle styles open for inspection');
    assert.equal(modalInstance.trigger, button);
    assert.ok(reportBackdrop.classes.has('gtx-chat-vin-backdrop'));
    assert.equal(backgroundBackdrop.classes.size, 0, 'Existing modal backdrops stay unchanged');
    assert.equal(context.conversation.length, 0, 'Reports stay out of conversation history even without PrivateReply');
    assert.equal(saves, 0);
    modalInstance.hide();
    assert.equal(modalBody.reportHtml, '', 'Closing clears injected report content and styles');
    assert.equal(modalTitle.textValue, 'VIN report');
    assert.equal(button.focusCount, 1, 'Closing returns focus to the report button');
}

const otherHtml = '<section class="avd-dataone">Another decoded VIN</section>';
context.receive({ Reply: 'Second report', VinDecode: { Vin: '1HGCM82633A004352', Html: otherHtml } });
messages.children.at(-1).children[1].fire('click');
assert.equal(injections.at(-1).html, otherHtml);
modalInstance.hide();
reportButtons[0].fire('click');
assert.equal(injections.at(-1).html, html, 'Earlier buttons reopen their own VIN report without another request');
assert.equal(modalTitle.textValue, 'VIN report · ' + vin);
const focusBeforeReset = reportButtons[0].focusCount;
context.resetChat();
assert.equal(modalVisible, false, 'Starting a new chat closes the report modal');
assert.equal(modalBody.reportHtml, '');
assert.equal(messages.children.length, 0);
assert.equal(reportButtons[0].focusCount, focusBeforeReset, 'Reset does not refocus a removed report button');
assert.equal(shown, 5, 'Each requested report opens in the shared modal');

context.receive({ Reply: 'Ordinary answer' });
assert.equal(messages.children.at(-1).children[0].text, 'Ordinary answer');
assert.equal(context.conversation.length, 1, 'Ordinary text still persists');
assert.equal(saves, 1);
assert.equal(savedState.includes(html), false);

context.receive({ Reply: 'Private answer', PrivateReply: true });
assert.equal(context.conversation.length, 1, 'Other private replies remain private');
context.receive({ Reply: 'Missing report', VinDecode: { Vin: vin, Html: '' } });
assert.equal(messages.children.at(-1).children[0].text, 'Missing report', 'Empty report falls back to normal text');
assert.equal(context.conversation.length, 1, 'Malformed report payloads also stay out of saved history');
context.receive({ Reply: 'Inventory', Vehicles: [{ Title: 'Vehicle' }] });
assert.equal(messages.children.at(-1).children[0].tag, 'vehicles');
context.receive({ Reply: 'Market', MarketReply: { Title: 'Market' } });
assert.equal(messages.children.at(-1).children[0].tag, 'market');
context.receive({ Navigation: { Label: 'Inventory' } });
assert.equal(navigation.Label, 'Inventory');
assert.equal(savedState.includes(html), false, 'Later saves never include an earlier report');

console.log('PASS: report buttons, safe modal injection, reopen/close/reset, focus, isolated backdrops, private history, Pascal/camel responses and existing chat result paths.');
