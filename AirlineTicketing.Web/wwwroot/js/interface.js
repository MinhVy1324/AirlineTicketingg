(() => {
    'use strict';

    const english = document.documentElement.lang === 'en';
    const t = (vietnamese, translation) => english ? translation : vietnamese;
    const feedback = document.querySelector('[data-interface-feedback]');
    if (!feedback) return;

    const display = feedback.querySelector('[data-feedback-display]');
    const text = feedback.querySelector('[data-feedback-text]');
    const live = feedback.querySelector('[data-feedback-live]');
    const pending = new Map();
    let dismissTimer;

    function dismiss() {
        window.clearTimeout(dismissTimer);
        display.hidden = true;
        live.textContent = '';
    }

    function show(message, tone = 'info') {
        if (!display.hidden && text.textContent === message && display.dataset.tone === tone) return;
        window.clearTimeout(dismissTimer);
        display.dataset.tone = tone;
        text.textContent = message;
        live.textContent = message;
        display.hidden = false;
        if (tone !== 'pending') dismissTimer = window.setTimeout(dismiss, 4000);
    }

    function restoreAttribute(element, attribute, value) {
        if (value === null) element.removeAttribute(attribute);
        else element.setAttribute(attribute, value);
    }

    window.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || event.defaultPrevented) return;
        if (pending.has(form)) {
            event.preventDefault();
            return;
        }

        const button = event.submitter;
        const target = button?.getAttribute('formtarget') ?? form.getAttribute('target');
        const method = button?.getAttribute('formmethod') ?? form.method;
        if ((target && target.toLowerCase() !== '_self') || method.toLowerCase() === 'dialog') return;

        if (button?.hasAttribute('data-feedback-download') || form.hasAttribute('data-feedback-download')) {
            show(t('Đang yêu cầu tệp tải xuống…', 'Requesting your download…'));
            return;
        }

        // Giữ nguyên name/value của nút để trình duyệt gửi đúng hành động đã chọn.
        const state = { button, busy: form.getAttribute('aria-busy'), disabled: button?.getAttribute('aria-disabled') };
        pending.set(form, state);
        form.setAttribute('aria-busy', 'true');
        button?.setAttribute('aria-disabled', 'true');
        button?.setAttribute('data-submitting', '');
        state.timer = window.setTimeout(() => {
            show(button?.dataset.pendingMessage ?? form.dataset.pendingMessage ?? t('Đang xử lý yêu cầu…', 'Processing your request…'), 'pending');
        }, 140);
    });

    window.addEventListener('pageshow', () => {
        for (const [form, state] of pending) {
            window.clearTimeout(state.timer);
            restoreAttribute(form, 'aria-busy', state.busy);
            if (state.button) {
                restoreAttribute(state.button, 'aria-disabled', state.disabled);
                state.button.removeAttribute('data-submitting');
            }
        }
        pending.clear();
        dismiss();
    });

    document.addEventListener('invalid', event => {
        if (event.target instanceof HTMLElement && event.target.closest('form')) {
            show(t('Kiểm tra các trường được đánh dấu.', 'Check the highlighted fields.'), 'warning');
        }
    }, true);

    document.querySelectorAll('[data-password-toggle]').forEach(button => {
        const input = document.getElementById(button.getAttribute('aria-controls'));
        if (!(input instanceof HTMLInputElement)) return;
        button.hidden = false;
        button.addEventListener('click', () => {
            const visible = input.type === 'password';
            input.type = visible ? 'text' : 'password';
            button.textContent = visible ? t('Ẩn', 'Hide') : t('Hiện', 'Show');
            button.setAttribute('aria-label', visible ? t('Ẩn mật khẩu', 'Hide password') : t('Hiện mật khẩu', 'Show password'));
        });
    });

    if (navigator.clipboard?.writeText) {
        document.querySelectorAll('[data-copy-value]').forEach(button => {
            button.hidden = false;
            button.addEventListener('click', async () => {
                try {
                    await navigator.clipboard.writeText(button.dataset.copyValue);
                    show(button.dataset.copyMessage ?? t('Đã sao chép.', 'Copied.'), 'success');
                } catch {
                    show(t('Hãy chọn và sao chép nội dung đang hiển thị.', 'Select the displayed text and copy it.'), 'warning');
                }
            });
        });
    }

    document.addEventListener('click', event => {
        const link = event.target instanceof Element ? event.target.closest('a[data-feedback-download]') : null;
        if (link && !event.defaultPrevented && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey) {
            show(t('Đang yêu cầu tệp tải xuống…', 'Requesting your download…'));
        }
    });

    const dialog = document.querySelector('.av-modal[aria-modal="true"]');
    if (dialog) {
        const panel = dialog.querySelector('.av-modal__panel');
        const backdrop = dialog.querySelector('.av-modal__backdrop');
        const heading = document.getElementById(dialog.getAttribute('aria-labelledby'));
        if (panel && heading) {
            document.body.classList.add('av-modal-open');
            backdrop?.setAttribute('tabindex', '-1');
            heading.setAttribute('tabindex', '-1');
            heading.focus();
            document.addEventListener('keydown', event => {
                if (event.defaultPrevented) return;
                if (event.key === 'Escape' && backdrop) {
                    event.preventDefault();
                    backdrop.click();
                }
                if (event.key !== 'Tab') return;
                const controls = Array.from(panel.querySelectorAll('a[href], button, input, select, textarea, [tabindex]'))
                    .filter(element => element.tabIndex >= 0 && !element.matches(':disabled') && element.getClientRects().length);
                const first = controls[0];
                const last = controls.at(-1);
                if (!first) return;
                const focused = document.activeElement;
                if (event.shiftKey ? focused === first || focused === heading : focused === last) {
                    event.preventDefault();
                    (event.shiftKey ? last : first).focus();
                }
            });
        }
    }
})();
