<<<<<<< Updated upstream
﻿// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
=======
// AeroViet · tương tác phía khách hàng: danh sách chuyến bay, chọn ghế, thanh toán.

(function () {
    'use strict';

    var english = document.documentElement.lang === 'en';
    var t = function (vietnamese, translation) { return english ? translation : vietnamese; };
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
        items.forEach(function (item, index) { item.classList.toggle('is-hidden', index >= step); });
        var moreWrapper = button.closest('.results-more');
        if (moreWrapper) moreWrapper.hidden = false;

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
                var count = Math.min(step, hidden.length);
                label.textContent = t('Xem thêm ' + count + ' chuyến',
                    'Show ' + count + ' more ' + (count === 1 ? 'flight' : 'flights'));
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
            var firstNewLink = hidden[0] && hidden[0].querySelector('a[href]');
            if (firstNewLink) firstNewLink.focus();
        });

        update();
    }

    // ------------------------------------------------------- chọn chỗ ngồi

    function initSeatPicker() {
        var pickers = Array.from(document.querySelectorAll('[data-seat-area]')).map(initSeatArea).filter(Boolean);
        new Set(pickers.map(function (picker) { return picker.form; })).forEach(function (form) {
            form.addEventListener('submit', function (event) {
                var missing = pickers.find(function (picker) { return picker.form === form && picker.missing() !== -1; });
                if (missing) {
                    event.preventDefault();
                    missing.focusMissing();
                }
            });
        });
    }

    function initSeatArea(area) {
        var form = area.closest('form');
        var grid = area.querySelector('[data-seat-grid]');
        var persons = Array.from(area.querySelectorAll('[data-seat-passenger]')).map(function (person) {
            return {
                input: person.querySelector('[data-seat-input]'),
                button: person.querySelector('[data-seat-person]'),
                status: person.querySelector('[data-seat-choice]'),
                fallback: person.querySelector('[data-seat-fallback]')
            };
        });
        var isJourney = persons.length > 0;
        var fields = isJourney ? persons : [{ input: form && form.querySelector('[data-seat-input]') }];
        if (!form || !grid || fields.some(function (field) { return !field.input; })) return null;

        var buttons = Array.from(grid.querySelectorAll('button[data-seat-code]'));
        var taken = new Set(buttons.filter(function (button) { return button.disabled; }));
        var codes = new Set(buttons.filter(function (button) { return !button.disabled; }).map(seatCode));
        var submit = !isJourney && form.querySelector('[data-seat-submit]');
        var title = area.querySelector('[data-seat-title]');
        var badge = area.querySelector('[data-seat-badge]');
        var price = area.querySelector('[data-seat-price]');
        var hint = area.querySelector('[data-seat-hint]');
        var activePerson = area.querySelector('[data-seat-active-person]');
        var seen = new Set();
        var active = 0;

        function seatCode(button) { return button.dataset.seatCode; }
        function missing() { return fields.findIndex(function (field) { return !field.input.value; }); }
        function available() { return buttons.filter(function (button) { return !button.disabled; }); }
        function tabStop(button, focus) {
            buttons.forEach(function (item) { item.tabIndex = item === button ? 0 : -1; });
            if (focus && button) button.focus();
        }

        fields.forEach(function (field, index) {
            var code = field.input.value;
            if (seen.has(code) || !codes.has(code)) field.input.value = '';
            else seen.add(code);
            if (!isJourney) return;
            field.input.required = false;
            field.fallback.hidden = true;
            field.button.hidden = false;
            field.button.addEventListener('click', function () {
                active = index;
                render();
            });
        });

        if (isJourney) {
            area.querySelector('[data-seat-panel]').hidden = false;
            active = Math.max(0, missing());
        }

        function render() {
            var code = fields[active].input.value;
            var otherChoices = new Set(fields.filter(function (field, index) { return index !== active; })
                .map(function (field) { return field.input.value; }));
            var selected = null;
            buttons.forEach(function (button) {
                var assigned = otherChoices.has(seatCode(button));
                var blocked = taken.has(button) || assigned;
                var current = !blocked && seatCode(button) === code;
                button.disabled = blocked;
                button.classList.toggle('is-taken', blocked);
                button.classList.toggle('is-selected', current);
                button.setAttribute('aria-pressed', String(current));
                button.textContent = current ? '✓' : blocked ? '×' : button.dataset.seatLetter;
                if (current) selected = button;
            });

            persons.forEach(function (person, index) {
                var chosen = person.input.value;
                person.button.setAttribute('aria-pressed', String(index === active));
                person.status.textContent = chosen ? t('Ghế ', 'Seat ') + chosen : t('Chưa chọn ghế', 'No seat selected');
            });
            if (activePerson) activePerson.textContent = fields[active].button.dataset.seatPersonName;

            if (title) title.textContent = selected ? t('Ghế ', 'Seat ') + code + ' · ' + selected.dataset.seatTypeName : t('Chưa chọn chỗ ngồi', 'No seat selected');
            if (badge) badge.textContent = selected ? t('Đã chọn · ', 'Selected · ') + selected.dataset.seatPosition : t('Vui lòng chọn ghế', 'Choose a seat');
            var surcharge = selected ? Number(selected.dataset.seatSurcharge) || 0 : 0;
            if (price) price.textContent = selected ? (surcharge > 0 ? '+' + money(surcharge) : t('Miễn phí', 'Free')) : '';
            if (hint) hint.textContent = code
                ? t('Ghế sẽ được giữ khi bạn xác nhận thông tin và tiếp tục thanh toán.', 'Your seat will be held when you confirm your details and proceed to payment.')
                : t('Chọn một ghế còn trống để tiếp tục.', 'Choose an available seat to continue.');
            if (submit) submit.disabled = !code;
            tabStop(selected || available()[0], false);
        }

        buttons.forEach(function (button) {
            button.addEventListener('click', function () {
                if (button.disabled) return;
                fields[active].input.value = seatCode(button);
                render();
                if (!isJourney) {
                    var alert = area.parentElement.querySelector('.av-alert--danger');
                    if (alert) alert.hidden = true;
                }
            });
        });

        grid.addEventListener('keydown', function (event) {
            var seats = available();
            var index = seats.indexOf(event.target);
            if (index < 0) return;
            var key = event.key;
            var next;
            if (key === 'ArrowLeft') next = seats[index - 1];
            else if (key === 'ArrowRight') next = seats[index + 1];
            else if (key === 'Home') next = seats[0];
            else if (key === 'End') next = seats[seats.length - 1];
            else if (key === 'ArrowUp' || key === 'ArrowDown') {
                var code = seatCode(seats[index]);
                var row = Number(code.slice(0, -1));
                var step = key === 'ArrowUp' ? -1 : 1;
                var lastRow = Number(seatCode(seats[seats.length - 1]).slice(0, -1));
                for (var target = row + step; target >= 1 && target <= lastRow; target += step) {
                    next = seats.find(function (seat) { return seatCode(seat) === target + code.slice(-1); });
                    if (next) break;
                }
            } else return;
            event.preventDefault();
            tabStop(next, true);
        });

        render();
        return {
            form: form,
            missing: missing,
            focusMissing: function () {
                active = missing();
                if (active < 0) return;
                render();
                if (hint) {
                    hint.textContent = t('Vui lòng chọn ghế trước khi tiếp tục thanh toán.', 'Choose a seat before proceeding to payment.');
                }
                area.scrollIntoView({ block: 'start' });
                var firstSeat = available()[0];
                if (firstSeat) tabStop(firstSeat, true);
                else if (fields[active].button) fields[active].button.focus();
            }
        };
    }

    // ---------------------------------------------------- phương thức thanh toán

    function initPayment() {
        var inputs = Array.prototype.slice.call(document.querySelectorAll('[data-payment-input]'));
        if (!inputs.length) {
            return;
        }

        var card = document.querySelector('[data-card-fields]');
        var transfer = document.querySelector('[data-transfer-fields]');
        var gateway = document.querySelector('[data-vnpay-fields]');
        var label = document.querySelector('[data-payment-submit]');

        var apply = function (value) {
            if (card) {
                card.classList.toggle('is-hidden', value !== 'card');
            }
            if (transfer) {
                transfer.classList.toggle('is-hidden', value !== 'transfer');
            }
            if (gateway) {
                gateway.classList.toggle('is-hidden', value !== 'vnpay');
            }
            if (label) {
                label.textContent = label.dataset[value + 'Label'];
            }
        };

        inputs.forEach(function (input) {
            input.addEventListener('change', function () {
                if (input.checked) {
                    apply(input.value);
                }
            });
        });

        var checked = inputs.find(function (input) { return input.checked; }) || inputs[0];
        apply(checked.value);
    }

    // ------------------------------------------------------- sắp xếp chuyến bay

    function initSort() {
        var select = document.querySelector('[data-sort-select]');
        if (!select) {
            return;
        }

        var form = select.form || select.closest('form');
        if (!form) {
            return;
        }

        select.addEventListener('change', function () {
            if (typeof form.requestSubmit === 'function') {
                form.requestSubmit();
            } else {
                form.submit();
            }
        });
    }

    function init() {
        var journeySearch = document.querySelector('[data-journey-search]');
        if (journeySearch) {
            var returnDate = journeySearch.querySelector('[data-return-date]');
            var departureDate = journeySearch.querySelector('[name="depart"]');
            var updateJourneySearch = function () {
                var roundtrip = journeySearch.querySelector('[name="trip"]:checked').value === 'roundtrip';
                returnDate.disabled = !roundtrip;
                returnDate.required = roundtrip;
                returnDate.min = departureDate.value;
                journeySearch.querySelectorAll('[name="trip"]').forEach(function (input) {
                    input.closest('label').classList.toggle('is-active', input.checked);
                });
            };
            journeySearch.addEventListener('change', updateJourneySearch);
            updateJourneySearch();
        }
        initFlightList();
        initSeatPicker();
        initPayment();
        initSort();
        var filterDrawer = document.querySelector('[data-filter-drawer]');
        if (filterDrawer && window.matchMedia) {
            var compactFilters = window.matchMedia('(max-width: 1080px)');
            var adaptFilters = function () {
                filterDrawer.open = !compactFilters.matches || filterDrawer.getAttribute('data-filter-errors') === 'true';
            };
            adaptFilters();
            if (compactFilters.addEventListener) compactFilters.addEventListener('change', adaptFilters);
        }
        var summary = document.querySelector('[data-validation-summary]');
        if (summary) {
            summary.focus();
            summary.addEventListener('click', function (event) {
                var link = event.target.closest('a[href^="#"]');
                if (!link) return;
                var target = document.getElementById(link.hash.slice(1));
                if (!target) return;
                var control = target.matches('input, select, textarea, button') ? target
                    : target.querySelector('input:not([type="hidden"]), select, textarea, button:not(:disabled)');
                if (control) { event.preventDefault(); control.focus(); }
            });
        }
        initStaffNav();
        var deadline = document.querySelector('[data-hold-until]');
        if (deadline) {
            var expires = Date.parse(deadline.getAttribute('data-hold-until'));
            if (Number.isFinite(expires)) {
                var notice = deadline.closest('.av-alert');
                var status = document.querySelector('[data-hold-status]');
                var state = 'held';
                var updateHold = function () {
                    var seconds = Math.max(0, Math.ceil((expires - Date.now()) / 1000));
                    deadline.textContent = seconds > 0
                        ? t('Thời gian giữ chỗ còn ', 'Time left on your hold: ') + Math.floor(seconds / 60) + ':' + String(seconds % 60).padStart(2, '0')
                        : t('Đặt chỗ đã hết hạn. Vui lòng tìm chuyến và đặt lại.', 'Your booking hold has expired. Search for a flight and book again.');
                    var nextState = seconds === 0 ? 'expired' : seconds <= 120 ? 'urgent' : 'held';
                    if (nextState !== state) {
                        state = nextState;
                        if (notice) notice.dataset.holdState = state;
                        if (status) status.textContent = state === 'expired'
                            ? deadline.textContent : t('Thời gian giữ chỗ còn không quá hai phút.', 'Your booking hold expires in two minutes or less.');
                    }
                    if (!seconds) {
                        document.querySelectorAll('form[action="/dat-ve/thanh-toan"] button[type="submit"], form[action="/dat-ve/khuyen-mai"] button').forEach(function (button) { button.disabled = true; });
                        clearInterval(holdTimer);
                    }
                };
                var holdTimer = setInterval(updateHold, 1000);
                updateHold();
            }
        }
    }

    function initStaffNav() {
        var toggle = document.getElementById('admin-nav-toggle');
        var side = document.getElementById('staff-navigation');
        var open = document.querySelector('[data-staff-nav-open]');
        var close = document.querySelector('[data-staff-nav-close]');
        if (!toggle || !side || !open || !close) return;
        var small = window.matchMedia('(max-width: 1024px)');
        var update = function () {
            var expanded = small.matches && toggle.checked;
            side.inert = small.matches && !expanded;
            open.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        };
        open.addEventListener('click', function () {
            toggle.checked = !toggle.checked;
            update();
            if (toggle.checked) window.requestAnimationFrame(function () { if (toggle.checked) close.focus(); });
        });
        var collapse = function () {
            toggle.checked = false;
            update();
            open.focus();
        };
        close.addEventListener('click', collapse);
        toggle.addEventListener('change', function () {
            update();
            if (!toggle.checked) open.focus();
        });
        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && toggle.checked && small.matches) collapse();
        });
        small.addEventListener('change', function () {
            toggle.checked = false;
            update();
        });
        update();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
>>>>>>> Stashed changes
