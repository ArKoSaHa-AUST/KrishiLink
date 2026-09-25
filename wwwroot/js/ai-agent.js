/**
 * KrishiLink assistant panel (Views/Shared/_AiAgent.cshtml).
 *
 * Vanilla JS, no inline handlers (CSP). Model output is always escaped before rendering; only a tiny, known subset of
 * formatting is re-applied (bold, bullet lists, same-site links, source tags). Proposals are never built here: the
 * server renders them as real forms that post to the existing booking actions.
 */
(function () {
    'use strict';

    const root = document.getElementById('krishiAgent');
    if (!root) return;

    const $ = (id) => document.getElementById(id);
    const strings = JSON.parse(root.dataset.strings || '{}');
    const citationLabels = JSON.parse(root.dataset.citations || '{}');
    const canBook = root.dataset.canBook === 'true';
    const maxLength = parseInt(root.dataset.maxLength || '2000', 10);
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const scroller = $('krishiAgentScroll');
    const log = $('krishiAgentLog');
    const empty = $('krishiAgentEmpty');
    const typing = $('krishiAgentTyping');
    const typingLabel = $('krishiAgentTypingLabel');
    const status = $('krishiAgentStatus');
    const form = $('krishiAgentForm');
    const input = $('krishiAgentInput');
    const sendBtn = $('krishiAgentSend');
    const counter = $('krishiAgentCounter');
    const historyMenu = $('krishiAgentHistory');
    const historyBtn = $('krishiAgentHistoryBtn');
    const subtitle = $('krishiAgentSubtitle');
    const defaultSubtitle = subtitle ? subtitle.textContent : '';

    let conversationId = null;
    let loaded = false;
    let busy = false;
    let progressTimer = null;

    // ------------------------------------------------------------------ Transport

    function token() {
        return document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]')?.value || '';
    }

    async function post(url, payload) {
        const body = new URLSearchParams(Object.assign({}, payload, { __RequestVerificationToken: token() }));
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token(), 'Accept': 'application/json' },
            body: body.toString(),
            credentials: 'same-origin'
        });
    }

    async function getJson(url) {
        const res = await fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' });
        if (!res.ok) throw new Error(String(res.status));
        return res.json();
    }

    // ------------------------------------------------------------------ Rendering helpers

    function escapeHtml(text) {
        return String(text ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function el(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    /** Same-site paths only: "/Equipment/Details/3" yes; "//evil", "javascript:" no. */
    function safePath(href) {
        return /^\/(?!\/)[\w\-./?=&%#]*$/.test(href) ? href : null;
    }

    /** Escaped text → paragraphs, bullet lists, **bold**, [label](/path) links and [tool_name] source tags. */
    function formatReply(text) {
        const inline = (line) => escapeHtml(line)
            .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
            .replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (m, label, href) => {
                const path = safePath(href.replace(/&amp;/g, '&'));
                return path ? `<a href="${escapeHtml(path)}">${label}</a>` : label;
            })
            // Source tags: [search_equipment] or, as models often write in Bangla, 【search_equipment】.
            .replace(/\s?[[【]([a-z_]+)(?:,\s*[a-z_]+)*[\]】]/g, (m) => {
                const tools = m.replace(/[\s[\]【】]/g, '').split(',');
                const labels = tools.map((t) => citationLabels[t]).filter(Boolean);
                return labels.length ? ` <span class="krishi-agent-cite">${escapeHtml(labels.join(' · '))}</span>` : '';
            });

        const out = [];
        let list = null;
        for (const raw of String(text || '').split(/\r?\n/)) {
            const line = raw.trim();
            const bullet = line.match(/^(?:[-*•]|\d+[.)])\s+(.*)$/);
            if (bullet) {
                if (!list) { list = []; }
                list.push(`<li>${inline(bullet[1])}</li>`);
                continue;
            }
            if (list) { out.push(`<ul>${list.join('')}</ul>`); list = null; }
            if (line) out.push(`<p>${inline(line.replace(/^#+\s*/, ''))}</p>`);
        }
        if (list) out.push(`<ul>${list.join('')}</ul>`);
        return out.join('');
    }

    function scrollToEnd() {
        requestAnimationFrame(() => scroller.scrollTo({ top: scroller.scrollHeight, behavior: reduceMotion ? 'auto' : 'smooth' }));
    }

    function setEmpty(isEmpty) {
        empty.classList.toggle('d-none', !isEmpty);
    }

    function speaker(label) {
        return el('span', 'visually-hidden', label + ': ');
    }

    function addUserMessage(text) {
        const row = el('div', 'krishi-agent-msg krishi-agent-msg-user');
        const bubble = el('div', 'krishi-agent-bubble');
        bubble.append(speaker(strings.you), document.createTextNode(text));
        row.append(bubble);
        log.append(row);
        setEmpty(false);
        scrollToEnd();
    }

    /** Assistant row: avatar + stack of bubble, listing cards, proposal slot and sources. */
    function addAssistantMessage(message, variant) {
        const row = el('div', 'krishi-agent-msg krishi-agent-msg-assistant' + (variant ? ' is-' + variant : ''));
        const avatar = el('span', 'krishi-agent-avatar');
        avatar.setAttribute('aria-hidden', 'true');
        avatar.innerHTML = variant === 'error' ? '<i class="bi bi-cloud-slash"></i>' : '<i class="bi bi-stars"></i>';
        const stack = el('div', 'krishi-agent-stack');

        const bubble = el('div', 'krishi-agent-bubble');
        bubble.append(speaker(strings.assistant));
        const body = el('div', 'krishi-agent-text');
        body.innerHTML = formatReply(message.reply ?? message.content);
        bubble.append(body);

        if (message.fallbackUrl && safePath(message.fallbackUrl)) {
            const link = el('a', 'krishi-agent-fallback', strings.openPage);
            link.href = message.fallbackUrl;
            link.insertAdjacentHTML('beforeend', ' <i class="bi bi-arrow-right" aria-hidden="true"></i>');
            bubble.append(link);
        }
        stack.append(bubble);

        if (message.listings?.length) stack.append(renderListings(message.listings));
        if (message.proposalId) stack.append(renderProposalSlot(message.proposalId));
        if (message.citations?.length) stack.append(renderSources(message.citations));

        row.append(avatar, stack);
        log.append(row);
        setEmpty(false);
        scrollToEnd();
    }

    function renderListings(listings) {
        const list = el('ul', 'krishi-agent-listings list-unstyled mb-0');
        for (const item of listings) {
            const href = safePath(item.detailUrl);
            if (!href) continue;
            const li = el('li', 'krishi-agent-listing');

            const thumb = el('span', 'krishi-agent-thumb');
            if (item.imageUrl && /^https:\/\//.test(item.imageUrl)) {
                const img = document.createElement('img');
                img.src = item.imageUrl;
                img.alt = '';
                img.loading = 'lazy';
                img.decoding = 'async';
                thumb.append(img);
            } else {
                thumb.innerHTML = `<i class="bi ${item.type === 'godown' ? 'bi-box-seam' : 'bi-truck'}" aria-hidden="true"></i>`;
            }

            const info = el('div', 'krishi-agent-listing-info');
            const name = el('a', 'krishi-agent-listing-name', `#${item.id} ${item.name}`);
            name.href = href;
            const meta = el('span', 'krishi-agent-listing-meta', [item.subtitle, item.district].filter(Boolean).join(' · '));
            const facts = el('span', 'krishi-agent-listing-facts');
            facts.append(el('strong', '', item.rateText));
            if (item.rating > 0) {
                facts.insertAdjacentHTML('beforeend', ` <span class="krishi-agent-rating"><i class="bi bi-star-fill" aria-hidden="true"></i> ${escapeHtml(item.rating.toFixed(1))}</span>`);
            }
            if (item.isVerifiedOwner) {
                facts.insertAdjacentHTML('beforeend', ` <span class="krishi-agent-verified" title="${escapeHtml(strings.verified)}"><i class="bi bi-patch-check-fill" aria-hidden="true"></i><span class="visually-hidden">${escapeHtml(strings.verified)}</span></span>`);
            }
            info.append(name, meta, facts);

            const actions = el('div', 'krishi-agent-listing-actions');
            const view = el('a', 'btn btn-sm btn-krishi-outline rounded-pill', strings.view);
            view.href = href;
            actions.append(view);
            if (canBook) {
                const rent = el('button', 'btn btn-sm btn-krishi-primary rounded-pill', item.type === 'godown' ? strings.store : strings.rent);
                rent.type = 'button';
                rent.addEventListener('click', () => {
                    const template = item.type === 'godown' ? strings.storePrompt : strings.rentPrompt;
                    input.value = template.replace('{0}', item.id).replace('{1}', item.name);
                    autoGrow();
                    input.focus();
                    input.setSelectionRange(input.value.length, input.value.length);
                });
                actions.append(rent);
            }

            li.append(thumb, info, actions);
            list.append(li);
        }
        return list;
    }

    function renderSources(citations) {
        const wrap = el('p', 'krishi-agent-sources mb-0');
        wrap.append(el('span', 'krishi-agent-sources-label', strings.sources));
        for (const c of citations) {
            const path = c.url && safePath(c.url);
            const tag = path ? el('a', 'krishi-agent-source', c.label) : el('span', 'krishi-agent-source', c.label);
            if (path) tag.href = path;
            wrap.append(tag);
        }
        return wrap;
    }

    /** The confirm form is rendered by the server (anti-forgery token, allow-listed action) and only inserted here. */
    function renderProposalSlot(id) {
        const slot = el('div', 'krishi-agent-proposal-slot');
        slot.append(el('p', 'krishi-agent-muted small mb-0', strings.proposalLoading));
        fetch(`${root.dataset.proposalUrl}/${encodeURIComponent(id)}`, { credentials: 'same-origin', headers: { 'Accept': 'text/html' } })
            .then((res) => (res.ok ? res.text() : Promise.reject(new Error(String(res.status)))))
            .then((html) => { slot.innerHTML = html; scrollToEnd(); })
            .catch(() => { slot.replaceChildren(el('p', 'krishi-agent-muted small mb-0', strings.proposalFailed)); });
        return slot;
    }

    // ------------------------------------------------------------------ Busy state

    function setBusy(on) {
        busy = on;
        sendBtn.disabled = on;
        root.setAttribute('aria-busy', on ? 'true' : 'false');
        typing.classList.toggle('d-none', !on);
        clearInterval(progressTimer);
        if (on) {
            // Non-streaming: step through honest, generic stages while the server runs its tool loop.
            const stages = [strings.thinking, strings.checking, strings.writing];
            let i = 0;
            typingLabel.textContent = stages[0];
            status.textContent = stages[0];
            progressTimer = setInterval(() => {
                i = Math.min(i + 1, stages.length - 1);
                typingLabel.textContent = stages[i];
            }, 2200);
            scrollToEnd();
        } else {
            typingLabel.textContent = '';
            status.textContent = '';
        }
    }

    // ------------------------------------------------------------------ Actions

    async function send(text) {
        text = text.trim();
        if (!text || busy) return;
        if (text.length > maxLength) text = text.slice(0, maxLength);

        addUserMessage(text);
        input.value = '';
        autoGrow();
        setBusy(true);

        try {
            const res = await post(root.dataset.sendUrl, conversationId ? { Message: text, ConversationId: conversationId } : { Message: text });
            if (res.status === 429) {
                addAssistantMessage({ reply: strings.rateLimited }, 'warning');
                return;
            }
            if (res.status === 401 || res.status === 403 || res.redirected) {
                window.location.reload();
                return;
            }
            if (!res.ok) throw new Error(String(res.status));
            const data = await res.json();
            if (data.conversationId) conversationId = data.conversationId;
            if (data.title && subtitle) subtitle.textContent = data.title;
            const variant = data.status === 'unavailable' ? 'error' : (data.status === 'ok' ? '' : 'warning');
            addAssistantMessage(data, variant);
        } catch {
            addAssistantMessage({ reply: strings.unavailable, fallbackUrl: '/Equipment' }, 'error');
        } finally {
            setBusy(false);
            input.focus();
        }
    }

    async function loadHistory(id) {
        try {
            const url = id ? `${root.dataset.historyUrl}?conversationId=${encodeURIComponent(id)}` : root.dataset.historyUrl;
            const data = await getJson(url);
            log.replaceChildren();
            conversationId = data.conversationId || null;
            if (subtitle) subtitle.textContent = data.title || defaultSubtitle;
            for (const m of data.messages || []) {
                if (m.role === 'user') addUserMessage(m.content);
                else addAssistantMessage(m, '');
            }
            setEmpty(!(data.messages || []).length);
            loaded = true;
        } catch {
            setEmpty(true);
        }
    }

    async function newConversation() {
        if (busy) return;
        log.replaceChildren();
        setEmpty(true);
        conversationId = null;
        if (subtitle) subtitle.textContent = defaultSubtitle;
        input.focus();
        try {
            const res = await post(root.dataset.newUrl, {});
            if (res.ok) conversationId = (await res.json()).id || null;
        } catch {
            // The next message starts a conversation anyway.
        }
    }

    async function fillHistoryMenu() {
        historyMenu.replaceChildren();
        try {
            const items = await getJson(root.dataset.conversationsUrl);
            if (!items.length) {
                historyMenu.append(Object.assign(el('li'), { innerHTML: `<span class="dropdown-item-text small krishi-agent-muted">${escapeHtml(strings.noConversations)}</span>` }));
                return;
            }
            for (const c of items) {
                const li = el('li');
                const btn = el('button', 'dropdown-item text-truncate' + (c.id === conversationId ? ' active' : ''), c.title || strings.newConversation);
                btn.type = 'button';
                btn.addEventListener('click', () => loadHistory(c.id));
                li.append(btn);
                historyMenu.append(li);
            }
        } catch {
            historyMenu.append(Object.assign(el('li'), { innerHTML: `<span class="dropdown-item-text small krishi-agent-muted">${escapeHtml(strings.unavailable)}</span>` }));
        }
    }

    // ------------------------------------------------------------------ Composer

    function autoGrow() {
        input.style.height = 'auto';
        input.style.height = Math.min(input.scrollHeight, 160) + 'px';
        const left = maxLength - input.value.length;
        counter.textContent = left <= 200 ? strings.charsLeft.replace('{0}', left) : '';
    }

    form.addEventListener('submit', (e) => {
        e.preventDefault();
        send(input.value);
    });

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
            e.preventDefault();
            send(input.value);
        }
    });
    input.addEventListener('input', autoGrow);

    empty.addEventListener('click', (e) => {
        const chip = e.target.closest('[data-agent-prompt]');
        if (chip) send(chip.dataset.agentPrompt);
    });

    $('krishiAgentNew').addEventListener('click', newConversation);
    historyBtn.addEventListener('show.bs.dropdown', fillHistoryMenu);

    root.addEventListener('show.bs.offcanvas', () => { if (!loaded) loadHistory(); });
    root.addEventListener('shown.bs.offcanvas', () => input.focus());
})();
