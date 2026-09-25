/*
 * Content-Security-Policy-safe replacement for inline on*= attributes.
 *
 * Markup declares data-onclick / data-onchange / data-oninput / data-onsubmit / data-onkeydown with the same short
 * statements an inline handler would hold, e.g.  data-onclick="event.stopPropagation(); KrishiFavorites.toggle(this)".
 * Nothing is eval'd: the text is parsed, and it may only call the functions in ALLOWED_CALLS, read the properties in
 * ALLOWED_READS and write the targets in ALLOWED_WRITES. Markup injected through an XSS bug therefore cannot reach
 * fetch, cookies or storage the way an inline handler could. A new handler must add its function to ALLOWED_CALLS.
 *
 * Handlers are bound on the element itself (bubble phase, like an inline handler), including elements added later,
 * so stopPropagation and "return false" keep their inline meaning.
 */
(function (global) {
    'use strict';

    const EVENTS = ['click', 'change', 'input', 'submit', 'keydown'];

    const ALLOWED_CALLS = new Set([
        'applyDecision', 'applyGodownPointsTier', 'applyGodownPromoCode', 'applyPointsTier', 'applyPromoCode',
        'clearGodownPromoCode', 'clearPromoCode', 'clearSearch', 'clearSelection', 'clearWeekdayPresets',
        'confirmDeleteRecord', 'confirmDeleteRule', 'confirmSaveAvailability', 'confirmUnblockPeriod', 'copyCode',
        'copyCoordinates', 'goToPage', 'handleFileSelect', 'handleFormSubmit', 'markCompleted', 'markSelectedDates',
        'onFilterChanged', 'onKindChanged', 'onManualCodeKeydown', 'onSearchInput', 'onStartDateChanged', 'onUnitsChanged',
        'openAddToHarvestPlanModal', 'openBookingQrModal', 'openEditIntakeModal', 'openIntakeModal', 'openModifyModal',
        'openReleaseModal', 'openReviewModal', 'openSaveSearchModal', 'previewImage', 'promptReject', 'removeExistingImage',
        'resetAllFilters', 'saveRecommendation', 'scrollToBookingForm', 'scrollToForm', 'scrollToRequestForm',
        'selectCalendarDate', 'setPrimaryImage', 'setQuickRange', 'setQuickRangeMonth', 'setWeekdayPreset',
        'showAnalyzingState', 'showCropModal', 'showHidden', 'startQrScannerCamera', 'stopQrScannerCamera',
        'submitManualQrCode', 'switchGodownImage', 'switchMainImage', 'switchView', 'syncCapacity', 'syncDates',
        'syncLocation', 'syncMobileDates', 'syncMobileToDesktopCategory', 'syncMobileToDesktopType', 'syncPriceInputs',
        'syncPriceInputsFromMobile', 'syncSliderToMaxInput', 'syncUnits', 'toggleDateSelection', 'toggleNewPlanInput',
        'updatePhDisplay', 'updateRemainingCapacity', 'validateDates', 'validateListingForm',
        'KrishiFavorites.toggle', 'KrishiNotifications.markAsRead',
        'confirm', 'window.print', 'event.stopPropagation', 'event.preventDefault', 'this.form.submit', 'this.remove',
        'document.getElementById', 'document.getElementById().click'
    ]);
    const ALLOWED_READS = new Set(['this.value', 'this.value.length', 'this.files', 'this.parentElement', 'event.key']);
    const ALLOWED_WRITES = new Set(['location.href', 'document.getElementById().textContent']);

    function tokenize(source) {
        const tokens = [];
        let i = 0;
        while (i < source.length) {
            const c = source[i];
            if (/\s/.test(c)) { i++; continue; }
            if (c === "'" || c === '"') {
                let j = i + 1;
                let value = '';
                while (j < source.length && source[j] !== c) {
                    if (source[j] === '\\' && j + 1 < source.length) {
                        const escaped = source[j + 1];
                        value += escaped === 'n' ? '\n' : escaped === 't' ? '\t' : escaped;
                        j += 2;
                        continue;
                    }
                    value += source[j++];
                }
                if (j >= source.length) throw new Error('unterminated string');
                tokens.push({ t: 'lit', v: value });
                i = j + 1;
                continue;
            }
            const number = /^\d+(?:\.\d+)?/.exec(source.slice(i));
            if (number) { tokens.push({ t: 'lit', v: parseFloat(number[0]) }); i += number[0].length; continue; }
            const word = /^[A-Za-z_$][\w$]*/.exec(source.slice(i));
            if (word) {
                const v = word[0];
                if (v === 'true' || v === 'false' || v === 'null') tokens.push({ t: 'lit', v: v === 'null' ? null : v === 'true' });
                else tokens.push({ t: 'id', v });
                i += v.length;
                continue;
            }
            if ('.,();=+-'.includes(c)) { tokens.push({ t: c }); i++; continue; }
            throw new Error('unexpected character ' + c);
        }
        return tokens;
    }

    function parse(source) {
        const tokens = tokenize(source);
        let p = 0;
        const is = (t) => p < tokens.length && tokens[p].t === t;
        const take = (t) => { if (!is(t)) throw new Error('expected ' + t); return tokens[p++]; };

        function operand() {
            if (is('-')) { p++; const n = take('lit'); if (typeof n.v !== 'number') throw new Error('bad number'); return { k: 'lit', v: -n.v }; }
            if (is('lit')) return { k: 'lit', v: tokens[p++].v };
            if (is('(')) { p++; const inner = expression(); take(')'); return inner; }
            let node = { k: 'id', name: take('id').v };
            node.path = node.name;
            for (;;) {
                if (is('.')) {
                    p++;
                    const name = take('id').v;
                    node = { k: 'get', obj: node, name, path: node.path + '.' + name };
                } else if (is('(')) {
                    p++;
                    const args = [];
                    if (!is(')')) {
                        args.push(expression());
                        while (is(',')) { p++; args.push(expression()); }
                    }
                    take(')');
                    node = { k: 'call', fn: node, args, path: node.path + '()' };
                } else {
                    return node;
                }
            }
        }

        function expression() {
            let left = operand();
            while (is('+')) { p++; left = { k: 'add', l: left, r: operand() }; }
            return left;
        }

        const program = [];
        while (p < tokens.length) {
            if (is(';')) { p++; continue; }
            let statement;
            if (tokens[p].t === 'id' && tokens[p].v === 'return') {
                p++;
                statement = { k: 'return', e: expression() };
            } else {
                const target = expression();
                if (is('=')) { p++; statement = { k: 'assign', target, value: expression() }; }
                else statement = { k: 'expr', e: target };
            }
            program.push(statement);
            if (p < tokens.length && !is(';')) throw new Error('expected ;');
        }
        return program;
    }

    function root(name, scope, inChain) {
        if (name === 'this') return scope.element;
        if (name === 'event') return scope.event;
        if (!inChain) throw new Error(name + ' cannot be used as a value');
        if (name === 'window') return global;
        if (name === 'document') return global.document;
        if (name === 'location') return global.location;
        return global[name];
    }

    function evaluate(node, scope, inChain) {
        switch (node.k) {
            case 'lit': return node.v;
            case 'add': return evaluate(node.l, scope, false) + evaluate(node.r, scope, false);
            case 'id': return root(node.name, scope, inChain);
            case 'get': {
                if (!inChain && !ALLOWED_READS.has(node.path)) throw new Error('reading ' + node.path + ' is not allowed');
                const obj = evaluate(node.obj, scope, true);
                return obj == null ? undefined : obj[node.name];
            }
            case 'call': {
                const fn = node.fn;
                if (!ALLOWED_CALLS.has(fn.path)) throw new Error('calling ' + fn.path + ' is not allowed');
                let self;
                let target;
                if (fn.k === 'get') {
                    self = evaluate(fn.obj, scope, true);
                    target = self == null ? undefined : self[fn.name];
                } else if (fn.k === 'id') {
                    self = global;
                    target = global[fn.name];
                } else {
                    throw new Error('unsupported call');
                }
                if (typeof target !== 'function') throw new Error(fn.path + ' is not a function');
                return target.apply(self, node.args.map((arg) => evaluate(arg, scope, false)));
            }
            default: throw new Error('unsupported expression');
        }
    }

    function assign(target, value, scope) {
        if (target.k !== 'get' || !ALLOWED_WRITES.has(target.path)) throw new Error('writing ' + (target.path || '?') + ' is not allowed');
        if (target.path === 'location.href' && !(typeof value === 'string' && value.startsWith('/') && !value.startsWith('//'))) {
            throw new Error('location.href may only be set to a same-site path');
        }
        const obj = evaluate(target.obj, scope, true);
        if (obj == null) throw new Error(target.path + ' has no target');
        obj[target.name] = value;
    }

    const programs = new Map();

    function run(element, event, source) {
        let program = programs.get(source);
        if (!program) { program = parse(source); programs.set(source, program); }
        const scope = { element, event };
        for (const statement of program) {
            if (statement.k === 'return') {
                if (evaluate(statement.e, scope, false) === false) event.preventDefault();
                return;
            }
            if (statement.k === 'assign') assign(statement.target, evaluate(statement.value, scope, false), scope);
            else evaluate(statement.e, scope, false);
        }
    }

    const boundTypes = new WeakMap();

    function bind(element) {
        let types = boundTypes.get(element);
        for (const type of EVENTS) {
            if (!element.hasAttribute('data-on' + type) || (types && types.has(type))) continue;
            if (!types) { types = new Set(); boundTypes.set(element, types); }
            types.add(type);
            element.addEventListener(type, function (event) {
                const source = this.getAttribute('data-on' + type);
                if (!source) return;
                try {
                    run(this, event, source);
                } catch (error) {
                    console.error('KrishiLink handler failed: ' + source, error);
                }
            });
        }
    }

    const SELECTOR = EVENTS.map((type) => '[data-on' + type + ']').join(',');

    function bindTree(node) {
        if (node.nodeType !== 1) return;
        if (node.matches(SELECTOR)) bind(node);
        node.querySelectorAll(SELECTOR).forEach(bind);
    }

    /** Reveals elements hidden with .d-none and removes the button that asked for them. */
    global.showHidden = function (selector, button) {
        global.document.querySelectorAll(selector).forEach((el) => el.classList.remove('d-none'));
        if (button) button.remove();
    };

    global.KrishiInlineHandlers = { parse, run };

    if (global.document && global.MutationObserver) {
        bindTree(global.document.documentElement);
        new global.MutationObserver((mutations) => {
            for (const mutation of mutations) {
                if (mutation.type === 'attributes') bind(mutation.target);
                else mutation.addedNodes.forEach(bindTree);
            }
        }).observe(global.document.documentElement, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: EVENTS.map((type) => 'data-on' + type)
        });
    }
})(typeof window !== 'undefined' ? window : globalThis);
