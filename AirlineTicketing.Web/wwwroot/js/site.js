// AeroViet · tương tác phía khách hàng: danh sách chuyến bay, chọn ghế, thanh toán.

(function () {
    'use strict';

    var money = function (value) {
        var amount = Number(value) || 0;
        return amount.toLocaleString('vi-VN', { maximumFractionDigits: 0 }) + ' ₫';
    };

    // ------------------------------------------------ danh sách chuyến bay

    function initFlightList() {
        var button = document.querySelector('[data-show-more]');
        if (!button) {
            return;
        }

        var items = Array.prototype.slice.call(document.querySelectorAll('[data-flight-item]'));
        var label = button.querySelector('[data-show-more-label]');
        var step = Math.max(1, parseInt(button.getAttribute('data-step'), 10) || 3);

        var update = function () {
            var hidden = items.filter(function (item) {
                return item.classList.contains('is-hidden');
            });
            if (!hidden.length) {
                var wrapper = button.closest('.results-more');
                if (wrapper) {
                    wrapper.classList.add('is-hidden');
                } else {
                    button.hidden = true;
                }
                return;
            }
            if (label) {
                label.textContent = 'Xem thêm ' + Math.min(step, hidden.length) + ' chuyến';
            }
        };

        button.addEventListener('click', function () {
            var hidden = items.filter(function (item) {
                return item.classList.contains('is-hidden');
            });
            hidden.slice(0, step).forEach(function (item) {
                item.classList.remove('is-hidden');
            });
            update();
        });

        update();
    }

    // ------------------------------------------------------- chọn chỗ ngồi

    function initSeatPicker() {
        var area = document.querySelector('[data-seat-area]');
        if (!area) {
            return;
        }

        var buttons = Array.prototype.slice.call(area.querySelectorAll('.seat[data-seat-code]'));
        var input = document.querySelector('[data-seat-input]');
        var submit = document.querySelector('[data-seat-submit]');
        var title = area.querySelector('[data-seat-title]');
        var badge = area.querySelector('[data-seat-badge]');
        var price = area.querySelector('[data-seat-price]');
        var hint = area.querySelector('[data-seat-hint]');

        var select = function (button, silent) {
            buttons.forEach(function (item) {
                var isSelected = item === button;
                item.classList.remove('is-selected', 'is-chosen');
                item.classList.toggle('is-selected', isSelected);
                item.setAttribute('aria-pressed', isSelected ? 'true' : 'false');
                if (!item.disabled) {
                    item.textContent = item.getAttribute('data-seat-letter') || item.textContent;
                }
            });

            if (button) {
                button.classList.add('is-selected');
                button.textContent = '✓';
            }

            var code = button ? button.getAttribute('data-seat-code') : '';
            var typeName = button ? button.getAttribute('data-seat-type-name') : '';
            var position = button ? button.getAttribute('data-seat-position') : '';
            var surcharge = button ? Number(button.getAttribute('data-seat-surcharge')) || 0 : 0;

            if (input) {
                input.value = code;
            }

            if (title) {
                title.textContent = code ? 'Ghế ' + code + ' · ' + typeName : 'Chưa chọn chỗ ngồi';
            }
            if (badge) {
                badge.textContent = code ? 'Đã chọn · ' + position : 'Vui lòng chọn ghế';
            }
            if (price) {
                price.textContent = surcharge > 0 ? '+' + money(surcharge) : 'Miễn phí';
            }
            if (hint) {
                hint.textContent = code
                    ? 'Ghế đã được giữ tạm cho bạn đến bước thanh toán.'
                    : 'Bạn cần chọn một ghế trước khi tiếp tục thanh toán.';
            }
            if (submit && !silent) {
                submit.disabled = !code;
            }

            var alert = area.parentElement && area.parentElement.querySelector('.av-alert--danger');
            if (alert && code) {
                alert.hidden = true;
            }
        };

        buttons.forEach(function (button) {
            // Ghi nhớ nhãn gốc (chữ cái cuối của mã ghế) để khôi phục khi bỏ chọn.
            var code = button.getAttribute('data-seat-code') || '';
            if (code) {
                button.setAttribute('data-seat-letter', code.slice(-1));
            }
            button.addEventListener('click', function () {
                if (button.disabled || button.classList.contains('is-taken')) {
                    return;
                }
                select(button);
            });
        });

        // Khôi phục ghế đã chọn trước đó (nếu có).
        var chosen = buttons.filter(function (button) {
            return button.classList.contains('is-selected');
        })[0];
        select(chosen || null, true);

        var form = area.closest('form');
        if (form) {
            form.addEventListener('submit', function (event) {
                if (!input || !input.value) {
                    event.preventDefault();
                    if (hint) {
                        hint.textContent = 'Vui lòng chọn một ghế trước khi tiếp tục thanh toán.';
                        hint.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                }
            });
        }
    }

    // ---------------------------------------------------- phương thức thanh toán

    function initPayment() {
        var inputs = Array.prototype.slice.call(document.querySelectorAll('[data-payment-input]'));
        if (!inputs.length) {
            return;
        }

        var card = document.querySelector('[data-card-fields]');
        var transfer = document.querySelector('[data-transfer-fields]');

        var apply = function (value) {
            inputs.forEach(function (input) {
                var chip = input.closest('.av-chip');
                if (chip) {
                    chip.classList.toggle('is-active', input.value === value);
                }
            });
            if (card) {
                card.classList.toggle('is-hidden', value !== 'card');
            }
            if (transfer) {
                transfer.classList.toggle('is-hidden', value !== 'transfer');
            }
        };

        inputs.forEach(function (input) {
            input.addEventListener('change', function () {
                if (input.checked) {
                    apply(input.value);
                }
            });
        });

        var checked = inputs.filter(function (input) { return input.checked; })[0] || inputs[0];
        apply(checked.value);
    }

    function initCardFormat() {
        var number = document.querySelector('[data-card-number]');
        if (!number) {
            return;
        }

        var format = function () {
            var digits = number.value.replace(/\D/g, '').slice(0, 19);
            number.value = digits.replace(/(.{4})/g, '$1 ').trim();
        };

        number.addEventListener('input', format);
        format();
    }

    function init() {
        initFlightList();
        initSeatPicker();
        initPayment();
        initCardFormat();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
