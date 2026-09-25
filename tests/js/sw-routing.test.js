// REA-01: the service worker caches by allow-list. Money, identity and account pages must never be stored.
// Run: node tests/js/sw-routing.test.js
'use strict';
const assert = require('assert');
const path = require('path');

global.self = { addEventListener() {}, location: { origin: 'https://krishilink.test' } };
const { routeFor } = require(path.join(__dirname, '..', '..', 'wwwroot', 'sw.js'));

const cases = [
    // [method, sameOrigin, path, mode, expected]
    ['GET', true, '/css/site.css', 'no-cors', 'static'],
    ['GET', true, '/js/site.js', 'no-cors', 'static'],
    ['GET', true, '/lib/bootstrap/dist/css/bootstrap.min.css', 'no-cors', 'static'],
    ['GET', true, '/images/icons/icon-192.png', 'no-cors', 'static'],
    ['GET', true, '/manifest.json', 'cors', 'static'],
    ['GET', true, '/Advisory/CalendarJson', 'cors', 'calendar'],
    ['GET', true, '/Advisory', 'navigate', 'advisory'],
    ['GET', true, '/Advisory/Calendar', 'navigate', 'advisory'],
    ['GET', true, '/Advisory/Alerts', 'navigate', 'advisory'],
    ['GET', true, '/Advisory/Suggestions', 'navigate', 'advisory'],
    ['GET', true, '/Advisory/Planner', 'navigate', 'advisory'],
    // Never cached: accounts, money, verification, owners, anything personal.
    ['GET', true, '/Account/Login', 'navigate', 'network'],
    ['GET', true, '/Account/Profile', 'navigate', 'network'],
    ['GET', true, '/Bookings', 'navigate', 'network'],
    ['GET', true, '/Bookings/Pay', 'navigate', 'network'],
    ['GET', true, '/Verify/Receipt', 'navigate', 'network'],
    ['GET', true, '/HarvestPlan/Season/5', 'navigate', 'network'],
    ['GET', true, '/Farmer/Dashboard', 'navigate', 'network'],
    ['GET', true, '/EquipmentOwner', 'navigate', 'network'],
    ['GET', true, '/Advisory/AlertFeedback', 'navigate', 'network'],
    ['GET', true, '/Advisory/PlannerIcs', 'navigate', 'network'],
    // Data requests other than the calendar, writes and other origins are not touched at all.
    ['GET', true, '/Equipment/FilterData', 'cors', null],
    ['GET', true, '/Bookings/Pay', 'cors', null],
    ['GET', true, '/Advisory/WeatherAlertsJson', 'cors', null],
    ['POST', true, '/Advisory', 'navigate', null],
    ['POST', true, '/Bookings/Pay', 'navigate', null],
    ['GET', false, '/css/site.css', 'no-cors', null],
];

let failed = 0;
for (const [method, same, pathname, mode, expected] of cases) {
    const actual = routeFor(method, same, pathname, mode);
    try {
        assert.strictEqual(actual, expected);
    } catch {
        failed++;
        console.log(`FAIL ${method} ${pathname} (${mode}): expected ${expected}, got ${actual}`);
    }
}
console.log(`${cases.length} routes checked, ${failed} failed`);
process.exit(failed ? 1 : 0);
