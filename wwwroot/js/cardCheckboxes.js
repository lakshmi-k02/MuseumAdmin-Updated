// Blazor-compatible checkbox initialization
window.cardCheckboxes = {
    // Initialize checkboxes on cards
    initialize: function (dotNetHelper) {
        console.log("Initializing checkboxes...");
        const targets = new Set();

        // For each .art-card, prefer a nested .card if present, otherwise the .art-card itself
        document.querySelectorAll('.art-card').forEach((art) => {
            const inner = art.querySelector('.card');
            targets.add(inner || art);
        });

        // Also include top-level standalone .card elements that are not inside an .art-card
        document.querySelectorAll('.card').forEach((c) => {
            if (!c.closest('.art-card')) targets.add(c);
        });

        const targetArray = Array.from(targets);
        console.log("Checkbox init: targets found:", targetArray.length);

        targetArray.forEach((card, i) => {
            if (!card || card.querySelector('.card-select')) return;

            const wrap = document.createElement('div');
            wrap.className = 'card-select-wrapper';

            const input = document.createElement('input');
            input.type = 'checkbox';
            input.className = 'card-select';
            input.dataset.index = String(i);

            // Prevent card click when interacting with the checkbox
            input.addEventListener('click', (ev) => ev.stopPropagation());
            
            // Call Blazor when checkbox changes
            input.addEventListener('change', function() {
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnCheckboxChanged', this.checked);
                }
                updateBulkUI(dotNetHelper);
            });

            wrap.appendChild(input);
            card.insertBefore(wrap, card.firstChild);
        });
        
        updateBulkUI(dotNetHelper);
    },

    // Update bulk UI
    updateBulkUI: function (dotNetHelper) {
        const bulkActions = document.getElementById('bulkActions');
        if (!bulkActions) return;

        const bulkCount = bulkActions.querySelector('.count');
        const checked = document.querySelectorAll('.card-select:checked');
        const n = checked.length;

        if (n > 0) {
            if (bulkCount) bulkCount.textContent = n + ' selected';
            bulkActions.style.display = 'flex';
            bulkActions.setAttribute('aria-hidden', 'false');
            
            // Update Blazor state
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('UpdateSelectedCount', n);
            }
        } else {
            bulkActions.style.display = 'none';
            bulkActions.setAttribute('aria-hidden', 'true');
            
            // Update Blazor state
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync('UpdateSelectedCount', 0);
            }
        }
    },

    // Get selected card indices
    getSelectedIndices: function () {
        const checked = document.querySelectorAll('.card-select:checked');
        return Array.from(checked).map(cb => parseInt(cb.dataset.index));
    },

    // Clear all selections
    clearSelections: function () {
        document.querySelectorAll('.card-select:checked').forEach(cb => {
            cb.checked = false;
        });
    }
};

// Helper function for bulk UI updates
function updateBulkUI(dotNetHelper) {
    window.cardCheckboxes.updateBulkUI(dotNetHelper);
}

