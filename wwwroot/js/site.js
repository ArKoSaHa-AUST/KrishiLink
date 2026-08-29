/**
 * KrishiLink Core Client-Side Utilities
 * Shared helper functions for modals, navigation, and user interactions.
 */

window.KrishiModal = {
    /**
     * Triggers the global confirmation modal (_Modal.cshtml) dynamically.
     * @param {Object} options Configuration options
     * @param {string} [options.title] Modal title text
     * @param {string} [options.body] Modal HTML or body content string
     * @param {string} [options.confirmText] Label for confirm button (default: "Confirm")
     * @param {string} [options.cancelText] Label for cancel button (default: "Cancel")
     * @param {string} [options.confirmClass] CSS class for confirm button (default: "btn-krishi-primary")
     * @param {string} [options.iconClass] Bootstrap icon class (default: "bi-question-circle-fill text-success")
     * @param {Function} [options.onConfirm] Callback executed when user clicks Confirm
     */
    confirm: function (options) {
        options = options || {};
        const modalId = options.modalId || 'krishiConfirmModal';
        const modalEl = document.getElementById(modalId);

        if (!modalEl) {
            console.warn(`[KrishiLink] Modal element #${modalId} not found in DOM.`);
            return;
        }

        const apply = function () {
            const titleEl = document.getElementById(`${modalId}Title`);
            const bodyEl = document.getElementById(`${modalId}Body`);
            const iconEl = document.getElementById(`${modalId}Icon`);
            const confirmBtn = document.getElementById(`${modalId}ConfirmBtn`);
            const cancelBtn = document.getElementById(`${modalId}CancelBtn`);

            if (titleEl) titleEl.textContent = options.title || 'Confirm Action';
            if (bodyEl) {
                if (options.body) {
                    bodyEl.innerHTML = typeof options.body === 'string' && options.body.startsWith('<')
                        ? options.body
                        : `<p class="text-secondary mb-0">${options.body}</p>`;
                } else {
                    bodyEl.innerHTML = '<p class="text-secondary mb-0">Are you sure you want to proceed with this action?</p>';
                }
            }
            if (cancelBtn) cancelBtn.textContent = options.cancelText || 'Cancel';

            if (iconEl) {
                iconEl.className = `bi ${options.iconClass || 'bi-question-circle-fill text-success'}`;
            }

            if (confirmBtn) {
                confirmBtn.textContent = options.confirmText || 'Confirm';
                confirmBtn.className = `btn rounded-pill px-4 fw-semibold ${options.confirmClass || 'btn-krishi-primary'}`;

                // Replace element with clone to clear prior event listeners
                const newConfirmBtn = confirmBtn.cloneNode(true);
                confirmBtn.parentNode.replaceChild(newConfirmBtn, confirmBtn);

                const bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);
                const hideModal = function () {
                    bsModal.hide();
                    // Bootstrap no-ops hide() while the show transition is running
                    // (e.g. throttled background tabs) — retry once it settles.
                    setTimeout(function () {
                        if (modalEl.classList.contains('show')) bsModal.hide();
                    }, 450);
                };

                newConfirmBtn.addEventListener('click', function () {
                    if (typeof options.onConfirm === 'function') {
                        options.onConfirm();
                    }
                    hideModal();
                });

                // Optional secondary action button (e.g. "Reject Instead")
                document.getElementById(`${modalId}SecondaryBtn`)?.remove();
                if (options.secondary) {
                    const secBtn = document.createElement('button');
                    secBtn.type = 'button';
                    secBtn.id = `${modalId}SecondaryBtn`;
                    secBtn.className = `btn rounded-pill px-4 fw-semibold ${options.secondary.className || 'btn-outline-secondary'}`;
                    secBtn.textContent = options.secondary.text || 'More';
                    secBtn.addEventListener('click', function () {
                        if (typeof options.secondary.onConfirm === 'function') options.secondary.onConfirm();
                        hideModal();
                    });
                    newConfirmBtn.parentNode.insertBefore(secBtn, newConfirmBtn);
                }
            }

            bootstrap.Modal.getOrCreateInstance(modalEl).show();
        };

        // Bootstrap swallows show() while a hide transition is running.
        // If the modal is open or still animating out, wait for it to fully
        // close before re-populating and showing it again.
        const isOpen = modalEl.classList.contains('show');
        const isClosing = !isOpen && getComputedStyle(modalEl).display !== 'none';
        if (isOpen || isClosing) {
            modalEl.addEventListener('hidden.bs.modal', apply, { once: true });
            if (isOpen) bootstrap.Modal.getOrCreateInstance(modalEl).hide();
        } else {
            apply();
        }
    }
};

/**
 * Global helper for quick modal call
 */
window.showConfirmModal = function (title, body, onConfirm, confirmText, confirmClass) {
    window.KrishiModal.confirm({
        title: title,
        body: body,
        onConfirm: onConfirm,
        confirmText: confirmText,
        confirmClass: confirmClass
    });
};

/**
 * Global stacking toast helper. Each call creates its own toast element,
 * so rapid successive events never overwrite each other.
 * @param {string} message Toast message text
 * @param {Object} [options]
 * @param {string} [options.iconClass] Bootstrap icon classes (default: success check)
 * @param {number} [options.delay] Auto-hide delay in ms (default: 4000)
 * @param {string} [options.actionText] Optional action button label (e.g. "Undo")
 * @param {Function} [options.onAction] Called when the action button is clicked
 * @param {Function} [options.onClosed] Called when the toast closes WITHOUT the action being clicked
 */
window.KrishiToast = {
    show: function (message, options) {
        options = options || {};
        let container = document.getElementById('krishiToastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'krishiToastContainer';
            container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            container.style.zIndex = '1080';
            container.setAttribute('aria-live', 'polite');
            document.body.appendChild(container);
        }

        const toastEl = document.createElement('div');
        toastEl.className = 'toast border-0 shadow rounded-3';
        toastEl.setAttribute('role', 'alert');
        toastEl.setAttribute('aria-live', 'assertive');
        toastEl.setAttribute('aria-atomic', 'true');
        const actionHtml = options.actionText
            ? `<button type="button" class="btn btn-sm btn-outline-secondary rounded-pill px-3 ms-auto flex-shrink-0 toast-action-btn">${options.actionText}</button>`
            : '';
        toastEl.innerHTML = `<div class="toast-body d-flex align-items-center gap-2 fw-semibold">
                <i class="bi ${options.iconClass || 'bi-check-circle-fill text-success'} fs-5"></i>
                <span>${message}</span>${actionHtml}
            </div>`;
        container.appendChild(toastEl);

        const bsToast = new bootstrap.Toast(toastEl, { delay: options.delay || 4000 });
        let actionClicked = false;

        const actionBtn = toastEl.querySelector('.toast-action-btn');
        if (actionBtn) {
            actionBtn.addEventListener('click', function () {
                actionClicked = true;
                if (typeof options.onAction === 'function') options.onAction();
                bsToast.hide();
            });
        }

        toastEl.addEventListener('hidden.bs.toast', function () {
            if (!actionClicked && typeof options.onClosed === 'function') options.onClosed();
            toastEl.remove();
        });

        bsToast.show();
        return bsToast;
    }
};

/**
 * Global animated number counter (simple number transition via rAF).
 */
window.KrishiCount = {
    animate: function (el, to, duration) {
        if (!el) return;
        // rAF is paused in hidden tabs — snap to the final value instead
        if (document.hidden) { el.textContent = to; return; }
        const from = Number(el.textContent) || 0;
        const start = performance.now();
        duration = duration || 400;
        function tick(now) {
            const p = Math.min((now - start) / duration, 1);
            el.textContent = Math.round(from + (to - from) * p);
            if (p < 1) requestAnimationFrame(tick);
        }
        requestAnimationFrame(tick);
    }
};

/**
 * Shared rental/booking request decision helpers.
 * Used by the owner dashboards and the full request list pages.
 * Pages must contain a `#antiForgeryForm` with the anti-forgery token.
 */
window.KrishiRequests = {
    /** POST a decision (accept/reject/undo) as form data; resolves the JSON body or throws. */
    post: async function (url, payload) {
        const token = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]')?.value || '';
        const body = new URLSearchParams(Object.assign({}, payload, { __RequestVerificationToken: token }));
        const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: body.toString()
        });
        const data = await res.json();
        if (!data.success) throw new Error(data.message || 'Request failed');
        return data;
    },

    /** Accept confirmation modal: "Accept this rental request from [Farmer]?" */
    confirmAccept: function (farmerName, onConfirm, opts) {
        opts = opts || {};
        KrishiModal.confirm({
            title: opts.title || 'Accept Rental Request',
            body: `Accept this ${opts.noun || 'rental request'} from <strong>${farmerName}</strong>? They will be notified immediately.`,
            confirmText: 'Yes, Accept',
            onConfirm: onConfirm
        });
    },

    /** Reject modal with a short, optional reason field. Calls onConfirm(reason). */
    promptReject: function (farmerName, onConfirm, opts) {
        opts = opts || {};
        KrishiModal.confirm({
            title: opts.title || 'Reject Rental Request',
            body: `<p class="text-secondary small mb-2">Reject this request from <strong>${farmerName}</strong>? Optionally tell them why — it helps them adjust:</p>
                   <textarea id="krishiRejectReason" class="form-control form-control-sm" rows="2"
                             placeholder="${opts.placeholder || 'e.g. Equipment is already booked for those dates (optional)'}"></textarea>`,
            confirmText: 'Reject Request',
            confirmClass: 'btn-danger',
            onConfirm: function () {
                onConfirm(document.getElementById('krishiRejectReason')?.value.trim() || '');
            }
        });
    },

    /**
     * Optimistic accept/reject with an Undo toast: the row leaves immediately
     * and the POST fires only if the toast closes without Undo.
     * opts: { url, id, decision, reason, row, acceptedMsg, rejectedMsg,
     *         onApply(), onRowHidden(), onRevert(), undoDelay }
     */
    optimisticDecision: function (opts) {
        const row = opts.row;
        const accepted = opts.decision === 'accept';
        let undone = false;

        row.querySelectorAll('button').forEach(b => b.disabled = true);
        row.classList.add('row-leaving');
        if (typeof opts.onApply === 'function') opts.onApply();
        setTimeout(function () {
            if (!undone) {
                row.classList.add('d-none');
                if (typeof opts.onRowHidden === 'function') opts.onRowHidden();
            }
        }, 350);

        const revert = function () {
            row.classList.remove('row-leaving', 'd-none');
            row.querySelectorAll('button').forEach(b => b.disabled = false);
            if (typeof opts.onRevert === 'function') opts.onRevert();
        };

        KrishiToast.show(accepted ? (opts.acceptedMsg || 'Request accepted ✓') : (opts.rejectedMsg || 'Request rejected'), {
            iconClass: accepted ? 'bi-check-circle-fill text-success' : 'bi-x-circle-fill text-danger',
            actionText: 'Undo',
            delay: opts.undoDelay || 5000,
            onAction: function () { undone = true; revert(); },
            onClosed: async function () {
                try {
                    await KrishiRequests.post(opts.url, { id: opts.id, decision: opts.decision, reason: opts.reason || '' });
                    row.remove();
                } catch {
                    revert();
                    KrishiToast.show('Could not save the decision. Please try again.', { iconClass: 'bi-exclamation-triangle-fill text-danger' });
                }
            }
        });
    },

    /** Poll a {count} JSON endpoint and call onIncrease(delta) when new requests arrive. */
    watchPendingCount: function (url, initialCount, onIncrease, intervalMs) {
        let last = initialCount;
        setInterval(async function () {
            try {
                const res = await fetch(url);
                const data = await res.json();
                if (data.count > last && typeof onIncrease === 'function') onIncrease(data.count - last);
                last = data.count;
            } catch { /* offline or logged out — silently skip this cycle */ }
        }, intervalMs || 20000);
    }
};

/**
 * Shared engine for the tabbed owner request pages (Equipment Rental Requests,
 * Godown Booking Requests). Owns: tab counts, per-tab empty states, search +
 * optional entity filter (with ?q= URL sync), hash deep-linking, keyboard row
 * expansion, decision submission (loading state → POST → animated row move with
 * an undo-race guard → Undo toast), the Accepted → Completed lifecycle, and
 * optional new-request polling.
 *
 * Expects the page markup conventions: #requestTabs, #pane-X/#list-X/#count-X/#empty-X,
 * #requestSearch, .request-row rows with data-request-id/-farmer-name/-search,
 * and a #antiForgeryForm. Installs window.applyDecision / promptReject / markCompleted
 * for the shared row partials.
 *
 * cfg: {
 *   respondUrl, pendingCountUrl?, initialPendingCount?, undoDelay?,
 *   filterSelectId?, sortSelectId?,
 *   messages: { acceptTitle, acceptNoun, rejectTitle, rejectPlaceholder,
 *               acceptedMsg, rejectedMsg, completedMsg, completeTitle, newRequestMsg },
 *   acceptGuard?(row, api {confirm, direct, reject(reason)}),
 *   onApplied?(row, decision), onReverted?(row, decision), afterListChange?()
 * }
 */
window.KrishiRequestsPage = {
    init: function (cfg) {
        const TABS = ['Pending', 'Accepted', 'Rejected', 'Completed'];
        const BADGE = {
            Pending: 'krishi-badge-pending d-none d-md-inline-flex',
            Accepted: 'krishi-badge-available',
            Rejected: 'krishi-badge-unavailable',
            Completed: 'krishi-badge-completed'
        };
        const MOVES = {
            accept: { from: 'Pending', to: 'Accepted' },
            reject: { from: 'Pending', to: 'Rejected' },
            complete: { from: 'Accepted', to: 'Completed' }
        };
        const msgs = cfg.messages || {};
        const searchInput = document.getElementById('requestSearch');
        const filterSelect = cfg.filterSelectId ? document.getElementById(cfg.filterSelectId) : null;

        function tabCount(tab, delta) {
            const el = document.getElementById('count-' + tab);
            if (el) KrishiCount.animate(el, Number(el.textContent) + delta);
        }

        function setStatus(row, status) {
            const badge = row.querySelector('.request-status-badge');
            if (badge) {
                badge.textContent = status;
                badge.className = 'krishi-badge request-status-badge ' + BADGE[status];
            }
        }

        function isFiltering() {
            return (searchInput?.value.trim().length > 0) || (filterSelect && filterSelect.value !== '');
        }

        function refreshEmptyStates() {
            TABS.forEach(tab => {
                const empty = document.getElementById('empty-' + tab);
                if (!empty) return;
                const total = document.querySelectorAll('#list-' + tab + ' .request-row').length;
                const visible = document.querySelectorAll('#list-' + tab + ' .request-row:not(.d-none)').length;
                empty.classList.toggle('d-none', visible > 0);
                empty.querySelector('span').textContent =
                    (total > 0 && isFiltering()) ? 'No requests match your search' : empty.dataset.defaultText;
            });
        }

        function applyFilters() {
            const q = (searchInput?.value || '').trim().toLowerCase();
            const entity = filterSelect ? filterSelect.value : '';
            document.querySelectorAll('.request-row').forEach(r => {
                const matchQ = !q || (r.dataset.search || '').includes(q);
                const matchE = !entity || r.dataset.godownId === entity;
                r.classList.toggle('d-none', !(matchQ && matchE));
            });
            // Keep ?q= shareable/refresh-safe alongside the tab hash
            const url = new URL(location.href);
            if (q) url.searchParams.set('q', q); else url.searchParams.delete('q');
            history.replaceState(null, '', url);
            refreshEmptyStates();
        }

        function sortPending() {
            if (!cfg.sortSelectId) return;
            const mode = document.getElementById(cfg.sortSelectId)?.value || 'newest';
            const list = document.getElementById('list-Pending');
            Array.from(list.querySelectorAll('.request-row'))
                .sort((a, b) => mode === 'capacity'
                    ? Number(b.dataset.capacity || 0) - Number(a.dataset.capacity || 0)
                    : Number(a.dataset.order || 0) - Number(b.dataset.order || 0))
                .forEach(r => list.appendChild(r));
        }

        async function submit(id, decision, btn, reason) {
            const row = btn.closest('.request-row');
            if (!row) return;
            const move = MOVES[decision];
            const actions = row.querySelector('.request-actions');
            const originalBtnHtml = btn.innerHTML;
            const originalActionsHtml = actions.innerHTML;

            actions.querySelectorAll('button').forEach(b => b.disabled = true);
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Saving…';

            try {
                await KrishiRequests.post(cfg.respondUrl, { id, decision, reason: reason || '' });
                btn.innerHTML = originalBtnHtml;
                actions.querySelectorAll('button').forEach(b => b.disabled = false);
                if (cfg.onApplied) cfg.onApplied(row, decision);

                const state = { undone: false, moved: false };
                row.classList.add('row-leaving');
                setTimeout(() => {
                    row.classList.remove('row-leaving');
                    if (state.undone) return; // undone before the move — leave the row in place
                    if (decision === 'accept') {
                        row.querySelector('.fit-indicator')?.classList.add('d-none');
                        actions.innerHTML = `<button type="button" class="btn btn-krishi-outline btn-sm rounded-pill px-3 fw-semibold btn-complete"
                                                     onclick="markCompleted(${id}, this)"><i class="bi bi-check2-all"></i> Mark Completed</button>`;
                    } else if (decision === 'reject') {
                        row.querySelector('.fit-indicator')?.classList.add('d-none');
                        actions.classList.add('d-none');
                        if (reason) {
                            const block = row.querySelector('.reject-reason-block');
                            if (block) {
                                block.querySelector('.reject-reason-text').textContent = reason;
                                block.classList.remove('d-none');
                            }
                        }
                    } else { // complete
                        actions.classList.add('d-none');
                    }
                    setStatus(row, move.to);
                    document.getElementById('list-' + move.to).prepend(row);
                    tabCount(move.from, -1);
                    tabCount(move.to, +1);
                    refreshEmptyStates();
                    if (cfg.afterListChange) cfg.afterListChange();
                    state.moved = true;
                }, 350);

                const msg = decision === 'accept' ? msgs.acceptedMsg : decision === 'reject' ? msgs.rejectedMsg : msgs.completedMsg;
                KrishiToast.show(msg || 'Saved ✓', {
                    iconClass: decision === 'reject' ? 'bi-x-circle-fill text-danger' : 'bi-check-circle-fill text-success',
                    actionText: 'Undo',
                    delay: cfg.undoDelay || 5000,
                    onAction: () => {
                        state.undone = true;
                        if (cfg.onReverted) cfg.onReverted(row, decision);
                        if (state.moved) {
                            actions.classList.remove('d-none');
                            actions.innerHTML = originalActionsHtml;
                            row.querySelector('.fit-indicator')?.classList.remove('d-none');
                            if (decision === 'reject') row.querySelector('.reject-reason-block')?.classList.add('d-none');
                            setStatus(row, move.from);
                            document.getElementById('list-' + move.from).prepend(row);
                            tabCount(move.to, -1);
                            tabCount(move.from, +1);
                            refreshEmptyStates();
                            if (cfg.afterListChange) cfg.afterListChange();
                        } else {
                            row.classList.remove('row-leaving');
                        }
                        KrishiRequests.post(cfg.respondUrl, { id, decision: 'undo' }).catch(() => {});
                    }
                });
            } catch {
                btn.innerHTML = originalBtnHtml;
                actions.querySelectorAll('button').forEach(b => b.disabled = false);
                KrishiToast.show('Could not save the decision. Please try again.', { iconClass: 'bi-exclamation-triangle-fill text-danger' });
            }
        }

        // Handlers referenced by the shared row partials
        window.applyDecision = function (id, decision, btn) {
            const row = btn.closest('.request-row');
            const farmer = row.dataset.farmerName;
            const direct = () => submit(id, 'accept', btn);
            const confirmThen = () => KrishiRequests.confirmAccept(farmer, direct, { title: msgs.acceptTitle, noun: msgs.acceptNoun });
            if (cfg.acceptGuard) {
                cfg.acceptGuard(row, { confirm: confirmThen, direct, reject: reason => submit(id, 'reject', btn, reason) });
            } else {
                confirmThen();
            }
        };

        window.promptReject = function (id, btn) {
            const farmer = btn.closest('.request-row').dataset.farmerName;
            KrishiRequests.promptReject(farmer,
                reason => submit(id, 'reject', btn, reason),
                { title: msgs.rejectTitle, placeholder: msgs.rejectPlaceholder });
        };

        window.markCompleted = function (id, btn) {
            const farmer = btn.closest('.request-row').dataset.farmerName;
            KrishiModal.confirm({
                title: msgs.completeTitle || 'Mark as Completed',
                body: `Mark this booking from <strong>${farmer}</strong> as completed? This finishes the request lifecycle.`,
                confirmText: 'Mark Completed',
                onConfirm: () => submit(id, 'complete', btn)
            });
        };

        // Deep-linking: #accepted etc. selects the tab; tab changes update the hash (keeping ?q=)
        const hashMap = { pending: 'Pending', accepted: 'Accepted', rejected: 'Rejected', completed: 'Completed' };
        const hash = location.hash.replace('#', '').toLowerCase();
        if (hashMap[hash]) bootstrap.Tab.getOrCreateInstance(document.getElementById('tab-' + hashMap[hash])).show();
        document.querySelectorAll('#requestTabs [data-bs-toggle="pill"]').forEach(t =>
            t.addEventListener('shown.bs.tab', e => {
                const url = new URL(location.href);
                url.hash = e.target.id.replace('tab-', '').toLowerCase();
                history.replaceState(null, '', url);
            }));

        // Keyboard accessibility: Enter/Space toggles row expansion
        document.getElementById('requestTabPanes')?.addEventListener('keydown', function (e) {
            if ((e.key === 'Enter' || e.key === ' ') && e.target.classList.contains('request-row-header')) {
                e.preventDefault();
                e.target.click();
            }
        });

        // Search (+ optional entity filter), prefilled from ?q=
        if (searchInput) {
            const q = new URL(location.href).searchParams.get('q');
            if (q) searchInput.value = q;
            searchInput.addEventListener('input', applyFilters);
        }
        filterSelect?.addEventListener('change', applyFilters);

        // Optional pending sort; remember the initial (newest-first) order
        if (cfg.sortSelectId) {
            document.querySelectorAll('#list-Pending .request-row').forEach((r, i) => r.dataset.order = i);
            document.getElementById(cfg.sortSelectId)?.addEventListener('change', sortPending);
        }

        // Screen readers hear capacity/fit changes in the pending list
        document.getElementById('list-Pending')?.setAttribute('aria-live', 'polite');

        // Notify when new requests arrive
        if (cfg.pendingCountUrl) {
            KrishiRequests.watchPendingCount(cfg.pendingCountUrl, cfg.initialPendingCount || 0, delta => {
                tabCount('Pending', delta);
                KrishiToast.show(`${delta} ${msgs.newRequestMsg || 'new request'}${delta > 1 ? 's' : ''} received`, { iconClass: 'bi-bell-fill text-success' });
            });
        }

        applyFilters();
        if (cfg.afterListChange) cfg.afterListChange();

        return { applyFilters, refreshEmptyStates };
    }
};

/**
 * Mobile Navbar Collapse & Interactive Enhancements
 */
document.addEventListener('DOMContentLoaded', function () {
    // Auto-close mobile navbar when clicking outside
    const navbarToggler = document.querySelector('.navbar-toggler');
    const navbarCollapse = document.querySelector('.navbar-collapse');
    const offcanvasEl = document.querySelector('.offcanvas');

    if (navbarToggler && navbarCollapse && !offcanvasEl) {
        document.addEventListener('click', function (e) {
            const isClickInside = navbarCollapse.contains(e.target) || navbarToggler.contains(e.target);
            if (!isClickInside && navbarCollapse.classList.contains('show')) {
                const bsCollapse = bootstrap.Collapse.getInstance(navbarCollapse);
                if (bsCollapse) {
                    bsCollapse.hide();
                }
            }
        });
    }

    // Auto-close offcanvas on mobile link clicks if present
    const offcanvasNavLinks = document.querySelectorAll('.offcanvas .nav-link:not(.dropdown-toggle)');
    offcanvasNavLinks.forEach(function (link) {
        link.addEventListener('click', function () {
            const openOffcanvas = document.querySelector('.offcanvas.show');
            if (openOffcanvas) {
                const bsOffcanvas = bootstrap.Offcanvas.getInstance(openOffcanvas);
                if (bsOffcanvas) {
                    bsOffcanvas.hide();
                }
            }
        });
    });
});
