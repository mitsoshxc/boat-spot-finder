document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('marina-search');
    const marinaRows = document.getElementById('marina-rows');

    if (!form || !marinaRows) {
        return;
    }

    const searchUrl = form.dataset.searchUrl;
    const input = form.querySelector('input[name="q"]');
    let debounceTimer = null;
    let activeController = null;

    async function fetchRows(value) {
        if (activeController) {
            activeController.abort();
        }
        activeController = new AbortController();
        marinaRows.setAttribute('aria-busy', 'true');

        try {
            const response = await fetch(
                `${searchUrl}?q=${encodeURIComponent(value)}`,
                { signal: activeController.signal }
            );
            if (response.ok) {
                marinaRows.innerHTML = await response.text();
            }
        } catch (err) {
            if (err.name !== 'AbortError') {
                console.error('Marina search failed:', err);
            }
        } finally {
            marinaRows.removeAttribute('aria-busy');
        }
    }

    input.addEventListener('input', function () {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(function () {
            fetchRows(input.value);
        }, 250);
    });

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        clearTimeout(debounceTimer);
        fetchRows(input.value);
    });
});
