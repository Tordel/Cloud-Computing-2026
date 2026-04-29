// CloudNote — site.js

// Auto-dismiss flash messages after 4 seconds
document.addEventListener('DOMContentLoaded', () => {
    const flash = document.querySelector('.flash');
    if (flash) {
        setTimeout(() => {
            flash.style.transition = 'opacity .5s';
            flash.style.opacity = '0';
            setTimeout(() => flash.remove(), 500);
        }, 4000);
    }

    // Close modal on backdrop click
    document.querySelectorAll('.modal').forEach(modal => {
        modal.addEventListener('click', e => {
            if (e.target === modal) modal.style.display = 'none';
        });
    });

    // Character counter for note content textarea
    const textarea = document.querySelector('textarea[name="Content"]');
    if (textarea) {
        const counter = document.createElement('small');
        counter.style.cssText = 'display:block;text-align:right;color:#9ca3af;margin-top:-0.8rem;margin-bottom:0.8rem;';
        textarea.insertAdjacentElement('afterend', counter);
        const update = () => { counter.textContent = `${textarea.value.length} chars · AI analysis will run on save`; };
        textarea.addEventListener('input', update);
        update();
    }
});
