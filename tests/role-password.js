const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Scripts/roles.js'), 'utf8');
const view = fs.readFileSync(path.join(__dirname, '../Views/Roles/_Edit.cshtml'), 'utf8');
const ruleNames = Array.from(view.matchAll(/data-password-rule="([^"]+)"/g), match => match[1]);
assert.deepEqual(ruleNames, ['length', 'uppercase', 'lowercase', 'number', 'symbol', 'whitespace']);

// Small jQuery adapter for the actual form validator; no browser dependencies needed.
class Collection {
    constructor(items) { this.items = items; }
    each(callback) { this.items.forEach(item => callback.call(item)); return this; }
    find(selector) {
        return new Collection(this.items.flatMap(item => {
            if (selector === '[data-password-rule]') return item.rules;
            if (selector === '#rolePasswordMatch') return [item.match];
            if (selector === 'i') return [item.icon];
            if (selector === '.password-rule-status') return [item.status];
            throw Error('Unexpected selector: ' + selector);
        }));
    }
    removeClass(names) {
        this.items.forEach(item => names.split(' ').forEach(name => item.classes.delete(name)));
        return this;
    }
    addClass(name) { this.items.forEach(item => item.classes.add(name)); return this; }
    attr(name, value) { this.items.forEach(item => { item[name] = value; }); return this; }
    text(value) { this.items.forEach(item => { item.text = value; }); return this; }
    data(name) { assert.equal(name, 'password-rule'); return this.items[0].rule; }
}
const $ = item => item instanceof Collection ? item : new Collection([item]);
const context = vm.createContext({ $ });
const start = source.indexOf('    function passwordPolicyResults(value)');
const end = source.indexOf('    $("#roleModalBody").on("input change"', start);
assert.ok(start >= 0 && end > start, 'Locate the actual password validator functions');
vm.runInContext(source.slice(start, end), context);

function indicator(rule) {
    return { rule, classes: new Set(['text-muted']), icon: {}, status: {} };
}
function field(value) {
    return { value, validationMessage: '', setCustomValidity(message) { this.validationMessage = message; } };
}
function form(id = '123', password = '', confirmation = '') {
    return {
        elements: { Id: { value: id }, Password: field(password), ConfirmPassword: field(confirmation) },
        rules: ruleNames.map(indicator), match: indicator('match')
    };
}
function expectStatus(item, state) {
    const expected = {
        neutral: ['text-muted', 'bi-dash-circle', 'Not checked: '],
        pass: ['text-success', 'bi-check-circle-fill', 'Met: '],
        fail: ['text-danger', 'bi-x-circle-fill', 'Not met: ']
    }[state];
    assert.deepEqual(Array.from(item.classes), [expected[0]]);
    assert.equal(item.icon.class, 'bi me-1 ' + expected[1]);
    assert.equal(item.status.text, expected[2]);
}
function allRulesPass(value) {
    return Object.values(context.passwordPolicyResults(value)).every(Boolean);
}
const valid = 'RolePassword42!';
assert.equal(valid.length, 15);
assert.ok(allRulesPass(valid));
assert.ok(allRulesPass('Aa7!xxxx'), '8 characters is the minimum');
assert.ok(allRulesPass('Aa7!' + 'x'.repeat(28)), '32 characters is the maximum');
assert.equal(allRulesPass('Aa7!xxx'), false, '7 characters is too short');
assert.equal(allRulesPass('Aa7!' + 'x'.repeat(29)), false, '33 characters is too long');
for (const [value, failedRule] of [
    ['rolepassword42!', 'uppercase'], ['ROLEPASSWORD42!', 'lowercase'],
    ['RolePasswordxx!', 'number'], ['RolePassword42x', 'symbol'],
    ['RolePassword42\u00e9', 'symbol']
]) {
    const test = form('123', value, value);
    context.validateRolePassword(test);
    test.rules.forEach(item => expectStatus(item, item.rule === failedRule ? 'fail' : 'pass'));
    assert.notEqual(test.elements.Password.validationMessage, '');
}
for (const whitespace of [' ', '\t', '\n', '\r', '\u0085', '\u00a0', '\u2003', '\u2028', '\ufeff']) {
    assert.equal(context.passwordPolicyResults('Role' + whitespace + 'Password42!').whitespace, false);
}
for (let codePoint = 33; codePoint <= 126; codePoint++) {
    if ((codePoint >= 48 && codePoint <= 57) || (codePoint >= 65 && codePoint <= 90) || (codePoint >= 97 && codePoint <= 122)) continue;
    assert.ok(allRulesPass('RolePassword42' + String.fromCharCode(codePoint)), 'ASCII punctuation qualifies');
}

const editing = form();
context.validateRolePassword(editing);
[...editing.rules, editing.match].forEach(item => expectStatus(item, 'neutral'));
assert.equal(editing.elements.Password.validationMessage, '');
assert.equal(editing.elements.ConfirmPassword.validationMessage, '');

const creating = form('0');
context.validateRolePassword(creating);
[...creating.rules, creating.match].forEach(item => expectStatus(item, 'fail'));
assert.notEqual(creating.elements.Password.validationMessage, '');
assert.notEqual(creating.elements.ConfirmPassword.validationMessage, '');

editing.elements.Password.value = valid;
editing.elements.ConfirmPassword.value = valid;
context.validateRolePassword(editing);
[...editing.rules, editing.match].forEach(item => expectStatus(item, 'pass'));
assert.equal(editing.elements.Password.validationMessage, '');
assert.equal(editing.elements.ConfirmPassword.validationMessage, '');

editing.elements.Password.value = 'OtherPassword42!';
context.validateRolePassword(editing);
expectStatus(editing.match, 'fail');
assert.notEqual(editing.elements.ConfirmPassword.validationMessage, '');
editing.elements.ConfirmPassword.value = 'otherPassword42!';
context.validateRolePassword(editing);
expectStatus(editing.match, 'fail');

editing.elements.Password.value = '';
context.validateRolePassword(editing);
assert.notEqual(editing.elements.Password.validationMessage, '', 'Confirmation alone must not permit saving');
editing.elements.ConfirmPassword.value = '';
context.validateRolePassword(editing);
[...editing.rules, editing.match].forEach(item => expectStatus(item, 'neutral'));
assert.equal(editing.elements.Password.validationMessage, '');
assert.equal(editing.elements.ConfirmPassword.validationMessage, '');

context.validateRolePassword(null);
console.log('PASS: password rules, boundaries, symbols, whitespace, live status icons, confirmation and clearing unchanged edits.');
