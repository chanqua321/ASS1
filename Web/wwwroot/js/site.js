// Assigment1 — UI enhancements
(function () {
    document.querySelectorAll('.alert-success.alert-dismissible').forEach(function (alert) {
        setTimeout(function () {
            if (typeof bootstrap !== 'undefined' && bootstrap.Alert) {
                bootstrap.Alert.getOrCreateInstance(alert).close();
            }
        }, 6000);
    });

    document.querySelectorAll('#questionInput').forEach(function (ta) {
        ta.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                const form = ta.closest('form');
                if (form) form.requestSubmit();
            }
        });
    });
})();
