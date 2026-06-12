(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const forms = document.querySelectorAll('form[data-dismiss-form]');

        forms.forEach(function (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();

                const message = form.getAttribute('data-dismiss-confirm') || 'Dismiss this booking?';
                if (!window.confirm(message)) {
                    return;
                }

                const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
                const card = form.closest('.rule-card');

                fetch(form.action, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'RequestVerificationToken': tokenInput ? tokenInput.value : ''
                    }
                })
                    .then(function (r) {
                        if (!r.ok) {
                            window.location.reload();
                            return;
                        }
                        removeCard(card);
                    })
                    .catch(function () {
                        window.location.reload();
                    });
            });
        });

        function removeCard(card) {
            if (!card) {
                window.location.reload();
                return;
            }

            const section = card.closest('.booking-section');
            card.remove();

            if (section) {
                const remaining = section.querySelectorAll('.rule-card').length;
                if (remaining === 0) {
                    section.remove();
                } else {
                    const count = section.querySelector('.booking-section__count');
                    if (count) {
                        count.textContent = remaining.toString();
                    }
                }
            }

            if (document.querySelectorAll('.rule-card').length === 0) {
                window.location.reload();
            }
        }
    });
}());
