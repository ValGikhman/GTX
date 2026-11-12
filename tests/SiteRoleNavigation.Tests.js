const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Scripts/chat-bot.js'), 'utf8');
const start = source.indexOf('        function hasNavigationAccess(requiredRole)');
const end = source.indexOf('        function openNavigationDestination(destination)', start);
assert.ok(start >= 0 && end > start, 'Locate the shipped chat navigation authorization function');
const context = vm.createContext({
    window: {},
    $: { trim: value => value.trim(), grep: (values, predicate) => values.filter(predicate) }
});
vm.runInContext(source.slice(start, end), context);

const menuSource = fs.readFileSync(path.join(__dirname, '../Common/MajordomeMenu.cs'), 'utf8');
const menuKeys = menuSource.match(/enum MajordomeItem\s*\{([^}]+)\}/)[1].split(',').map(key => key.trim());
const regularKeys = menuKeys.filter(key => !['Roles', 'Profile', 'Services'].includes(key));

assert.equal(context.hasNavigationAccess(null), true, 'Public destinations do not require a login');
for (const role of ['user', 'Owner', 'Manager', 'Detail Team', 'Majordome']) {
    context.window.gtx = { currentRole: role, menuPermissions: [] };
    for (const key of menuKeys) {
        assert.equal(context.hasNavigationAccess(key), false, `${role} must not bypass a missing ${key} grant`);
    }
    assert.equal(context.hasNavigationAccess('administrator'), false, 'Role names must not imply administrator access');
}
for (const role of ['Owner', 'Manager', 'Detail Team']) {
    context.window.gtx = { currentRole: role, menuPermissions: regularKeys.slice() };
    for (const key of menuKeys) {
        assert.equal(context.hasNavigationAccess(key), regularKeys.includes(key), `${role}: wrong access for ${key}`);
    }
    assert.equal(context.hasNavigationAccess('administrator'), false, `${role} is an ordinary role`);
    context.window.gtx.menuPermissions = ['Appraisal'];
    assert.equal(context.hasNavigationAccess('APPRAISAL'), true, 'Permission matching is case insensitive');
    assert.equal(context.hasNavigationAccess('VinDecoder'), false, 'Saved regular permission edits govern navigation');
    assert.equal(context.hasNavigationAccess('Roles'), false);
    assert.equal(context.hasNavigationAccess('Profile'), false);
    assert.equal(context.hasNavigationAccess('Services'), false);
}
context.window.gtx = { currentRole: 'Majordome', menuPermissions: menuKeys.slice() };
for (const key of menuKeys) assert.equal(context.hasNavigationAccess(key), true, `Configured administrator can open ${key}`);
assert.equal(context.hasNavigationAccess('administrator'), true);
assert.equal(context.hasNavigationAccess('UndefinedPage'), false, 'Unknown destinations do not inherit administrator access');
console.log('PASS: shipped chat navigation honors actual menu grants, edited Owner/custom permissions, reserved pages and administrator authorization.');
