/**
 * KrishiLink Realtime Client & Webhook Event Stream
 * Connects all pages via Server-Sent Events (SSE) and processes live Webhook & System Updates
 */
(function () {
    'use strict';

    const KrishiRealtime = {
        eventSource: null,
        reconnectAttempts: 0,
        maxReconnectDelay: 30000,
        baseReconnectDelay: 2000,
        isConnecting: false,

        init: function () {
            if (!window.EventSource) {
                console.warn('[KrishiRealtime] EventSource is not supported by this browser.');
                return;
            }

            this.connect();

            // Reconnect when user comes back online or tab becomes visible
            window.addEventListener('online', () => {
                console.log('[KrishiRealtime] Browser online. Reconnecting stream...');
                this.reconnect(true);
            });

            document.addEventListener('visibilitychange', () => {
                if (!document.hidden && (!this.eventSource || this.eventSource.readyState === EventSource.CLOSED)) {
                    this.reconnect(false);
                }
            });
        },

        connect: function () {
            if (this.isConnecting) return;
            this.isConnecting = true;

            try {
                if (this.eventSource) {
                    this.eventSource.close();
                }

                this.eventSource = new EventSource('/api/realtime/stream');

                this.eventSource.onopen = () => {
                    this.isConnecting = false;
                    this.reconnectAttempts = 0;
                    console.log('[KrishiRealtime] Connected to realtime event stream.');
                    this.updateLiveIndicator(true);
                };

                this.eventSource.onerror = (err) => {
                    this.isConnecting = false;
                    this.updateLiveIndicator(false);
                    console.warn('[KrishiRealtime] Stream connection dropped. Retrying...', err);
                    this.eventSource.close();
                    this.scheduleReconnect();
                };

                // Listen for standard event types
                this.bindEventType('connected');
                this.bindEventType('notification');
                this.bindEventType('booking');
                this.bindEventType('revenue');
                this.bindEventType('intake');
                this.bindEventType('webhook');
                this.bindEventType('system');
                this.bindEventType('weather');
                this.bindEventType('update');

            } catch (err) {
                this.isConnecting = false;
                console.error('[KrishiRealtime] Failed to initialize stream', err);
                this.scheduleReconnect();
            }
        },

        bindEventType: function (eventName) {
            this.eventSource.addEventListener(eventName, (e) => {
                try {
                    const data = e.data ? JSON.parse(e.data) : {};
                    this.handleEvent(eventName, data);
                } catch (parseErr) {
                    console.error('[KrishiRealtime] Error parsing event data for ' + eventName, parseErr);
                }
            });
        },

        handleEvent: function (eventName, data) {
            // Dispatch general window event
            window.dispatchEvent(new CustomEvent('krishilink:realtime', {
                detail: { type: eventName, data: data }
            }));

            // Dispatch specific type event
            window.dispatchEvent(new CustomEvent('krishilink:' + eventName, {
                detail: data
            }));

            if (eventName === 'connected') {
                return;
            }

            // Handle UI updates across all pages
            this.onRealtimeMessage(eventName, data);
        },

        onRealtimeMessage: function (type, data) {
            const title = data.Title || data.title || 'KrishiLink Update';
            const message = data.Message || data.message || '';
            const linkUrl = data.LinkUrl || data.linkUrl || null;

            // 1. Live Notification Bell Badge update
            if (window.KrishiNotifications && typeof window.KrishiNotifications.fetchUnreadCount === 'function') {
                window.KrishiNotifications.fetchUnreadCount();
            } else {
                const desktopBadge = document.getElementById('notificationBadge');
                const mobileBadge = document.getElementById('mobileNotificationBadge');
                if (desktopBadge) {
                    let current = parseInt(desktopBadge.textContent || '0', 10);
                    if (isNaN(current)) current = 0;
                    current++;
                    desktopBadge.textContent = current > 99 ? '99+' : current;
                    desktopBadge.style.display = 'inline-block';
                    desktopBadge.classList.add('animate-pulse');
                }
                if (mobileBadge) {
                    let current = parseInt(mobileBadge.textContent || '0', 10);
                    if (isNaN(current)) current = 0;
                    current++;
                    mobileBadge.textContent = current > 99 ? '99+' : current;
                    mobileBadge.style.display = 'inline-block';
                }
            }

            // 2. Display Toast Popup on active screen
            if (window.KrishiToast && (title || message)) {
                const iconClass = type === 'booking' ? 'bi-calendar-check text-success' :
                                  type === 'revenue' ? 'bi-cash-coin text-success' :
                                  type === 'webhook' ? 'bi-broadcast text-primary' :
                                  'bi-bell-fill text-success';

                let toastBody = message;
                if (linkUrl) {
                    toastBody += `<div class="mt-1"><a href="${linkUrl}" class="small fw-bold text-decoration-none" style="color: var(--krishi-primary);">View Details &rarr;</a></div>`;
                }

                window.KrishiToast.show(title + (message ? ': ' + toastBody : ''), {
                    iconClass: iconClass,
                    duration: 6000
                });
            }
        },

        scheduleReconnect: function () {
            this.reconnectAttempts++;
            const delay = Math.min(this.baseReconnectDelay * Math.pow(1.5, this.reconnectAttempts), this.maxReconnectDelay);
            console.log(`[KrishiRealtime] Reconnecting in ${Math.round(delay / 1000)}s (attempt ${this.reconnectAttempts})...`);
            setTimeout(() => {
                this.connect();
            }, delay);
        },

        reconnect: function (immediate) {
            if (this.eventSource) {
                this.eventSource.close();
                this.eventSource = null;
            }
            this.isConnecting = false;
            if (immediate) {
                this.reconnectAttempts = 0;
                this.connect();
            } else {
                this.scheduleReconnect();
            }
        },

        updateLiveIndicator: function (isLive) {
            const indicators = document.querySelectorAll('.krishi-realtime-indicator');
            indicators.forEach(el => {
                if (isLive) {
                    el.classList.remove('bg-secondary', 'bg-danger');
                    el.classList.add('bg-success');
                    el.setAttribute('title', 'Realtime Connected');
                } else {
                    el.classList.remove('bg-success');
                    el.classList.add('bg-secondary');
                    el.setAttribute('title', 'Reconnecting...');
                }
            });
        }
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => KrishiRealtime.init());
    } else {
        KrishiRealtime.init();
    }

    // Expose globally
    window.KrishiRealtime = KrishiRealtime;
})();
