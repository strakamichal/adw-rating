/**
 * Global search dropdown — fetches results from /api/search and
 * populates a dropdown under each .search-wrapper element.
 */
(function () {
    'use strict';

    let debounceTimer = null;
    let activeWrapper = null;
    let selectedIndex = -1;

    function init() {
        document.addEventListener('input', function (e) {
            const input = e.target.closest('.search-input__field');
            if (!input) return;

            const wrapper = input.closest('.search-wrapper');
            if (!wrapper) return;

            const query = input.value.trim();
            clearTimeout(debounceTimer);

            if (query.length < 2) {
                hideDropdown(wrapper);
                return;
            }

            debounceTimer = setTimeout(function () {
                fetchResults(wrapper, query);
            }, 300);
        });

        // Click outside closes dropdown
        document.addEventListener('click', function (e) {
            if (!e.target.closest('.search-wrapper')) {
                document.querySelectorAll('.search-dropdown').forEach(function (dd) {
                    dd.style.display = 'none';
                });
                activeWrapper = null;
            }
        });

        // Keyboard navigation
        document.addEventListener('keydown', function (e) {
            if (!activeWrapper) return;

            const dropdown = activeWrapper.querySelector('.search-dropdown');
            if (!dropdown || dropdown.style.display === 'none') return;

            const items = dropdown.querySelectorAll('.search-dropdown__item');
            if (items.length === 0) return;

            if (e.key === 'ArrowDown') {
                e.preventDefault();
                selectedIndex = Math.min(selectedIndex + 1, items.length - 1);
                updateSelection(items);
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                selectedIndex = Math.max(selectedIndex - 1, 0);
                updateSelection(items);
            } else if (e.key === 'Enter' && selectedIndex >= 0) {
                e.preventDefault();
                items[selectedIndex].click();
            } else if (e.key === 'Escape') {
                hideDropdown(activeWrapper);
            }
        });
    }

    function updateSelection(items) {
        items.forEach(function (item, i) {
            item.classList.toggle('search-dropdown__item--active', i === selectedIndex);
        });
    }

    function hideDropdown(wrapper) {
        const dropdown = wrapper.querySelector('.search-dropdown');
        if (dropdown) dropdown.style.display = 'none';
        activeWrapper = null;
        selectedIndex = -1;
    }

    function fetchResults(wrapper, query) {
        // Determine base URL: if the page has a meta tag or data attribute for API base, use it.
        // Otherwise, try relative /api/search (works when Web proxies API or same host).
        const apiBase = document.querySelector('meta[name="api-base-url"]');
        const baseUrl = apiBase ? apiBase.content.replace(/\/$/, '') : '';
        const url = baseUrl + '/api/search?q=' + encodeURIComponent(query) + '&limit=10';

        fetch(url)
            .then(function (res) { return res.json(); })
            .then(function (results) {
                renderDropdown(wrapper, results, query);
            })
            .catch(function () {
                // Silently ignore errors
            });
    }

    function renderDropdown(wrapper, results, query) {
        let dropdown = wrapper.querySelector('.search-dropdown');
        if (!dropdown) {
            dropdown = document.createElement('div');
            dropdown.className = 'search-dropdown';
            wrapper.appendChild(dropdown);
        }

        selectedIndex = -1;
        activeWrapper = wrapper;

        if (!results || results.length === 0) {
            dropdown.innerHTML = '<div class="search-dropdown__empty">No results found for &ldquo;' +
                escapeHtml(query) + '&rdquo;</div>';
            dropdown.style.display = 'block';
            return;
        }

        // Group by type
        const groups = {};
        var typeOrder = ['Team', 'Handler', 'Competition'];
        results.forEach(function (r) {
            if (!groups[r.type]) groups[r.type] = [];
            groups[r.type].push(r);
        });

        let html = '';
        typeOrder.forEach(function (type) {
            const items = groups[type];
            if (!items) return;

            const label = type === 'Team' ? 'Teams' : type === 'Handler' ? 'Handlers' : 'Competitions';
            html += '<div class="search-dropdown__group">';
            html += '<div class="search-dropdown__group-title">' + label + '</div>';

            items.forEach(function (item) {
                const href = getResultUrl(item);
                html += '<a href="' + escapeHtml(href) + '" class="search-dropdown__item">';
                html += '<span class="search-dropdown__item-name">' + escapeHtml(item.displayName) + '</span>';
                if (item.subtitle) {
                    html += '<span class="search-dropdown__item-detail">' + escapeHtml(item.subtitle) + '</span>';
                }
                html += '</a>';
            });

            html += '</div>';
        });

        dropdown.innerHTML = html;
        dropdown.style.display = 'block';
    }

    function getResultUrl(item) {
        switch (item.type) {
            case 'Team':
                return '/teams/' + encodeURIComponent(item.slug);
            case 'Handler':
                return '/rankings?search=' + encodeURIComponent(item.displayName);
            case 'Competition':
                return '/competitions';
            default:
                return '/';
        }
    }

    function escapeHtml(str) {
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
