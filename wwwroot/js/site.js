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
