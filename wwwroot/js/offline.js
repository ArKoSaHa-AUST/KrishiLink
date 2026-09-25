/*
 * The offline page (REA-01): this month's sowing and harvest from the saved crop calendar, and the last forecast the
 * farmer looked at, each stamped with when it was saved. Rendered with textContent only.
 */
(function () {
    'use strict';

    const root = document.querySelector('.krishi-offline');
    if (!root) return;
    const msg = root.dataset;
    const bangla = msg.bangla === '1';
    const months = (msg.months || '').split('|');

    document.getElementById('offlineRetry')?.addEventListener('click', function () { window.location.reload(); });

    function when(iso) {
        const date = iso ? new Date(iso) : null;
        if (!date || isNaN(date)) return '';
        return date.toLocaleString(bangla ? 'bn-BD' : 'en-GB', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Dhaka' });
    }

    function el(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text) node.textContent = text;
        return node;
    }

    // The crop calendar is seasonal: "this month" is the month in Bangladesh, whatever the phone's timezone.
    const month = parseInt(new Intl.DateTimeFormat('en-US', { timeZone: 'Asia/Dhaka', month: 'numeric' }).format(new Date()), 10);

    function cropList(title, crops) {
        const block = el('div', 'mb-3');
        block.appendChild(el('h3', 'h6 fw-semibold mb-2', `${title} · ${months[month - 1] || ''}`));
        const list = el('ul', 'list-unstyled mb-0 d-grid gap-2');
        crops.forEach(function (crop) {
            const item = el('li', 'border rounded-3 p-2');
            item.appendChild(el('div', 'fw-semibold', bangla && crop.nameBn ? crop.nameBn : crop.name));
            const tip = bangla && crop.tipBn ? crop.tipBn : crop.tip;
            if (tip) item.appendChild(el('div', 'small text-muted', tip.length > 180 ? tip.slice(0, 177) + '…' : tip));
            list.appendChild(item);
        });
        block.appendChild(list);
        return block;
    }

    (async function renderCalendar() {
        const target = document.getElementById('offlineCalendar');
        const stamp = document.getElementById('offlineCalendarStamp');
        try {
            const response = await fetch('/Advisory/CalendarJson', { headers: { 'Accept': 'application/json' } });
            if (!response.ok) throw new Error('calendar');
            const data = await response.json();
            const crops = data.crops || [];
            if (!crops.length) throw new Error('empty');
            const saved = when(response.headers.get('X-Saved-At') || data.generatedAt);
            stamp.textContent = saved ? msg.msgSaved.replace('{0}', saved) : '';
            const sow = crops.filter(c => (c.sowing || []).includes(month));
            const harvest = crops.filter(c => (c.harvesting || []).includes(month));
            if (!sow.length && !harvest.length) { target.appendChild(el('p', 'text-muted mb-0', msg.msgNone)); return; }
            if (sow.length) target.appendChild(cropList(msg.msgSown, sow));
            if (harvest.length) target.appendChild(cropList(msg.msgHarvest, harvest));
        } catch (e) {
            target.appendChild(el('p', 'text-muted mb-0', msg.msgNoCalendar));
        }
    })();

    (function renderForecast() {
        const target = document.getElementById('offlineForecast');
        const stamp = document.getElementById('offlineForecastStamp');
        let snapshot = null;
        try { snapshot = JSON.parse(window.localStorage.getItem('krishilink.forecast') || 'null'); } catch (e) { snapshot = null; }
        if (!snapshot || !snapshot.district) { target.appendChild(el('p', 'text-muted mb-0', msg.msgNoForecast)); return; }
        stamp.textContent = msg.msgSaved.replace('{0}', when(snapshot.savedAt));
        target.appendChild(el('p', 'fw-semibold mb-2', bangla && snapshot.conditionBn ? snapshot.conditionBn : snapshot.condition));
        target.appendChild(el('p', 'mb-0', msg.msgForecast
            .replace('{0}', snapshot.district).replace('{1}', Math.round(snapshot.max)).replace('{2}', Math.round(snapshot.min))
            .replace('{3}', Math.round(snapshot.humidity)).replace('{4}', Math.round(snapshot.rain))));
    })();
})();
