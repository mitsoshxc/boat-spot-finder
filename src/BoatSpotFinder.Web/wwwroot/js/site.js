// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
    const init = (root) => {
        const trigger = root.querySelector('[data-user-menu-trigger]');
        const panel = root.querySelector('[data-user-menu-panel]');
        const closer = root.querySelector('[data-user-menu-close]');
        if (!trigger || !panel) { return; }

        const items = () => Array.from(panel.querySelectorAll('[role="menuitem"]'));

        const open = () => {
            root.classList.add('user-menu--open');
            trigger.setAttribute('aria-expanded', 'true');
            document.addEventListener('keydown', onKey);
            document.addEventListener('click', onDocClick, true);
            requestAnimationFrame(() => {
                const first = items()[0];
                if (first) { first.focus({ preventScroll: true }); }
            });
        };

        const close = ({ restoreFocus = true } = {}) => {
            if (!root.classList.contains('user-menu--open')) { return; }
            root.classList.remove('user-menu--open');
            trigger.setAttribute('aria-expanded', 'false');
            document.removeEventListener('keydown', onKey);
            document.removeEventListener('click', onDocClick, true);
            if (restoreFocus) { trigger.focus({ preventScroll: true }); }
        };

        const onKey = (e) => {
            if (e.key === 'Escape') {
                e.preventDefault();
                close();
                return;
            }
            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                e.preventDefault();
                const list = items();
                if (list.length === 0) { return; }
                const current = list.indexOf(document.activeElement);
                const dir = e.key === 'ArrowDown' ? 1 : -1;
                const next = list[(current + dir + list.length) % list.length];
                next.focus({ preventScroll: true });
            }
        };

        const onDocClick = (e) => {
            if (root.contains(e.target)) { return; }
            close({ restoreFocus: false });
        };

        trigger.addEventListener('click', () => {
            const isOpen = root.classList.contains('user-menu--open');
            isOpen ? close() : open();
        });

        if (closer) {
            closer.addEventListener('click', () => close());
        }

        panel.addEventListener('click', (e) => {
            const sheet = panel.querySelector('.user-menu__sheet');
            if (!sheet) { return; }
            if (sheet.contains(e.target)) { return; }
            close({ restoreFocus: false });
        });
    };

    document.querySelectorAll('[data-user-menu]').forEach(init);
})();
