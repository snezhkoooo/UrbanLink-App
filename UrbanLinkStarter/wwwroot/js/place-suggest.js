/**
 * place-suggest.js — Bulgaria-only, cities/towns/villages only.
 * Attach data-place-suggest to any input.
 */
(function () {
    'use strict';

    var NOMINATIM = 'https://nominatim.openstreetmap.org/search';
    var DEBOUNCE_MS = 320;

    // Nominatim "type" values that count as a settlement.
    var ALLOWED_TYPES = ['city', 'town', 'village', 'hamlet', 'administrative', 'municipality'];

    function debounce(fn, ms) {
        var t;
        return function () {
            clearTimeout(t);
            t = setTimeout(fn.bind(this, arguments[0]), ms);
        };
    }

    function escHtml(s) {
        return (s || '').replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function pinIcon() {
        return '<svg class="ul-suggest-item-icon" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>';
    }

    function buildList(results) {
        var ul = document.createElement('div');
        ul.className = 'ul-suggest-list';
        results.forEach(function (r, i) {
            var item = document.createElement('div');
            item.className = 'ul-suggest-item';
            item.dataset.idx = i;

            // r.display_name = "Sofia, Stolichna, Sofia City Province, ..."
            // We want the settlement name + region.
            var parts = r.display_name.split(',').map(function (s) { return s.trim(); });
            var primary   = parts[0];
            // try to find the province (usually one of the last 2 parts before "Bulgaria")
            var bgIdx = parts.findIndex(function (p) { return /bulgaria/i.test(p); });
            var secondary = bgIdx > 1 ? parts[bgIdx - 1] : (parts[1] || '');

            item.innerHTML =
                pinIcon() +
                '<span class="ul-suggest-item-name">' + escHtml(primary) + '</span>' +
                '<span class="ul-suggest-item-sub">' + escHtml(secondary) + '</span>';
            // Save just the city name + province (no street noise)
            item.dataset.value = primary + (secondary ? ', ' + secondary : '');
            ul.appendChild(item);
        });
        return ul;
    }

    function init(input) {
        var parent = input.parentNode;
        var wrap = document.createElement('div');
        wrap.className = 'ul-suggest-wrap';
        parent.insertBefore(wrap, input);
        wrap.appendChild(input);

        var list = null;
        var focusedIdx = -1;
        var lastQuery = '';
        var controller = null;

        function close() {
            if (list) { list.remove(); list = null; }
            focusedIdx = -1;
        }

        function setFocus(idx) {
            if (!list) return;
            var items = list.querySelectorAll('.ul-suggest-item');
            items.forEach(function (el) { el.classList.remove('focused'); });
            if (idx >= 0 && idx < items.length) {
                items[idx].classList.add('focused');
                items[idx].scrollIntoView({ block: 'nearest' });
                focusedIdx = idx;
            }
        }

        function select(value) { input.value = value; close(); }

        var search = debounce(function (q) {
            if (q.length < 2) { close(); return; }
            if (q === lastQuery) return;
            lastQuery = q;

            if (controller) controller.abort();
            controller = new AbortController();

            var url = NOMINATIM +
                '?format=json&limit=10&addressdetails=1' +
                '&countrycodes=bg' +
                '&q=' + encodeURIComponent(q) +
                '&accept-language=en';

            fetch(url, { signal: controller.signal, headers: { 'Accept': 'application/json' } })
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    close();
                    if (!data || !data.length) return;

                    // Keep only settlements: type ∈ allowed, AND class ∈ {place, boundary}.
                    // Reject anything where address contains a 'road', 'house_number', etc.
                    var filtered = data.filter(function (r) {
                        if (r['class'] === 'highway' || r['class'] === 'building') return false;
                        if (!ALLOWED_TYPES.includes(r['type'])) return false;
                        if (r.address && (r.address.road || r.address.house_number)) return false;
                        return true;
                    });

                    // Dedupe by primary display name
                    var seen = {};
                    filtered = filtered.filter(function (r) {
                        var key = r.display_name.split(',')[0].trim().toLowerCase();
                        if (seen[key]) return false;
                        seen[key] = true; return true;
                    }).slice(0, 6);

                    if (!filtered.length) return;
                    list = buildList(filtered);
                    wrap.appendChild(list);

                    list.addEventListener('mousedown', function (e) {
                        var item = e.target.closest('.ul-suggest-item');
                        if (item) { e.preventDefault(); select(item.dataset.value); }
                    });
                })
                .catch(function () {});
        }, DEBOUNCE_MS);

        input.addEventListener('input', function () { search(input.value.trim()); });
        input.addEventListener('keydown', function (e) {
            if (!list) return;
            var items = list.querySelectorAll('.ul-suggest-item');
            if (e.key === 'ArrowDown') { e.preventDefault(); setFocus(Math.min(focusedIdx + 1, items.length - 1)); }
            if (e.key === 'ArrowUp')   { e.preventDefault(); setFocus(Math.max(focusedIdx - 1, 0)); }
            if (e.key === 'Enter' && focusedIdx >= 0) { e.preventDefault(); select(items[focusedIdx].dataset.value); }
            if (e.key === 'Escape') close();
        });

        document.addEventListener('click', function (e) { if (!wrap.contains(e.target)) close(); });
    }

    function attach() {
        document.querySelectorAll('input[data-place-suggest]').forEach(function (el) {
            if (el.dataset.suggestInit) return;
            el.dataset.suggestInit = '1';
            init(el);
        });
    }

    document.addEventListener('DOMContentLoaded', attach);
    window.__placeSuggestAttach = attach;
})();
