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

            newConfirmBtn.addEventListener('click', function () {
                if (typeof options.onConfirm === 'function') {
                    options.onConfirm();
                }
                bsModal.hide();
            });
        }

        const bsModalInstance = bootstrap.Modal.getOrCreateInstance(modalEl);
        bsModalInstance.show();
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
