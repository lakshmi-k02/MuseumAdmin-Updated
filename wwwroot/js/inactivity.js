(function () {
    const INACTIVITY_TIMEOUT_MS = 15 * 60 * 1000; // 15 minutes
    const WARNING_DURATION_SECONDS = 30;           // 30 seconds countdown
    const CHECK_INTERVAL_MS = 1000;                // Check every second

    window.inactivityTracking = {
        timeoutMs: INACTIVITY_TIMEOUT_MS,
        warningSeconds: WARNING_DURATION_SECONDS,
        lastActivity: Date.now(),
        lastMouseX: -1,
        lastMouseY: -1,
        isWarningActive: false,
        warningStart: 0,
        originalTitle: '',
        checkInterval: null,
        blinkInterval: null,
        soundInterval: null,
        audioCtx: null,
        initialized: false,
        lastKeepAlivePing: Date.now(),

        init: function (customTimeoutMs, customWarningSecs) {
            if (this.initialized) return;
            this.initialized = true;

            if (customTimeoutMs) this.timeoutMs = customTimeoutMs;
            if (customWarningSecs) this.warningSeconds = customWarningSecs;

            // Do not track on login page
            const path = (window.location?.pathname || '').toLowerCase();
            if (path.includes('/login') || path.includes('/account/login')) {
                return;
            }

            this.originalTitle = document.title || 'Memby Admin';
            this.lastActivity = Date.now();
            this.syncLastActivity();

            console.log(`[INACTIVITY] Tracker initialized. Idle timeout: ${this.timeoutMs / 1000}s, Warning countdown: ${this.warningSeconds}s.`);

            // User input handler: Strictly filters for genuine physical human interaction.
            // Automated DOM updates / background SignalR polling will NOT trigger these.
            const recordUserActivity = (e) => {
                // Must be an authentic user event (ignore synthetic events dispatched by scripts)
                if (e && e.isTrusted === false) return;

                // If warning modal is currently displayed, do NOT silently dismiss via ambient mouse movement.
                // The user must explicitly click "Keep Session" or "Close".
                if (this.isWarningActive) return;

                // For mousemove, require actual pointer movement (avoid optical sensor micro-jitter)
                if (e && (e.type === 'mousemove' || e.type === 'pointermove')) {
                    if (this.lastMouseX >= 0 && this.lastMouseY >= 0) {
                        const dist = Math.hypot(e.clientX - this.lastMouseX, e.clientY - this.lastMouseY);
                        if (dist < 6) return;
                    }
                    this.lastMouseX = e.clientX;
                    this.lastMouseY = e.clientY;
                }

                this.lastActivity = Date.now();
                this.syncLastActivity();
            };

            // Human input events:
            window.addEventListener('pointerdown', recordUserActivity, { passive: true, capture: true });
            window.addEventListener('pointermove', recordUserActivity, { passive: true, capture: true });
            window.addEventListener('keydown', recordUserActivity, { passive: true, capture: true });
            window.addEventListener('wheel', recordUserActivity, { passive: true, capture: true });
            window.addEventListener('touchstart', recordUserActivity, { passive: true, capture: true });
            window.addEventListener('touchmove', recordUserActivity, { passive: true, capture: true });

            // Instant check as soon as user switches back to this tab or window
            document.addEventListener('visibilitychange', () => {
                if (!document.hidden) this.checkInactivity();
            });
            window.addEventListener('focus', () => this.checkInactivity());

            // Start heartbeat interval
            if (this.checkInterval) clearInterval(this.checkInterval);
            this.checkInterval = setInterval(() => this.checkInactivity(), CHECK_INTERVAL_MS);
        },

        syncLastActivity: function () {
            try {
                sessionStorage.setItem('memby_last_activity', this.lastActivity.toString());
            } catch (e) { }
        },

        getLastActivity: function () {
            try {
                const stored = sessionStorage.getItem('memby_last_activity');
                if (stored) {
                    const parsed = parseInt(stored, 10);
                    if (!isNaN(parsed) && parsed > this.lastActivity) {
                        this.lastActivity = parsed;
                    }
                }
            } catch (e) { }
            return this.lastActivity;
        },

        checkInactivity: function () {
            const path = (window.location?.pathname || '').toLowerCase();
            if (path.includes('/login') || path.includes('/account/login')) {
                return;
            }

            const now = Date.now();
            const lastActive = this.getLastActivity();
            const idleMs = now - lastActive;
            const totalThresholdMs = this.timeoutMs + (this.warningSeconds * 1000);

            // If machine was asleep or tab backgrounded for > (timeout + warning) duration, immediate logout
            if (idleMs >= totalThresholdMs) {
                console.log(`[INACTIVITY] Idle threshold exceeded (${Math.floor(idleMs / 1000)}s). Forcing logout.`);
                this.forceLogout();
                return;
            }

            // If idle >= timeout (e.g. 1 minute), trigger or update warning modal
            if (idleMs >= this.timeoutMs) {
                const elapsedInWarning = idleMs - this.timeoutMs;
                const remainingSecs = Math.max(0, Math.ceil((this.warningSeconds * 1000 - elapsedInWarning) / 1000));
                
                if (!this.isWarningActive) {
                    this.showWarning(remainingSecs);
                } else {
                    this.updateWarningCountdown(remainingSecs);
                }
            } else {
                if (this.isWarningActive) {
                    this.hideWarning();
                }

                // If user is actively working in the app, periodically refresh server-side sliding cookie (every 4 mins).
                // Blazor Server runs over WebSockets, so periodic keep-alive maintains active HTTP cookie state while working.
                if (now - this.lastKeepAlivePing > 4 * 60 * 1000) {
                    this.lastKeepAlivePing = now;
                    fetch('/account/keep-alive', { method: 'GET', credentials: 'include' })
                        .then(res => {
                            if (res.status === 401 || res.redirected) {
                                this.forceLogout();
                            }
                        })
                        .catch(err => console.warn('[INACTIVITY] Periodic keep-alive warning:', err));
                }
            }
        },

        showWarning: function (remainingSecs) {
            this.isWarningActive = true;
            this.warningStart = Date.now();

            console.log(`[INACTIVITY] ⚠️ Idle limit reached! Warning modal displayed with ${remainingSecs}s countdown.`);

            const currentTitle = document.title || 'Memby Admin';
            this.originalTitle = currentTitle.replace(/^⚠️.*?\|\s*/, '').replace(/^⚠️\s*/, '');

            this.injectModalHtml(remainingSecs);
            this.startBlinking(remainingSecs);
            this.startSoundLoop();
        },

        updateWarningCountdown: function (remainingSecs) {
            const countEl = document.getElementById('memby-timeout-count');
            const progressEl = document.getElementById('memby-timeout-progress');
            const iconWrapper = document.getElementById('memby-timeout-icon-wrapper');
            const badgeEl = document.getElementById('memby-timeout-badge');

            if (countEl) countEl.textContent = remainingSecs;
            if (progressEl) {
                const percent = Math.max(0, Math.min(100, (remainingSecs / this.warningSeconds) * 100));
                progressEl.style.width = percent + '%';
            }

            if (remainingSecs <= 10) {
                if (iconWrapper) iconWrapper.classList.add('urgent');
                if (badgeEl) badgeEl.classList.add('urgent');
                if (progressEl) progressEl.classList.add('urgent');
            } else {
                if (iconWrapper) iconWrapper.classList.remove('urgent');
                if (badgeEl) badgeEl.classList.remove('urgent');
                if (progressEl) progressEl.classList.remove('urgent');
            }

            if (remainingSecs <= 0) {
                console.log(`[INACTIVITY] Countdown finished. Forcing logout.`);
                this.forceLogout();
            }
        },

        hideWarning: function () {
            this.isWarningActive = false;
            this.stopBlinking();
            this.stopSoundLoop();
            const modalEl = document.getElementById('memby-session-timeout-modal');
            if (modalEl) {
                modalEl.remove();
            }
        },

        startBlinking: function (initialSeconds) {
            if (this.blinkInterval) clearInterval(this.blinkInterval);
            let toggle = false;
            let secs = initialSeconds || this.warningSeconds;

            const updateBlink = () => {
                toggle = !toggle;
                if (toggle) {
                    document.title = `⚠️ (${secs}s) Inactivity Warning!`;
                } else {
                    document.title = `⏳ Action Required | ${this.originalTitle}`;
                }
            };

            updateBlink();
            this.blinkInterval = setInterval(() => {
                if (secs > 1) secs--;
                updateBlink();
            }, 1000);
        },

        stopBlinking: function () {
            if (this.blinkInterval) {
                clearInterval(this.blinkInterval);
                this.blinkInterval = null;
            }
            if (this.originalTitle) {
                document.title = this.originalTitle;
            }
        },

        startSoundLoop: function () {
            this.stopSoundLoop();
            // Play immediately on warning appearance
            this.playNoticeableBeep();
            // Repeat every 5 seconds during the countdown
            this.soundInterval = setInterval(() => {
                if (this.isWarningActive) {
                    this.playNoticeableBeep();
                } else {
                    this.stopSoundLoop();
                }
            }, 5000);
        },

        stopSoundLoop: function () {
            if (this.soundInterval) {
                clearInterval(this.soundInterval);
                this.soundInterval = null;
            }
        },

        playNoticeableBeep: function () {
            try {
                const AudioContext = window.AudioContext || window.webkitAudioContext;
                if (!AudioContext) return;

                if (!this.audioCtx) {
                    this.audioCtx = new AudioContext();
                }
                if (this.audioCtx.state === 'suspended') {
                    this.audioCtx.resume();
                }

                const ctx = this.audioCtx;
                const now = ctx.currentTime;

                // Pulse 1: Attention Tone (800 Hz)
                const osc1 = ctx.createOscillator();
                const gain1 = ctx.createGain();
                osc1.type = 'triangle';
                osc1.frequency.setValueAtTime(800, now);
                gain1.gain.setValueAtTime(0.35, now);
                gain1.gain.exponentialRampToValueAtTime(0.001, now + 0.14);
                osc1.connect(gain1);
                gain1.connect(ctx.destination);
                osc1.start(now);
                osc1.stop(now + 0.14);

                // Pulse 2: High Alert Chime (1050 Hz)
                const osc2 = ctx.createOscillator();
                const gain2 = ctx.createGain();
                osc2.type = 'sine';
                osc2.frequency.setValueAtTime(1050, now + 0.16);
                gain2.gain.setValueAtTime(0.40, now + 0.16);
                gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.38);
                osc2.connect(gain2);
                gain2.connect(ctx.destination);
                osc2.start(now + 0.16);
                osc2.stop(now + 0.38);
            } catch (e) {
                console.warn('[INACTIVITY] Audio playback warning:', e);
            }
        },

        keepSession: function () {
            console.log(`[INACTIVITY] User clicked 'Keep Session'. Resetting timer and extending session...`);
            this.hideWarning();
            this.lastActivity = Date.now();
            this.lastKeepAlivePing = Date.now();
            this.syncLastActivity();

            // Ping server keep-alive to refresh ASP.NET Core sliding authentication cookie
            fetch('/account/keep-alive', { method: 'GET', credentials: 'include' })
                .then(response => {
                    console.log(`[INACTIVITY] Keep-alive HTTP response: Status ${response.status}, Redirected: ${response.redirected}`);
                    this.lastActivity = Date.now();
                    this.syncLastActivity();
                    if (response.ok && !response.redirected) {
                        console.log(`[INACTIVITY] ✅ Session successfully extended on server! Idle timer reset to ${this.timeoutMs / 1000}s.`);
                    } else if (response.status === 401 || response.redirected) {
                        console.warn(`[INACTIVITY] Server keep-alive rejected (Status: ${response.status}). Forcing logout.`);
                        this.forceLogout();
                    }
                })
                .catch(err => {
                    console.warn(`[INACTIVITY] Keep-alive network exception:`, err);
                    this.lastActivity = Date.now();
                    this.syncLastActivity();
                });
        },

        forceLogout: function () {
            this.hideWarning();
            if (this.checkInterval) clearInterval(this.checkInterval);
            try {
                sessionStorage.removeItem('memby_last_activity');
            } catch (e) { }
            window.location.href = '/account/logout';
        },

        injectModalHtml: function (remainingSecs) {
            // Remove existing modal if any
            const existing = document.getElementById('memby-session-timeout-modal');
            if (existing) existing.remove();

            this.injectStyles();

            const minutes = Math.round(this.timeoutMs / 60000);
            const minuteText = minutes === 1 ? '1 minute' : `${minutes} minutes`;

            const modalHtml = `
                <div id="memby-session-timeout-modal" class="memby-timeout-overlay" role="dialog" aria-modal="true" aria-labelledby="memby-timeout-title">
                    <div class="memby-timeout-card">
                        <div id="memby-timeout-icon-wrapper" class="memby-timeout-icon-wrapper ${remainingSecs <= 10 ? 'urgent' : ''}">
                            <div class="memby-timeout-icon-pulse"></div>
                            <svg class="memby-timeout-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <circle cx="12" cy="12" r="10"></circle>
                                <polyline points="12 6 12 12 16 14"></polyline>
                            </svg>
                        </div>
                        <h2 id="memby-timeout-title" class="memby-timeout-title">Session Expiring Soon</h2>
                        <p class="memby-timeout-desc">
                            You have been inactive for ${minuteText}. For HIPAA compliance and data security, your session will automatically terminate in:
                        </p>
                        <div id="memby-timeout-badge" class="memby-timeout-timer-badge ${remainingSecs <= 10 ? 'urgent' : ''}">
                            <span id="memby-timeout-count" class="memby-timeout-seconds">${remainingSecs}</span>
                            <span class="memby-timeout-unit">seconds</span>
                        </div>
                        <div class="memby-timeout-progress-container">
                            <div id="memby-timeout-progress" class="memby-timeout-progress-bar ${remainingSecs <= 10 ? 'urgent' : ''}" style="width: ${(remainingSecs / this.warningSeconds) * 100}%;"></div>
                        </div>
                        <div class="memby-timeout-actions">
                            <button type="button" id="memby-btn-keep-session" class="memby-btn-keep">
                                <svg class="memby-btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <polyline points="23 4 23 10 17 10"></polyline>
                                    <polyline points="1 20 1 14 7 14"></polyline>
                                    <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path>
                                </svg>
                                Keep Session
                            </button>
                            <button type="button" id="memby-btn-close-session" class="memby-btn-close">
                                <svg class="memby-btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
                                    <polyline points="16 17 21 12 16 7"></polyline>
                                    <line x1="21" y1="12" x2="9" y2="12"></line>
                                </svg>
                                Close
                            </button>
                        </div>
                    </div>
                </div>
            `;

            document.body.insertAdjacentHTML('beforeend', modalHtml);

            // Bind button events
            const keepBtn = document.getElementById('memby-btn-keep-session');
            if (keepBtn) {
                keepBtn.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    this.keepSession();
                });
                keepBtn.focus();
            }

            const closeBtn = document.getElementById('memby-btn-close-session');
            if (closeBtn) {
                closeBtn.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    this.forceLogout();
                });
            }
        },

        injectStyles: function () {
            if (document.getElementById('memby-timeout-styles')) return;
            const style = document.createElement('style');
            style.id = 'memby-timeout-styles';
            style.textContent = `
                .memby-timeout-overlay {
                    position: fixed;
                    top: 0; left: 0; right: 0; bottom: 0;
                    background-color: rgba(15, 23, 42, 0.75);
                    backdrop-filter: blur(8px);
                    -webkit-backdrop-filter: blur(8px);
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    z-index: 9999999;
                    padding: 1.25rem;
                    animation: membyFadeIn 0.25s cubic-bezier(0.16, 1, 0.3, 1);
                    font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                }
                .memby-timeout-card {
                    background: #ffffff;
                    border-radius: 1.25rem;
                    box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.35), 0 0 0 1px rgba(226, 232, 240, 0.8);
                    width: 100%;
                    max-width: 440px;
                    padding: 2rem;
                    text-align: center;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    position: relative;
                    animation: membyScaleIn 0.3s cubic-bezier(0.16, 1, 0.3, 1);
                }
                .memby-timeout-icon-wrapper {
                    position: relative;
                    width: 64px;
                    height: 64px;
                    border-radius: 50%;
                    background: #FEF3C7;
                    color: #D97706;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    margin-bottom: 1.25rem;
                    transition: all 0.3s ease;
                }
                .memby-timeout-icon-wrapper.urgent {
                    background: #FEE2E2;
                    color: #DC2626;
                }
                .memby-timeout-icon-pulse {
                    position: absolute;
                    width: 100%;
                    height: 100%;
                    border-radius: 50%;
                    background: inherit;
                    opacity: 0.6;
                    animation: membyPulseRing 2s cubic-bezier(0.215, 0.61, 0.355, 1) infinite;
                }
                .memby-timeout-icon {
                    width: 32px;
                    height: 32px;
                    position: relative;
                    z-index: 1;
                }
                .memby-timeout-title {
                    font-size: 1.35rem;
                    font-weight: 700;
                    color: #0F172A;
                    margin: 0 0 0.5rem 0;
                    letter-spacing: -0.02em;
                }
                .memby-timeout-desc {
                    font-size: 0.925rem;
                    color: #64748B;
                    line-height: 1.5;
                    margin: 0 0 1.25rem 0;
                }
                .memby-timeout-timer-badge {
                    display: inline-flex;
                    align-items: baseline;
                    gap: 0.35rem;
                    background: #F8FAFC;
                    border: 2px solid #E2E8F0;
                    border-radius: 0.85rem;
                    padding: 0.6rem 1.5rem;
                    margin-bottom: 1rem;
                    transition: all 0.2s ease;
                }
                .memby-timeout-timer-badge.urgent {
                    border-color: #FECACA;
                    background: #FEF2F2;
                }
                .memby-timeout-seconds {
                    font-size: 2rem;
                    font-weight: 800;
                    color: #D97706;
                    font-variant-numeric: tabular-nums;
                    line-height: 1;
                    transition: color 0.3s ease;
                }
                .memby-timeout-timer-badge.urgent .memby-timeout-seconds {
                    color: #DC2626;
                }
                .memby-timeout-unit {
                    font-size: 0.875rem;
                    font-weight: 600;
                    color: #64748B;
                    text-transform: uppercase;
                    letter-spacing: 0.05em;
                }
                .memby-timeout-progress-container {
                    width: 100%;
                    height: 6px;
                    background: #E2E8F0;
                    border-radius: 9999px;
                    overflow: hidden;
                    margin-bottom: 1.75rem;
                }
                .memby-timeout-progress-bar {
                    height: 100%;
                    background: #F59E0B;
                    border-radius: 9999px;
                    transition: width 0.9s linear, background-color 0.3s ease;
                }
                .memby-timeout-progress-bar.urgent {
                    background: #EF4444;
                }
                .memby-timeout-actions {
                    display: flex;
                    gap: 0.75rem;
                    width: 100%;
                }
                .memby-btn-keep {
                    flex: 1.4;
                    display: inline-flex;
                    align-items: center;
                    justify-content: center;
                    gap: 0.5rem;
                    background: #2563EB;
                    color: #FFFFFF;
                    border: none;
                    border-radius: 0.75rem;
                    padding: 0.75rem 1.25rem;
                    font-size: 0.95rem;
                    font-weight: 600;
                    cursor: pointer;
                    transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
                    box-shadow: 0 4px 14px rgba(37, 99, 235, 0.35);
                }
                .memby-btn-keep:hover {
                    background: #1D4ED8;
                    transform: translateY(-1px);
                    box-shadow: 0 6px 20px rgba(37, 99, 235, 0.45);
                }
                .memby-btn-keep:active {
                    transform: translateY(0);
                }
                .memby-btn-close {
                    flex: 1;
                    display: inline-flex;
                    align-items: center;
                    justify-content: center;
                    gap: 0.4rem;
                    background: #FFFFFF;
                    color: #64748B;
                    border: 1.5px solid #CBD5E1;
                    border-radius: 0.75rem;
                    padding: 0.75rem 1rem;
                    font-size: 0.95rem;
                    font-weight: 600;
                    cursor: pointer;
                    transition: all 0.2s ease;
                }
                .memby-btn-close:hover {
                    background: #FEF2F2;
                    color: #DC2626;
                    border-color: #FCA5A5;
                }
                .memby-btn-icon {
                    width: 18px;
                    height: 18px;
                }
                @keyframes membyFadeIn {
                    from { opacity: 0; }
                    to { opacity: 1; }
                }
                @keyframes membyScaleIn {
                    from { opacity: 0; transform: scale(0.95) translateY(8px); }
                    to { opacity: 1; transform: scale(1) translateY(0); }
                }
                @keyframes membyPulseRing {
                    0% { transform: scale(0.95); opacity: 0.8; }
                    50% { transform: scale(1.3); opacity: 0; }
                    100% { transform: scale(1.3); opacity: 0; }
                }
            `;
            document.head.appendChild(style);
        },

        // Helper to test in browser console: inactivityTracking.testWarning(10)
        testWarning: function (seconds) {
            this.showWarning(seconds || 30);
        },

        // Helper to set short timeout for testing: inactivityTracking.setTestTimeout(5)
        setTestTimeout: function (seconds) {
            this.timeoutMs = (seconds || 5) * 1000;
            this.lastActivity = Date.now();
            this.syncLastActivity();
            console.log(`[INACTIVITY] Inactivity timeout set to ${seconds || 5}s for testing.`);
        }
    };

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => window.inactivityTracking.init());
    } else {
        window.inactivityTracking.init();
    }
})();
