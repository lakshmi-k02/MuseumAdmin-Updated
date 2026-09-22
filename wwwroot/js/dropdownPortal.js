import { createPopper } from '/libs/popper/index.js';

window._dropdownPortal = window._dropdownPortal || {
    instances: {}
};

// shared listeners (added once)
if (!window._dropdownPortal._listenersAdded) {
    window._dropdownPortal._listenersAdded = true;
    const onGlobalUpdate = () => {
        for (const k in window._dropdownPortal.instances) {
            const rec = window._dropdownPortal.instances[k];
            try { rec.popper.update(); } catch (e) { }
        }
    };
    const onKey = (ev) => {
        if (ev.key === 'Escape' || ev.key === 'Esc') {
            for (const k in window._dropdownPortal.instances) {
                try { hide(k); } catch (e) { }
            }
        }
    };
    window.addEventListener('resize', onGlobalUpdate, { passive: true });
    window.addEventListener('scroll', onGlobalUpdate, { passive: true });
    window.addEventListener('keydown', onKey);
}
export function show(triggerEl, dropdownEl, id = null) {
    if (!triggerEl || !dropdownEl) return;
    id = id || (dropdownEl.id || Math.random().toString(36).slice(2));

    // Move dropdown to body to avoid clipping/overflow issues
    if (!dropdownEl.__originalParent) {
        dropdownEl.__originalParent = dropdownEl.parentElement;
    }
    // keep visually hidden and positioned offscreen until Popper positions it to avoid flash
    dropdownEl.style.opacity = '0';
    dropdownEl.style.pointerEvents = 'none';
    dropdownEl.style.position = 'absolute';
    dropdownEl.style.zIndex = 3000;
    dropdownEl.style.display = 'block';

    // Disable CSS animation/transition that might conflict with Popper's transform
    dropdownEl.style.animation = 'none';

    // place offscreen using left/top so the browser won't briefly paint at 0,0
    dropdownEl.style.left = '-9999px';
    dropdownEl.style.top = '-9999px';
    dropdownEl.style.right = 'auto';
    dropdownEl.style.bottom = 'auto';
    dropdownEl.style.visibility = 'hidden';
    document.body.appendChild(dropdownEl);

    // match trigger width for consistent visual alignment
    try {
        const tw = triggerEl.getBoundingClientRect().width;
        dropdownEl.style.minWidth = `${Math.max(280, Math.round(tw))}px`;
    } catch (e) { }

    // Create Popper instance
    const popperInstance = createPopper(triggerEl, dropdownEl, {
        placement: 'bottom-start',
        modifiers: [
            { name: 'offset', options: { offset: [0, 8] } },
            { name: 'preventOverflow', options: { padding: 8 } },
            { name: 'flip', options: { fallbackPlacements: ['top-start', 'bottom-end', 'top-end'] } }
        ]
    });

    // Force an initial update, then reveal once Popper has placed the element.
    popperInstance.forceUpdate();
    // Wait for Popper to place the element by sampling its bounding rect for a few frames.
    const maxFrames = 8;
    let frame = 0;
    const revealIfPlaced = () => {
        frame++;
        const rect = dropdownEl.getBoundingClientRect();
        // Check if moved from -9999px
        const placed = rect.left > -5000 && rect.top > -5000 && rect.width > 0 && rect.height >= 0;

        if (placed || frame >= maxFrames) {
            // make element visible at its computed position, then fade in
            dropdownEl.style.visibility = '';
            // DO NOT clear left/top here; Popper sets them (or transform) inline. 
            // Clearing them reverts to CSS which might be invalid for body-appended elements.

            dropdownEl.style.transition = 'opacity 160ms ease';
            dropdownEl.style.opacity = '1';
            dropdownEl.style.pointerEvents = 'auto';
            return;
        }
        requestAnimationFrame(revealIfPlaced);
    };
    requestAnimationFrame(revealIfPlaced);

    // store
    window._dropdownPortal.instances[id] = { popper: popperInstance, el: dropdownEl };
}

export function hide(idOrEl) {
    let record = null;
    if (typeof idOrEl === 'string') {
        record = window._dropdownPortal.instances[idOrEl];
    } else if (idOrEl && idOrEl instanceof Element) {
        // find by element
        for (const k in window._dropdownPortal.instances) {
            if (window._dropdownPortal.instances[k].el === idOrEl) {
                record = window._dropdownPortal.instances[k];
                idOrEl = k;
                break;
            }
        }
    }

    if (!record) return;

    const el = record.el;
    // fade out then destroy+move back to original parent
    try {
        el.style.transition = 'opacity 140ms ease';
        el.style.opacity = '0';
        el.style.pointerEvents = 'none';
    } catch (e) { }

    // wait for fade to finish (fallback timeout)
    const cleanup = () => {
        try { record.popper.destroy(); } catch (e) { }
        if (el.__originalParent) {
            el.__originalParent.appendChild(el);
            el.style.position = '';
            el.style.zIndex = '';
            el.style.display = 'none';
            el.style.visibility = '';
            el.style.left = '';
            el.style.top = '';
            el.style.transition = '';
            el.style.animation = ''; // restore animation
        }
        delete window._dropdownPortal.instances[idOrEl];
    };

    // try to listen for transitionend, but always fallback after 220ms
    let done = false;
    const onEnd = (ev) => {
        if (ev && ev.target !== el) return;
        if (done) return; done = true;
        el.removeEventListener('transitionend', onEnd);
        cleanup();
    };
    el.addEventListener('transitionend', onEnd);
    setTimeout(() => { if (!done) onEnd(); }, 220);
}

export function update(id) {
    const rec = window._dropdownPortal.instances[id];
    if (rec && rec.popper) rec.popper.update();
}
