window.setCookie = function (name, value, expirationDays) {
    var expires = "";
    if (expirationDays) {
        var date = new Date();
        date.setTime(date.getTime() + (expirationDays * 24 * 60 * 60 * 1000));
        expires = "; expires=" + date.toUTCString();
    }
    document.cookie = name + "=" + value + expires + "; path=/";
};
window.getCookie = function (name) {
    var nameEQ = name + "=";
    var ca = document.cookie.split(';');
    for (var i = 0; i < ca.length; i++) {
        var c = ca[i];
        while (c.charAt(0) === ' ') c = c.substring(1, c.length);
        if (c.indexOf(nameEQ) === 0) return c.substring(nameEQ.length, c.length);
    }
    return null;
};
function goBack() {
    window.history.back();
}

function showInlineMapMulti(zones) {
    var mapContainer = document.getElementById("mapContainerMulti");

    if (!mapContainer) {
        console.error("Map container not found!");
        return;
    }

    // Clear previous map instance
    mapContainer.innerHTML = "";

    if (typeof google === "undefined" || typeof google.maps === "undefined") {
        console.error("Google Maps API is not loaded!");
        return;
    }

    var map = new google.maps.Map(mapContainer, {
        center: { lat: 0, lng: 0 },
        zoom: 10
    });

    var bounds = new google.maps.LatLngBounds();

    zones.forEach(zone => {
        var position = { lat: parseFloat(zone.latitude), lng: parseFloat(zone.longitude) };

        var marker = new google.maps.Marker({
            position: position,
            map: map,
            title: zone.name
        });

        bounds.extend(position);

        var infoWindow = new google.maps.InfoWindow({
            content: `<b>${zone.name}</b>`
        });

        marker.addListener("click", function () {
            infoWindow.open(map, marker);
        });
    });

    if (zones.length > 1) {
        map.fitBounds(bounds);
    } else if (zones.length === 1) {
        map.setCenter(bounds.getCenter());
        map.setZoom(15);
    }

    console.log("Map initialized with all zones together:", zones);
}



function showInlineMap(lat, lng) {
    var mapContainer = document.getElementById("mapContainer");

    if (!mapContainer) {
        console.error("Map container not found!");
        return;
    }

    // Clear previous map instance
    mapContainer.innerHTML = "";

    var map = new google.maps.Map(mapContainer, {
        center: { lat: parseFloat(lat), lng: parseFloat(lng) },
        zoom: 15
    });

    new google.maps.Marker({
        position: { lat: parseFloat(lat), lng: parseFloat(lng) },
        map: map,
        title: "Museum Zone Location"
    });
}

function clearDropdown(dropdownId) {
    let dropdown = document.getElementById(dropdownId);
    if (dropdown) {
        dropdown.selectedIndex = -1; // Deselect all options
        Array.from(dropdown.options).forEach(option => option.selected = false);
    }
}

window.quillInterop = {
    editors: {},

    initializeQuill: function (elementId, dotNetRef) {
        var quill = new Quill(`#${elementId}`, { theme: 'snow' });
        this.editors[elementId] = { quill: quill, dotNetRef: dotNetRef };

        quill.on('text-change', function () {
            const html = quill.root.innerHTML;
            dotNetRef.invokeMethodAsync('UpdateMessage', elementId, html);
        });
    },

    setEditorContent: function (elementId, content) {
        if (this.editors[elementId]) {
            this.editors[elementId].quill.root.innerHTML = content;
        }
    }
};

function openGoogleMapPopup(latLong) {
    if (!latLong) {
        alert("Invalid location data");
        return;
    }

    const url = `https://www.google.com/maps?q=${latLong}`;
    window.open(url, "GoogleMapPopup", "width=800,height=600");
}


// wwwroot/js/site.js
window.focusOnceOnTouch = (inputId, timeoutMs) => {
    timeoutMs = timeoutMs || 300;
    // use pointerdown so it works for touch but also avoids some double events
    function handler(e) {
        try {
            const el = document.getElementById(inputId);
            if (!el) return;
            // if already focused, stop listening
            if (document.activeElement === el) {
                cleanup();
                return;
            }
            // focus once
            el.focus();
        } catch (err) {
            // ignore
        } finally {
            // remove listener after the short timeout to avoid loops
            setTimeout(cleanup, timeoutMs);
        }
    }
    function cleanup() {
        window.removeEventListener('pointerdown', handler, { passive: true });
        window.removeEventListener('touchstart', handler, { passive: true });
    }
    // add both for maximum compatibility
    window.addEventListener('pointerdown', handler, { passive: true });
    window.addEventListener('touchstart', handler, { passive: true });
};
