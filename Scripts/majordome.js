var selectedVehicle;

function getActiveMajordomeStock() {
    var fromVehicle = selectedVehicle && selectedVehicle.Stock ? selectedVehicle.Stock : "";
    fromVehicle = (fromVehicle || "").toString().trim();
    if (fromVehicle) return fromVehicle;

    if (typeof window !== "undefined" && window.majordomeSelectedStock) {
        return window.majordomeSelectedStock.toString().trim();
    }

    return "";
}

function toInventoryImageUrl(source) {
    var raw = (source || "").toString().trim();
    if (!raw) return "";

    if (/^https?:\/\//i.test(raw)) {
        return raw;
    }

    if (/^\/?InventoryImages\/Get\?path=/i.test(raw)) {
        return raw.charAt(0) === "/" ? raw : "/" + raw;
    }

    var normalized = raw.replace(/\\/g, "/").replace(/^\/+/, "");
    normalized = normalized.replace(/^GTXImages\/Inventory\//i, "");
    normalized = normalized.replace(/^Pictures\//i, "");

    return "/InventoryImages/Get?path=" + encodeURIComponent(normalized);
}

function appendCacheBust(url, token) {
    if (!url) return "";
    var separator = url.indexOf("?") >= 0 ? "&" : "?";
    return url + separator + "v=" + encodeURIComponent(token);
}

function decodeUriComponentSafe(value) {
    try {
        return decodeURIComponent(value);
    } catch (e) {
        return value;
    }
}

function getMajordomeFileNameOnly(source) {
    var raw = (source || "").toString().trim();
    if (!raw) return "";

    var pathMatch = raw.match(/[?&]path=([^&]+)/i);
    if (pathMatch && pathMatch[1]) {
        raw = decodeUriComponentSafe(pathMatch[1]);
    }

    raw = raw.split("#")[0];
    raw = raw.split("?")[0];
    raw = raw.replace(/\\/g, "/").replace(/\/+$/, "");

    var parts = raw.split("/");
    return parts.length ? parts[parts.length - 1] : raw;
}

function escapeHtml(value) {
    return (value || "").toString()
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function applyMajordomePhotoCardOrientation($image) {
    var imageEl = $image && $image.length ? $image.get(0) : null;
    if (!imageEl) return;

    var $card = $image.closest(".majordome-photo-card");
    if (!$card.length) return;

    var setOrientation = function () {
        var naturalWidth = imageEl.naturalWidth || 0;
        var naturalHeight = imageEl.naturalHeight || 0;

        $card.removeClass("is-landscape is-portrait");
        if (!naturalWidth || !naturalHeight) return;

        if (naturalWidth >= naturalHeight) {
            $card.addClass("is-landscape");
        } else {
            $card.addClass("is-portrait");
        }
    };

    $image.off("load.majordomeOrientation").on("load.majordomeOrientation", setOrientation);
    if (imageEl.complete) {
        setOrientation();
    }
}

class StyleParser {
    constructor(styleString) {
        this.styles = {};
        if (!styleString) return;

        styleString.split(';').forEach(rule => {
            if (!rule.trim()) return;
            const [prop, val] = rule.split(':').map(s => s.trim());
            if (prop && val) {
                const camelProp = prop.replace(/-([a-z])/g, (_, c) => c.toUpperCase());
                this.styles[camelProp] = val;
            }
        });
    }

    get(prop) {
        return this.styles[prop];
    }

    has(prop) {
        return Object.prototype.hasOwnProperty.call(this.styles, prop);
    }

    toObject() {
        return this.styles;
    }
}

function applyFilterTerm(term) {
    if (typeof window.applyMajordomeInventoryFilter === "function") {
        window.applyMajordomeInventoryFilter(term);
    }
}

function saveDetails(model) {
    fetch('/Majordome/SaveDetails', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(model)
    })
        .then(response => { 
            if (!response.ok) throw new Error("Server error");
            return response.json();
        })
        .then(data => {
            console.log("✅ Server response:", data);;
        })
        .catch(error => {
            console.log("❌ Error submitting:", error);
        });
}

function loadGallery(vehicle) {
    var container = $("#sortable-gallery");
    container.empty();
    var i = 0;
    var cacheToken = Date.now();
    vehicle.Images.forEach(function (img) {
        var source = (img.Source || "").toString();
        var showImageEdit = "";
        var imageIcon = "bi bi-image";

        if (source.includes("-O")) {
            showImageEdit = "visually-hidden";
        }

        if (img.Overlay !== null) {
            imageIcon = "bi bi-image-fill";
        }

        var imagePath = appendCacheBust(toInventoryImageUrl(source), cacheToken);
        var fileNameOnly = getMajordomeFileNameOnly(source) || "image";
        var safeFileNameOnly = escapeHtml(fileNameOnly);

        var item = `
        <li id="${img.Id}" class="majordome-photo-card" data-filename="${source}">
            <a href="${imagePath}" class="majordome-photo-link" data-lightbox="gallery" title="${safeFileNameOnly}">
                <div class="majordome-photo-media">
                    <img class="majordome-photo-image" src="${imagePath}" alt="${safeFileNameOnly}" title="${safeFileNameOnly}" />
                </div>
            </a>
            <div class="majordome-photo-footer">
                <div class="majordome-photo-title" title="${safeFileNameOnly}">${safeFileNameOnly}</div>
                <div class="majordome-photo-actions">
                    <button type="button" id="${img.Id}" class="delete-image bi bi-trash btn btn-light shadow-sm" data-filename="${source}" title="Delete image"></button>
                    <button type="button" id="${img.Id}" class="overlay-image ${imageIcon} btn btn-light shadow-sm ${showImageEdit}" data-filename="${source}" title="Add overlay"></button>
                    <button type="button" id="${img.Id}" class="rotate-image-ccw bi bi-arrow-counterclockwise btn btn-light shadow-sm ${showImageEdit}" data-filename="${source}" data-degrees="-90" title="Rotate image left"></button>
                    <button type="button" id="${img.Id}" class="rotate-image bi bi-arrow-clockwise btn btn-light shadow-sm ${showImageEdit}" data-filename="${source}" data-degrees="90" title="Rotate image right"></button>
                    <button type="button" class="move-to-top bi bi-front btn btn-light shadow-sm" title="Make it default image"></button>
                </div>
            </div>
        </li>
        `;

        var $item = $(item);
        container.append($item);
        applyMajordomePhotoCardOrientation($item.find(".majordome-photo-image"));
        i++;
    });

    updateGalleryDisplay();
}

function updateGalleryDisplay() {
    $("#sortable-gallery li").each(function (index) {
        const $li = $(this);
        const $btn = $li.find(".move-to-top");
        const source = ($li.attr("data-filename") || "").toString();
        const fileNameOnly = getMajordomeFileNameOnly(source) || "image";
        const orderedTitle = "#" + (index + 1) + " - " + fileNameOnly;

        $li.find(".majordome-photo-title").text(orderedTitle).attr("title", orderedTitle);

        if (index === 0) {
            $btn.addClass("d-none").hide();
            $li.addClass("gradient");
        }
        else {
            $btn.removeClass("d-none").show();
            $li.removeClass("gradient");
        }
    });
}

function uploadFiles(stock, input) {
    showSpinner($("#inventoryOverlay"));
    const files = input.files;

    const formData = new FormData();
    for (let i = 0; i < files.length; i++) {
        formData.append("files", files[i]);
    }
    formData.append("stock", stock);

    upload(formData, stock);
}

function uploadDroppedFiles(stock, files) {
    showSpinner($("#inventoryOverlay"));
    const formData = new FormData();
    files.forEach(f => formData.append("files", f, f.name));
    formData.append("stock", stock);

    upload(formData, stock);
}

function upload(formData, stock) {
    fetch("/Majordome/Upload", {
        method: "POST",
        body: formData,
        headers: {
            "Cache-Control": "no-cache"
        }
    })
    .then(response => {
        if (response.ok) {
            fetch('/Majordome/GetUpdatedItems')
                .then(res => res.json())
                .then(data => {
                    const vehicle = data.find(v => v.Stock === stock);
                    loadGallery(vehicle);
                    updateRow(data);
                });
        } else {
            alert("Upload failed.");
        }
    })
    .catch(error => {
        alert(error);
    });
}

function uploadInventory(input) {
    showSpinner($("#inventoryOverlay"));
    const formData = new FormData();
    formData.append("dataCsv", input.files[0]);

    fetch("/Majordome/ReplaceHeaderAndConvertToXml", {
        method: "POST",
        body: formData,
        headers: {
            "Cache-Control": "no-cache"
        }
    })
    .then(response => {
        hideSpinner($("#inventoryOverlay"));
        window.location.href = "/Home";
    })
    .catch(error => {
        alert(error);
    });
}

function restoreBackUpInventory() {
    showSpinner($("#inventoryOverlay"));
    fetch("/Majordome/RestoreBackUpInventory", { method: "POST" })
    .then(response => {
        if (response.ok) {
            hideSpinner($("#inventoryOverlay"));
            window.location.href = "/Home";
        } else {
            alert("Restore backup failed.");
        }
    })
    .catch(error => {
        alert(error);
    });
}

function setDetails(stock) {
    fetch('/Majordome/SetDetails', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ stock: stock })
    })
    .then(data => {
    });
}

function setQrCode(vehicle) {
    var qrText = `https://usedcarscincinnati.com/Inventory/Details?stock=${vehicle.Stock}&QR=${encodeURIComponent(vehicle.VIN)}`;
    var qrUrl = "/Majordome/Qr?text=" + encodeURIComponent(qrText);
    $("#qrImg").attr("src", qrUrl);
    $("#qrText").html(`<div>${vehicle.Year} ${vehicle.Make} ${vehicle.Model} Stock# ${vehicle.Stock}</div>`);
    $("#QR-code-tab").removeClass("d-none");
}

function reStoryAll() {
    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    fetch('/Majordome/ReStoryAll', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' }
    })
    .then(response => {
        if (response.ok) {
            fetch('/Majordome/GetUpdatedItems')
                .then(res => res.json())
                .then(data => {
                    updateRow(data);
                });
            alert(`Restory is done`);
        } else {
            alert("Restory failed.");
        }
    })
    .catch(error => {
        alert(error);
    })
    .finally(() => {
        if (typeof hideSpinner === 'function') {
            hideSpinner($overlay);
        }
    });
}

async function decodeAll() {
    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    try {
        const decodeResponse = await fetch('/Majordome/DecodeAll', {
            method: 'POST'
        });

        if (!decodeResponse.ok) {
            throw new Error(`DecodeAll failed: ${decodeResponse.status} ${decodeResponse.statusText}`);
        }

        // Reuse the shared helper
        const data = await getUpdatedItems();
        updateRow(data);

        alert('Decoding is done');
    }
    catch (error) {
        console.error('Error in decodeAll:', error);
        alert('Decoding failed while getting updated items.');
    }
    finally {
        if (typeof hideSpinner === 'function') {
            hideSpinner($overlay);
        }
    }
}

async function decodeDataOne(vin) {
    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    try {
        const decodeResponse = await fetch('/Majordome/DecodeDataOne', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ vin })
        });

        if (!decodeResponse.ok) {
            throw new Error(`DecodeDataOne failed: ${decodeResponse.status} ${decodeResponse.statusText}`);
        }

        const data = await getUpdatedItems(); // reused helper
        updateRow(data);
    }

    catch (error) {
        console.error('Error in decodeDataOne:', error);
        alert('Decoding failed while getting updated items.');
    }

    finally {
        if (typeof hideSpinner === 'function') {
            hideSpinner($overlay);
        }
    }
}

async function deleteDataOne(stock) {
    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    try {
        const res = await fetch(`${root}Majordome/DeleteDataOne`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ stock })
        });

        if (!res.ok) {
            const errorText = await res.text();
            console.error('DeleteDataOne error body:', errorText);
            throw new Error(`DeleteDataOne failed: ${res.status} ${res.statusText}`);
        }

        const response = await res.json();

        if (!response.success) {
            throw new Error(response.message || 'Delete failed.');
        }

        const data = await getUpdatedItems();
        updateRow(data);
    }

    catch (err) {
        console.error('Error in deleteDataOne:', err);
        alert(err.message || 'Delete failed.');
    }

    finally {
        if (typeof hideSpinner === 'function') {
            hideSpinner($overlay);
        }
    }
}

function deleteImages(stock) {
    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    $.post(`${root}Majordome/DeleteImages`, { stock })
        .done(function (response) {
            if (response && response.success) {

                getUpdatedItems()
                    .then(data => {
                        const vehicle = data.find(v => v.Stock === stock);

                        if (vehicle) {
                            loadGallery(vehicle);
                        } else {
                            console.warn(`Vehicle with stock ${stock} not found in updated items.`);
                        }

                        updateRow(data);
                    })
                    .catch(err => {
                        console.error('Error refreshing updated items after deleteImages:', err);
                        alert('Images deleted, but failed to refresh items.');
                    });
            } else {
                alert((response && response.message) || 'Failed to delete images.');
            }
        })

        .fail(function (xhr, status, error) {
            console.error('DeleteImages failed:', status, error, xhr.responseText);
            alert('Failed to delete images on the server.');
        })

        .always(function () {
            if (typeof hideSpinner === 'function') {
                hideSpinner($overlay);
            }
        });
}

function deleteImage(id, file, object) {
    const stock = getActiveMajordomeStock();
    if (!stock) {
        alert("Please select a vehicle first.");
        return;
    }

    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    const $item = $(object).closest('li');

    $.post(`${root}Majordome/DeleteImage`, { id, file, stock })
        .done(function (response) {
            if (response && response.success) {
                // Remove the image from the DOM first
                $item.fadeOut(300, function () { $item.remove(); });

                getUpdatedItems()
                    .then(data => {
                        const vehicle = data.find(v => v.Stock === stock);

                        if (vehicle) {
                            loadGallery(vehicle);
                        } else {
                            console.warn(`Vehicle with stock ${stock} not found in updated items.`);
                        }

                        updateRow(data);
                        $("#close").click();
                    })
                    .catch(err => {
                        console.error('Error refreshing updated items after deleteImage:', err);
                        alert('Image deleted, but failed to refresh items.');
                    });
            } else {
                alert((response && response.message) || 'Failed to delete image.');
            }
        })
        .fail(function (xhr, status, error) {
            console.error('DeleteImage failed:', status, error, xhr.responseText);
            alert('Failed to delete image on the server.');
        })
        .always(function () {
            if (typeof hideSpinner === 'function') {
                hideSpinner($overlay);
            }
        });
}

function rotateImage(file, degrees) {
    const stock = getActiveMajordomeStock();
    if (!stock) {
        alert("Please select a vehicle first.");
        return;
    }

    var rotationDegrees = parseInt(degrees, 10);
    if (rotationDegrees !== -90 && rotationDegrees !== 90) {
        rotationDegrees = 90;
    }

    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);
    window.majordomeSelectedStock = stock;
    window.majordomeForceActiveTab = "gallery-tab";

    $.post(`${root}Majordome/RotateImage`, { file, stock, degrees: rotationDegrees })
        .done(function (response) {
            if (response && response.success) {
                getUpdatedItems()
                    .then(data => {
                        const targetStock = String(stock).trim().toUpperCase();
                        const vehicle = (Array.isArray(data) ? data : []).find(v => String(v && v.Stock || "").trim().toUpperCase() === targetStock);

                        if (vehicle) {
                            loadGallery(vehicle);
                        } else {
                            console.warn(`Vehicle with stock ${stock} not found in updated items.`);
                        }

                        updateRow(data);
                        $("#gallery-tab").tab("show");
                    })
                    .catch(err => {
                        console.error('Error refreshing updated items after rotateImage:', err);
                        alert('Image rotated, but failed to refresh items.');
                        if (typeof hideSpinner === 'function') {
                            hideSpinner($overlay);
                        }
                    });
            } else {
                alert((response && response.message) || 'Failed to rotate image.');
                if (typeof hideSpinner === 'function') {
                    hideSpinner($overlay);
                }
            }
        })
        .fail(function (xhr, status, error) {
            console.error('RotateImage failed:', status, error, xhr.responseText);
            alert('Failed to rotate image on the server.');
            if (typeof hideSpinner === 'function') {
                hideSpinner($overlay);
            }
        });
}

function createStory(stock) {
    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    $.post(`${root}Majordome/CreateStory`, { stock })
        .done(function (response) {
            if (response && response.success) {

                quill.setContents([]);
                quill.clipboard.dangerouslyPasteHTML(0, response?.Story || "", 'api');

                $("#title").val(response.Title || "");

                getUpdatedItems()
                    .then(data => {
                        updateRow(data);
                    })
                    .catch(err => {
                        console.error('Error refreshing updated items after createStory:', err);
                        alert('Story created, but failed to refresh items.');
                    });
            } else {
                alert((response && response.message) || 'Failed to create story.');
            }
        })

        .fail(function (xhr, status, error) {
            console.error('CreateStory failed:', status, error, xhr.responseText);
            alert('Failed to create story on the server.');
        })

        .always(function () {
            if (typeof hideSpinner === 'function') {
                hideSpinner($overlay);
            }
        });
}

function deleteStory(stock) {
    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    $.post(`${root}Majordome/DeleteStory`, { stock })
        .done(function (response) {
            if (response && response.success) {
                quill.clipboard.dangerouslyPasteHTML(0, "");
                $("#title").val("");

                getUpdatedItems()
                    .then(data => {
                        updateRow(data);
                    })
                    .catch(err => {
                        console.error('Error refreshing updated items after deleteStory:', err);
                        alert('Story deleted, but failed to refresh items.');
                    });
            } else {
                alert((response && response.message) || 'Failed to delete story.');
            }
        })
        .fail(function (xhr, status, error) {
            console.error('DeleteStory failed:', status, error, xhr.responseText);
            alert('Failed to delete story on the server.');
        })
        .always(function () {
            if (typeof hideSpinner === 'function') {
                hideSpinner($overlay);
            }
        });
}

function saveOrder(sorted) {
    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    $.ajax({
        url: `${root}Majordome/SaveOrder`,
        type: "POST",
        data: { sorted },
        traditional: true
    })
        .done(function (response) {
            if (response && response.success) {
                getUpdatedItems()
                    .then(data => {
                        updateRow(data);
                    })
                    .catch(err => {
                        console.error('Error refreshing updated items after saveOrder:', err);
                        alert('Order saved, but failed to refresh items.');
                    });

            } else {
                alert((response && response.message) || 'Failed to save order.');
            }
        })
        .fail(function (xhr, status, error) {
            console.error('SaveOrder failed:', status, error, xhr.responseText);
            alert('Failed to save order on the server.');
        })
        .always(function () {
            if (typeof hideSpinner === 'function') {
                hideSpinner($overlay);
            }
        });
}

async function getUpdatedItems() {
    const res = await fetch('/Majordome/GetUpdatedItems', {
        method: 'GET'
    });

    if (!res.ok) {
        const errorText = await res.text();
        console.error('GetUpdatedItems error body:', errorText);
        throw new Error(`GetUpdatedItems failed: ${res.status} ${res.statusText}`);
    }

    return res.json();
}

function updateRow(data) {
    if (typeof window.onMajordomeInventoryDataUpdated === "function") {
        window.onMajordomeInventoryDataUpdated(data);
        return;
    }

    hideSpinner($("#inventoryOverlay"));
}

function wearOverlay(json) {
    try {
        const data = JSON.parse(json);
        const overlay = $("#overlay");

        overlay.empty(); // Clear previous content
        overlay.attr("style", data.overlay.style); // Apply overlay style

        data.overlay.children.forEach(function (child) {
            const element = $(`<${child.tag}/>`, {
                class: "overlay-text",
                text: child.text,
                style: child.style
            });
            overlay.append(element);
        });

    } catch (e) {
        console.error('Invalid overlay JSON:', e);
    }
}

function setControls(json) {
    const data = JSON.parse(json);

    let styleString = data.overlay.style;
    let parser = new StyleParser(styleString);

    const $backgroundColor = parser.get("backgroundColor");
    $("#backgroundColor").val($backgroundColor).trigger("change");

    styleString = data.overlay.children[0].style;
    parser = new StyleParser(styleString);

    const $fontSize = parser.get("fontSize");
    const $fontWeight = parser.get("fontWeight");
    const $fontStyle = parser.get("fontStyle");
    const $color = parser.get("color");

    $("#textColor").val($color).trigger("change");
    $("#fontSize").val($fontSize).trigger("change");

    // Apply selected style
    $("#fontType").val("normal");

    if ($fontWeight === "bold") {
        $("#fontType").val("bold");
    }

    if ($fontStyle === "italic") {
        $("#fontType").val("italic");
    }

    if ($fontStyle === "italic" && $fontWeight === "bold") {
        $("#fontType").val("bolditalic");
    }
}

function deleteOverlayData() {
    showSpinner($("#inventoryOverlay"));
    const overlay = $("#overlay");
    $.post(`${root}Majordome/DeleteOverlay`, { id })
        .done(function (response) {
            if (response.success) {
                fetch('/Majordome/GetUpdatedItems')
                    .then(res => res.json())
                    .then(data => {
                        const vehicle = data.find(v => v.Stock === stock);
                        loadGallery(vehicle);
                        updateRow(data);
                        hideSpinner($("#inventoryOverlay"));
                        $("#close").click();
                    });
            }
        })
}

function saveOverlayData() {
    showSpinner($("#inventoryOverlay"));
    const overlay = $("#overlay");
    const overlayStyle = `background-color: ${$("#backgroundColor").val()}`;

    const children = [];

    overlay.children().each(function () {
        const bold = $("#fontType").val().includes("bold") ? "bold" : "normal"
        const italic = $("#fontType").val().includes("italic") ? "italic" : "normal"

        const tag = this.tagName.toLowerCase();
        const text = $(this).text();
        const color = `color:${$("#textColor").val()};`;
        const fontSize = `font-size:${$("#fontSize").val()};`;
        const fontWeight = `font-weight:${bold};`;
        const fontStyle = `font-style:${italic};`;
        const style = `${color}${fontSize}${fontWeight}${fontStyle}`;

        children.push({
            tag: tag,
            text: text,
            style: style
        });
    });

    const json = {
        overlay: {
            style: overlayStyle,
            children: children
        }
    };

    $.post(`${root}Majordome/SaveOverlay`, { id, overlay: JSON.stringify(json), stock, imagePath: imagePath })
        .done(function (response) {
            if (response.success) {
                fetch('/Majordome/GetUpdatedItems')
                    .then(res => res.json())
                    .then(data => {
                        const vehicle = data.find(v => v.Stock === stock);
                        loadGallery(vehicle);
                        updateRow(data);
                        hideSpinner($("#inventoryOverlay"));
                        $("#close").click();
                    });
            }
        })
}

