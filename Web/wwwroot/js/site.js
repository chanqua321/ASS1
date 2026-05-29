// EduAI — Site JS enhancements
(function () {
    'use strict';

    // Auto-dismiss success alerts after 5s
    document.querySelectorAll('.alert-success.alert-dismissible').forEach(function (alert) {
        setTimeout(function () {
            alert.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
            alert.style.opacity = '0';
            alert.style.transform = 'translateY(-8px)';
            setTimeout(function () {
                if (typeof bootstrap !== 'undefined' && bootstrap.Alert) {
                    bootstrap.Alert.getOrCreateInstance(alert).close();
                }
            }, 600);
        }, 5000);
    });

    // Chat input: Enter to send, Shift+Enter for new line
    document.querySelectorAll('#questionInput').forEach(function (ta) {
        // Auto-resize textarea
        ta.addEventListener('input', function () {
            this.style.height = 'auto';
            this.style.height = Math.min(this.scrollHeight, 160) + 'px';
        });
        ta.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                const form = ta.closest('form');
                if (form) form.requestSubmit();
            }
        });
    });

    // Staggered animation for list items
    const observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry, i) {
            if (entry.isIntersecting) {
                setTimeout(function () {
                    entry.target.style.opacity = '1';
                    entry.target.style.transform = 'translateY(0)';
                }, i * 60);
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.1 });

    document.querySelectorAll('.list-group-item, .hero-quick-link, .app-table-wrap tbody tr').forEach(function (el, i) {
        el.style.opacity = '0';
        el.style.transform = 'translateY(12px)';
        el.style.transition = 'opacity 0.35s ease, transform 0.35s ease';
        observer.observe(el);
    });
})();
