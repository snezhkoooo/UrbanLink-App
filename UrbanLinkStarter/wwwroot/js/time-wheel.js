/**
 * wheel.js — scrollable picker wheels
 *  [data-time-wheel]  – hours 0-23 + minutes 0/5/10…55
 *  [data-date-wheel]  – day + month, auto-current-year
 *  [data-year-wheel]  – year range  data-min / data-max
 *
 * Common attrs on host:
 *   data-target       – id of hidden input (receives the value)
 *   data-value        – initial value  ("HH:mm" / "YYYY-MM-DD" / "YYYY")
 *   data-combine-with – id of a second hidden input receiving date+T+time
 *   data-time-input   – id of the sibling time hidden (used by date wheel when combining)
 *   data-date-input   – id of the sibling date hidden (used by time wheel when combining)
 */
(function () {
    'use strict';

    var ROW    = 40;   // px — row height (must match CSS)
    var PAD    = 2;    // extra blank rows top and bottom to allow centering
    var MONTH_ABBR = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

    /* ── helpers ─────────────────────────────────────── */
    function pad2(n) { return String(n).padStart(2, '0'); }

    /**
     * Build one scrollable column.
     * values   – array of numbers
     * display  – optional array of strings (same length as values)
     * initVal  – the initial selected value
     * returns  { el, value(), setByValue(v) }
     */
    function makeCol(values, display, initVal) {
        var currentIdx = Math.max(0, values.indexOf(initVal));
        if (currentIdx < 0) currentIdx = 0;

        var outer = document.createElement('div');
        outer.className = 'ul-time-wheel-col';
        outer.style.position = 'relative';

        var inner = document.createElement('div');
        inner.className = 'ul-time-wheel-inner';
        outer.appendChild(inner);

        /* pad rows */
        function makePad() { var d=document.createElement('div'); d.className='ul-time-wheel-row ul-time-wheel-pad'; d.style.height=ROW+'px'; return d; }
        for (var p=0; p<PAD; p++) inner.appendChild(makePad());

        values.forEach(function(v, i) {
            var row = document.createElement('div');
            row.className = 'ul-time-wheel-row';
            row.style.height = ROW + 'px';
            row.dataset.val = v;
            row.textContent = display ? display[i] : pad2(v);
            inner.appendChild(row);
        });

        for (var q=0; q<PAD; q++) inner.appendChild(makePad());

        var api = {
            el: outer,
            onChange: null,
            value: function() { return values[currentIdx]; }
        };

        function highlight() {
            inner.querySelectorAll('.ul-time-wheel-row[data-val]').forEach(function(r, i) {
                r.classList.toggle('focused', i === currentIdx);
            });
        }

        function scrollTo(idx, smooth) {
            idx = Math.max(0, Math.min(idx, values.length - 1));
            currentIdx = idx;
            outer.scrollTo({ top: idx * ROW, behavior: smooth ? 'smooth' : 'auto' });
            highlight();
            if (api.onChange) api.onChange();
        }

        api.setByValue = function(v) {
            var idx = values.indexOf(v);
            if (idx >= 0) scrollTo(idx, false);
        };

        // Click to select
        inner.addEventListener('click', function(e) {
            var row = e.target.closest('.ul-time-wheel-row[data-val]');
            if (!row) return;
            var idx = values.indexOf(parseInt(row.dataset.val, 10));
            if (idx >= 0) scrollTo(idx, true);
        });

        // Scroll-snap
        var scrollTimer;
        outer.addEventListener('scroll', function() {
            clearTimeout(scrollTimer);
            scrollTimer = setTimeout(function() {
                var idx = Math.round(outer.scrollTop / ROW);
                scrollTo(idx, true);
            }, 80);
        });

        // Mouse-wheel
        outer.addEventListener('wheel', function(e) {
            e.preventDefault();
            scrollTo(currentIdx + (e.deltaY > 0 ? 1 : -1), true);
        }, { passive: false });

        // Init
        requestAnimationFrame(function() {
            outer.scrollTop = currentIdx * ROW;
            highlight();
        });

        return api;
    }

    /* ── TIME WHEEL ─────────────────────────────────── */
    function buildTimeWheel(host) {
        var hidden   = document.getElementById(host.dataset.target);
        var initVal  = host.dataset.value || (hidden && hidden.value) || '09:00';
        var parts    = initVal.split(':').map(Number);
        var initH    = isNaN(parts[0]) ? 9 : parts[0];
        var initM    = isNaN(parts[1]) ? 0 : Math.round(parts[1]/5)*5;
        if (initM >= 60) initM = 55;

        host.innerHTML = '';
        host.classList.add('ul-time-wheel');

        var hours   = Array.from({length:24}, function(_,i){return i;});
        var minutes = Array.from({length:12}, function(_,i){return i*5;});

        var hourCol   = makeCol(hours,   null, initH);
        var minuteCol = makeCol(minutes, null, initM);

        host.appendChild(hourCol.el);
        var sep = document.createElement('div');
        sep.className = 'ul-time-wheel-sep';
        sep.textContent = ':';
        host.appendChild(sep);
        host.appendChild(minuteCol.el);

        var band = document.createElement('div');
        band.className = 'ul-time-wheel-band';
        host.appendChild(band);

        function commit() {
            var h = pad2(hourCol.value());
            var m = pad2(minuteCol.value());
            var val = h + ':' + m;
            if (hidden) { hidden.value = val; hidden.dispatchEvent(new Event('change')); }
            sync(host, null, val);
        }
        hourCol.onChange   = commit;
        minuteCol.onChange = commit;
        commit();
    }

    /* ── DATE WHEEL ─────────────────────────────────── */
    function buildDateWheel(host) {
        var hidden  = document.getElementById(host.dataset.target);
        var today   = new Date();
        var year    = today.getFullYear();
        var initStr = host.dataset.value || (hidden && hidden.value) || today.toISOString().slice(0,10);
        var initD   = new Date(initStr + 'T00:00');
        if (isNaN(initD.getTime())) initD = today;

        var initDay   = initD.getDate();
        var initMonth = initD.getMonth(); // 0-11

        host.innerHTML = '';
        host.classList.add('ul-time-wheel');

        /* Month column */
        var monthVals = [0,1,2,3,4,5,6,7,8,9,10,11];
        var monthCol  = makeCol(monthVals, MONTH_ABBR, initMonth);

        /* Day column — wrapped in a container we can rebuild */
        var dayWrap = document.createElement('div');
        dayWrap.style.display = 'contents';

        var sep = document.createElement('div');
        sep.className = 'ul-time-wheel-sep';
        sep.textContent = '/';

        // Build order: day | sep | month
        var dayColApi;

        function buildDayCol(month, day) {
            var count = new Date(year, month+1, 0).getDate();
            var days  = Array.from({length:count}, function(_,i){return i+1;});
            var safeDay = Math.min(day, count);
            // Remove old day col if exists
            if (dayColApi) dayColApi.el.remove();
            dayColApi = makeCol(days, null, safeDay);
            dayColApi.onChange = commit;
            dayWrap.appendChild(dayColApi.el);
        }

        host.appendChild(dayWrap);
        host.appendChild(sep);
        host.appendChild(monthCol.el);
        var band = document.createElement('div');
        band.className = 'ul-time-wheel-band';
        host.appendChild(band);

        function commit() {
            if (!dayColApi) return;
            var m  = monthCol.value();
            var d  = dayColApi.value();
            var ds = year + '-' + pad2(m+1) + '-' + pad2(d);
            if (hidden) { hidden.value = ds; hidden.dispatchEvent(new Event('change')); }
            sync(host, ds, null);
        }

        monthCol.onChange = function() {
            var curDay = dayColApi ? dayColApi.value() : 1;
            buildDayCol(monthCol.value(), curDay);
            commit();
        };

        buildDayCol(initMonth, initDay);
    }

    /* ── YEAR WHEEL ─────────────────────────────────── */
    function buildYearWheel(host) {
        var hidden = document.getElementById(host.dataset.target);
        var initV  = parseInt(host.dataset.value || (hidden && hidden.value) || new Date().getFullYear(), 10);
        var min    = parseInt(host.dataset.min || '1980', 10);
        var max    = parseInt(host.dataset.max || '2030', 10);
        var years  = Array.from({length: max-min+1}, function(_,i){return min+i;});

        host.innerHTML = '';
        host.classList.add('ul-time-wheel');
        host.style.minWidth = '110px';

        var col = makeCol(years, null, initV);
        col.onChange = function() {
            if (hidden) { hidden.value = col.value(); hidden.dispatchEvent(new Event('change')); }
        };
        host.appendChild(col.el);
        var band = document.createElement('div');
        band.className = 'ul-time-wheel-band';
        host.appendChild(band);
    }

    /* ── combine helper (keeps the combined datetime hidden in sync) ─ */
    function sync(host, dateVal, timeVal) {
        var combineId = host.dataset.combineWith;
        if (!combineId) return;
        var combined = document.getElementById(combineId);
        if (!combined) return;

        var d = dateVal, t = timeVal;
        if (!d) {
            var dInputId = host.dataset.dateInput;
            d = dInputId ? (document.getElementById(dInputId)?.value || '') : '';
        }
        if (!t) {
            var tInputId = host.dataset.timeInput;
            t = tInputId ? (document.getElementById(tInputId)?.value || '') : '';
        }
        if (d && t) combined.value = d + 'T' + t;
    }

    /* ── attach all wheels on page ─────────────────── */
    function attach() {
        document.querySelectorAll('[data-time-wheel]').forEach(function(el) {
            if (el.dataset.wheelInit) return;
            el.dataset.wheelInit = '1';
            buildTimeWheel(el);
        });
        document.querySelectorAll('[data-date-wheel]').forEach(function(el) {
            if (el.dataset.wheelInit) return;
            el.dataset.wheelInit = '1';
            buildDateWheel(el);
        });
        document.querySelectorAll('[data-year-wheel]').forEach(function(el) {
            if (el.dataset.wheelInit) return;
            el.dataset.wheelInit = '1';
            buildYearWheel(el);
        });
    }

    document.addEventListener('DOMContentLoaded', attach);
    window.__wheelAttach = attach;
})();
