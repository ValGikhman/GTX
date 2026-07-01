var selectedVehicle;
var photosInventoryImagesBaseUrl = "https://photos.usedcarscincinnati.com/Images";

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

    if (/^\/?InventoryImages\/Get\?/i.test(raw)) {
        var queryIndex = raw.indexOf("?");
        if (queryIndex >= 0) {
            var query = raw.substring(queryIndex + 1).split("&");
            for (var i = 0; i < query.length; i++) {
                var part = (query[i] || "").split("=");
                if (part.length < 2) continue;

                var key = decodeUriComponentSafe((part[0] || "").replace(/\+/g, " "));
                if (key.toLowerCase() !== "path") continue;

                var pathValue = decodeUriComponentSafe((part.slice(1).join("=") || "").replace(/\+/g, " "));
                raw = pathValue;
                break;
            }
        }
    }

    var normalized = raw.replace(/\\/g, "/").replace(/^\/+/, "");
    normalized = normalized.replace(/^GTXImages\/Inventory\//i, "");
    normalized = normalized.replace(/^Pictures\//i, "");
    normalized = normalized.replace(/^Images\//i, "");
    normalized = normalized.replace(/^\/+/, "").replace(/\/+$/, "");

    if (!normalized) return "";

    var segments = normalized.split("/").filter(function (segment) {
        return !!segment;
    }).map(function (segment) {
        return encodeURIComponent(segment);
    });

    if (!segments.length) return "";
    return photosInventoryImagesBaseUrl + "/" + segments.join("/");
}

function appendCacheBust(url, token) {
    if (!url) return "";
    var separator = url.indexOf("?") >= 0 ? "&" : "?";
    return url + separator + "v=" + encodeURIComponent(token);
}

function appendImageWidth(url, width) {
    return (url || "").toString().trim();
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

var majordomeImageActionInProgress = false;

function normalizeMajordomeStockKey(stock) {
    return (stock || "").toString().trim().toUpperCase();
}

function findMajordomeVehicleByStock(data, stock) {
    var target = normalizeMajordomeStockKey(stock);
    if (!target || !Array.isArray(data)) return null;

    for (var i = 0; i < data.length; i++) {
        var item = data[i];
        if (normalizeMajordomeStockKey(item && item.Stock) === target) {
            return item;
        }
    }

    return null;
}

function setMajordomeImageActionsBusy(isBusy) {
    var busy = !!isBusy;

    $("#sortable-gallery .majordome-photo-actions .btn, #upload, #deleteAll, #saveOverlayFile")
        .prop("disabled", busy)
        .toggleClass("disabled", busy);

    $("#dropzone").toggleClass("pe-none", busy);
}

function beginMajordomeImageAction($overlay) {
    if (majordomeImageActionInProgress) {
        return false;
    }

    majordomeImageActionInProgress = true;
    setMajordomeImageActionsBusy(true);
    showSpinner($overlay);
    return true;
}

function endMajordomeImageAction($overlay) {
    majordomeImageActionInProgress = false;
    setMajordomeImageActionsBusy(false);
    if (typeof hideSpinner === "function") {
        hideSpinner($overlay);
    }
}

function refreshMajordomeAfterImageMutation(stock, options) {
    var settings = options || {};
    var keepGalleryTab = settings.keepGalleryTab !== false;
    var stockKey = (stock || "").toString().trim();

    if (stockKey) {
        window.majordomeSelectedStock = stockKey;
    }

    if (keepGalleryTab) {
        window.majordomeForceActiveTab = "gallery-tab";
    }

    return getUpdatedItems().then(function (data) {
        var vehicle = findMajordomeVehicleByStock(data, stockKey);

        if (vehicle) {
            loadGallery(vehicle);
        } else if (keepGalleryTab) {
            $("#sortable-gallery").empty();
        }

        updateRow(data);

        if (keepGalleryTab) {
            $("#gallery-tab").tab("show");
        }

        return data;
    });
}

function waitForMajordomeImageToLoad($image) {
    return new Promise(function (resolve) {
        var imageEl = $image && $image.length ? $image.get(0) : null;
        if (!imageEl) {
            resolve();
            return;
        }

        var complete = imageEl.complete && imageEl.naturalWidth > 0;
        if (complete) {
            resolve();
            return;
        }

        var done = function () {
            $image.off("load.majordomeRefresh error.majordomeRefresh", done);
            resolve();
        };

        $image.one("load.majordomeRefresh error.majordomeRefresh", done);
    });
}

function refreshMajordomePhotoCardImage($card, file) {
    if (!$card || !$card.length) {
        return Promise.resolve();
    }

    var baseUrl = toInventoryImageUrl(file);
    var freshLinkUrl = appendCacheBust(appendImageWidth(baseUrl, 1600), Date.now());
    var freshThumbUrl = appendCacheBust(appendImageWidth(baseUrl, 640), Date.now());
    var $link = $card.find(".majordome-photo-link");
    var $image = $card.find(".majordome-photo-image");

    if ($link.length) {
        $link.attr("href", freshLinkUrl);
    }

    if (!$image.length) {
        return Promise.resolve();
    }

    $image.attr("src", freshThumbUrl);
    applyMajordomePhotoCardOrientation($image);
    return waitForMajordomeImageToLoad($image);
}

function reorderMajordomeSelectedVehicleImages(sorted, stock) {
    if (!selectedVehicle || !Array.isArray(selectedVehicle.Images) || !Array.isArray(sorted) || sorted.length === 0) {
        return;
    }

    var selectedStock = normalizeMajordomeStockKey(selectedVehicle.Stock);
    var targetStock = normalizeMajordomeStockKey(stock);
    if (targetStock && selectedStock && selectedStock !== targetStock) {
        return;
    }

    var byId = {};
    var noId = [];
    for (var i = 0; i < selectedVehicle.Images.length; i++) {
        var image = selectedVehicle.Images[i];
        var key = image && image.Id != null ? image.Id.toString() : "";
        if (key) {
            byId[key] = image;
        } else {
            noId.push(image);
        }
    }

    var reordered = [];
    for (var j = 0; j < sorted.length; j++) {
        var sortedKey = (sorted[j] || "").toString();
        if (sortedKey && byId[sortedKey]) {
            reordered.push(byId[sortedKey]);
            delete byId[sortedKey];
        }
    }

    var remainingKeys = Object.keys(byId);
    for (var k = 0; k < remainingKeys.length; k++) {
        reordered.push(byId[remainingKeys[k]]);
    }
    for (var m = 0; m < noId.length; m++) {
        reordered.push(noId[m]);
    }

    selectedVehicle.Images = reordered;
    if (reordered.length > 0 && reordered[0] && reordered[0].Source) {
        selectedVehicle.Image = reordered[0].Source;
    }
}

function refreshMajordomeSelectedRowThumbnail(stock) {
    var targetStock = normalizeMajordomeStockKey(stock);
    if (!targetStock) {
        return;
    }

    var $firstCard = $("#sortable-gallery li").first();
    if (!$firstCard.length) {
        return;
    }

    var source = ($firstCard.attr("data-filename") || "").toString().trim();
    if (!source) {
        return;
    }

    var freshThumb = appendCacheBust(appendImageWidth(toInventoryImageUrl(source), 320), Date.now());
    var $row = $("#majordomeInventoryBody .majordome-vehicle-row").filter(function () {
        return normalizeMajordomeStockKey($(this).attr("data-stock")) === targetStock;
    }).first();

    if ($row.length) {
        $row.find(".majordome-row-image").attr("src", freshThumb);
    }
}

function getMajordomeOverlayContext() {
    if (typeof window !== "undefined" && window.majordomeOverlayContext) {
        return window.majordomeOverlayContext;
    }

    return null;
}

function postMajordome(url, data, ajaxOptions) {
    return new Promise(function (resolve, reject) {
        var options = $.extend(
            {
                url: url,
                type: "POST",
                data: data || {}
            },
            ajaxOptions || {}
        );

        $.ajax(options)
            .done(function (response) {
                resolve(response);
            })
            .fail(function (xhr, status, error) {
                var message =
                    (xhr && xhr.responseJSON && xhr.responseJSON.message) ||
                    (xhr && xhr.responseText) ||
                    error ||
                    status ||
                    "Request failed.";
                reject(new Error(message));
            });
    });
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

const MAJORDOME_OVERLAY_DEFAULT_OPACITY = "1";

function normalizeMajordomeOverlayOpacity(value) {
    var raw = (value == null ? "" : value).toString().trim();
    if (!raw) {
        return MAJORDOME_OVERLAY_DEFAULT_OPACITY;
    }

    if (raw.endsWith("%")) {
        var percent = parseFloat(raw.slice(0, -1));
        if (!isNaN(percent)) {
            raw = (percent / 100).toString();
        }
    }

    var opacity = parseFloat(raw);
    if (isNaN(opacity)) {
        return MAJORDOME_OVERLAY_DEFAULT_OPACITY;
    }

    opacity = Math.max(0, Math.min(1, opacity));
    if (opacity === 1) return "1";
    if (opacity === 0.75) return "0.75";
    if (opacity === 0.5) return "0.5";
    if (opacity === 0.25) return "0.25";

    return opacity.toString();
}

function resolveMajordomeCssRgb(color) {
    var raw = (color || "black").toString().trim();
    var match = raw.match(/^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})/i);
    if (match) {
        return {
            r: Math.max(0, Math.min(255, parseInt(match[1], 10))),
            g: Math.max(0, Math.min(255, parseInt(match[2], 10))),
            b: Math.max(0, Math.min(255, parseInt(match[3], 10)))
        };
    }

    var probe = document.createElement("span");
    probe.style.color = raw;
    probe.style.display = "none";
    document.body.appendChild(probe);
    var computed = window.getComputedStyle(probe).color;
    document.body.removeChild(probe);

    match = computed.match(/^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})/i);
    if (!match) {
        return null;
    }

    return {
        r: Math.max(0, Math.min(255, parseInt(match[1], 10))),
        g: Math.max(0, Math.min(255, parseInt(match[2], 10))),
        b: Math.max(0, Math.min(255, parseInt(match[3], 10)))
    };
}

function buildMajordomeOverlayBackground(color, opacity) {
    var normalizedOpacity = normalizeMajordomeOverlayOpacity(opacity);
    var alpha = parseFloat(normalizedOpacity);

    if (alpha >= 1) {
        return color || "black";
    }

    var rgb = resolveMajordomeCssRgb(color);
    if (!rgb) {
        return color || "black";
    }

    return `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, ${normalizedOpacity})`;
}

function applyOverlayBackground() {
    var color = ($("#backgroundColor").val() || "black").toString();
    var opacity = normalizeMajordomeOverlayOpacity($("#backgroundOpacity").val());

    $(".overlay-image-overlay")
        .css("background-color", buildMajordomeOverlayBackground(color, opacity))
        .attr("data-background-color", color)
        .attr("data-background-opacity", opacity);
}

function getOverlayBackgroundSettings(data) {
    var overlay = (data && data.overlay) || {};
    var parser = new StyleParser(overlay.style || "");
    var color = overlay.backgroundColor || parser.get("backgroundColor") || "black";
    var opacity = overlay.backgroundOpacity || parser.get("backgroundOpacity") || parser.get("opacity") || MAJORDOME_OVERLAY_DEFAULT_OPACITY;

    return {
        color: color,
        opacity: normalizeMajordomeOverlayOpacity(opacity)
    };
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
    var images = Array.isArray(vehicle && vehicle.Images) ? vehicle.Images : [];
    var items = [];

    images.forEach(function (img, index) {
        var source = (img.Source || "").toString();
        var showImageEdit = "";
        var imageIcon = "bi bi-image";

        if (source.includes("-O")) {
            showImageEdit = "visually-hidden";
        }

        var baseImagePath = toInventoryImageUrl(source);
        var imageHref = appendImageWidth(baseImagePath, 1600);
        var imageThumb = appendImageWidth(baseImagePath, 640);
        var fileNameOnly = getMajordomeFileNameOnly(source) || "image";
        var safeId = escapeHtml(img.Id);
        var safeSource = escapeHtml(source);
        var safeImageHref = escapeHtml(imageHref);
        var safeImageThumb = escapeHtml(imageThumb);
        var safeFileNameOnly = escapeHtml(fileNameOnly);
        var loadingMode = index < 4 ? "eager" : "lazy";
        var fetchPriority = index === 0 ? "high" : "low";

        items.push(`
        <li id="${safeId}" class="majordome-photo-card" data-filename="${safeSource}">
            <a href="${safeImageHref}" class="majordome-photo-link" data-lightbox="gallery" title="${safeFileNameOnly}">
                <div class="majordome-photo-media">
                    <img class="majordome-photo-image" src="${safeImageThumb}" alt="${safeFileNameOnly}" title="${safeFileNameOnly}" loading="${loadingMode}" decoding="async" fetchpriority="${fetchPriority}" />
                </div>
            </a>
            <div class="majordome-photo-footer">
                <div class="majordome-photo-title" title="${safeFileNameOnly}">${safeFileNameOnly}</div>
                <div class="majordome-photo-actions">
                    <button type="button" id="${safeId}" class="delete-image bi bi-trash btn btn-light shadow-sm" data-filename="${safeSource}" title="Delete image"></button>
                    <button type="button" id="${safeId}" class="overlay-image ${imageIcon} btn btn-light shadow-sm ${showImageEdit}" data-filename="${safeSource}" title="Create overlay file"></button>
                    <button type="button" id="${safeId}" class="rotate-image-ccw bi bi-arrow-counterclockwise btn btn-light shadow-sm ${showImageEdit}" data-filename="${safeSource}" data-degrees="-90" title="Rotate image left"></button>
                    <button type="button" id="${safeId}" class="rotate-image bi bi-arrow-clockwise btn btn-light shadow-sm ${showImageEdit}" data-filename="${safeSource}" data-degrees="90" title="Rotate image right"></button>
                    <button type="button" class="move-to-top bi bi-front btn btn-light shadow-sm" title="Make it default image"></button>
                </div>
            </div>
        </li>
        `);
    });

    container.html(items.join(""));
    container.find(".majordome-photo-image").each(function () {
        applyMajordomePhotoCardOrientation($(this));
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

function applyUploadedImagesToMajordomeState(stock, images, options) {
    var settings = options || {};
    var targetStock = normalizeMajordomeStockKey(stock);
    if (!targetStock || !Array.isArray(images)) {
        return false;
    }

    var leadImage = (settings.image || settings.leadImage || "").toString().trim();
    var activeStock = normalizeMajordomeStockKey(getActiveMajordomeStock());
    var shouldSelectVehicle = settings.selectVehicle !== false;
    var isActiveStock = activeStock && activeStock === targetStock;
    var shouldUpdateGallery = settings.updateGallery !== false && (shouldSelectVehicle || isActiveStock);
    var shouldActivateGallery = settings.activateGallery !== false;
    var vehicle = null;
    if (selectedVehicle && normalizeMajordomeStockKey(selectedVehicle.Stock) === targetStock) {
        vehicle = selectedVehicle;
    }

    if (!vehicle && typeof inventoryVehicles !== "undefined" && Array.isArray(inventoryVehicles)) {
        vehicle = findMajordomeVehicleByStock(inventoryVehicles, targetStock);
    }

    if (!vehicle && typeof inventoryVehiclesSource !== "undefined" && Array.isArray(inventoryVehiclesSource)) {
        vehicle = findMajordomeVehicleByStock(inventoryVehiclesSource, targetStock);
    }

    if (!vehicle) {
        return false;
    }

    vehicle.Images = images;
    if (leadImage) {
        vehicle.Image = leadImage;
    } else if (images.length > 0 && images[0] && images[0].Source) {
        vehicle.Image = images[0].Source;
    } else {
        vehicle.Image = "";
    }

    if (shouldSelectVehicle || isActiveStock) {
        selectedVehicle = vehicle;
    }

    if ((shouldSelectVehicle || isActiveStock) && typeof selectedVehicleStock !== "undefined") {
        selectedVehicleStock = (vehicle.Stock || stock || "").toString().trim();
        window.majordomeSelectedStock = selectedVehicleStock;
    }

    if (shouldUpdateGallery) {
        loadGallery(vehicle);
        refreshMajordomeSelectedRowThumbnail(stock);
    }

    var $row = $("#majordomeInventoryBody .majordome-vehicle-row").filter(function () {
        return normalizeMajordomeStockKey($(this).attr("data-stock")) === targetStock;
    }).first();

    if ($row.length) {
        var deleteTitle = images.length > 0
            ? "Delete all " + images.length + " pictures for Stock# " + (vehicle.Stock || stock || "")
            : "No images to delete for Stock# " + (vehicle.Stock || stock || "");
        $row.find(".js-amm-delete-images")
            .attr("data-images-count", images.length)
            .attr("title", deleteTitle)
            .prop("disabled", images.length === 0);

        var thumbSource = leadImage || (images.length > 0 && images[0] ? images[0].Source : "") || vehicle.Image;
        var $rowImage = $row.find(".majordome-row-image");
        if (thumbSource) {
            var freshThumb = appendCacheBust(appendImageWidth(toInventoryImageUrl(thumbSource), 320), Date.now());
            $rowImage.attr("src", freshThumb);
        } else {
            $rowImage.attr("src", "");
        }
    }

    if (shouldSelectVehicle || isActiveStock) {
        $("#gallery-tab").text("Photos (" + images.length + ")");
    }
    if (typeof syncMajordomeGalleryAvailability === "function") {
        syncMajordomeGalleryAvailability();
    }
    if (shouldActivateGallery) {
        $("#gallery-tab").tab("show");
    }
    return true;
}

function uploadFiles(stock, input) {
    const files = Array.from((input && input.files) || []);
    if (!files.length) return;

    if (typeof window.openMajordomeImageUploadProgressModal === "function") {
        window.openMajordomeImageUploadProgressModal(stock, files);
        if (input) {
            input.value = "";
        }
        return;
    }

    const formData = new FormData();
    for (let i = 0; i < files.length; i++) {
        formData.append("files", files[i]);
    }
    formData.append("stock", stock);

    upload(formData, stock);
}

function uploadDroppedFiles(stock, files) {
    const fileList = Array.from(files || []);
    if (!fileList.length) return;

    if (typeof window.openMajordomeImageUploadProgressModal === "function") {
        window.openMajordomeImageUploadProgressModal(stock, fileList);
        return;
    }

    const formData = new FormData();
    fileList.forEach(f => formData.append("files", f, f.name));
    formData.append("stock", stock);

    upload(formData, stock);
}

async function upload(formData, stock) {
    const $overlay = $("#inventoryOverlay");
    if (!beginMajordomeImageAction($overlay)) {
        return;
    }

    try {
        const response = await fetch("/Majordome/UploadInventoryFiles", {
            method: "POST",
            body: formData
        });

        if (!response.ok) {
            throw new Error("Upload failed.");
        }

        let payload = null;
        try {
            payload = await response.json();
        } catch (jsonError) {
            payload = null;
        }

        if (payload && payload.success === false) {
            throw new Error(payload.message || "Upload failed.");
        }

        const uploadStock = normalizeMajordomeStockKey(stock);
        const activeStock = normalizeMajordomeStockKey(getActiveMajordomeStock());
        const hasImages = payload && Array.isArray(payload.images);
        const sameStock = uploadStock && activeStock && uploadStock === activeStock;

        if (hasImages && sameStock && applyUploadedImagesToMajordomeState(stock, payload.images, { image: payload.image })) {
            return;
        }

        await refreshMajordomeAfterImageMutation(stock, { keepGalleryTab: true });
    } catch (error) {
        console.error("Upload failed:", error);
        alert(error && error.message ? error.message : "Upload failed.");
    } finally {
        endMajordomeImageAction($overlay);
    }
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
    // Selection state is maintained client-side; actions pass stock explicitly.
    return Promise.resolve();
}

function setQrCode(vehicle) {
    var qrUrl = "/Majordome/Qr?stock=" + encodeURIComponent(vehicle.Stock || "") + "&vin=" + encodeURIComponent(vehicle.VIN || "");
    $("#qrImg").attr("src", qrUrl);
    $("#qrText").text(`${vehicle.Year} ${vehicle.Make} ${vehicle.Model} Stock# ${vehicle.Stock}`);
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

        if (typeof syncMajordomeDataOneDeletedLocalState === "function") {
            syncMajordomeDataOneDeletedLocalState(response.stock || stock);
        } else {
            const data = await getUpdatedItems();
            updateRow(data);
        }
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

async function deleteImages(stock) {
    const $overlay = $("#inventoryOverlay");
    if (!beginMajordomeImageAction($overlay)) {
        return;
    }

    try {
        const response = await postMajordome(`${root}Majordome/DeleteImages`, { stock });
        if (!response || !response.success) {
            throw new Error((response && response.message) || "Failed to delete images.");
        }

        if (Array.isArray(response.images) && applyUploadedImagesToMajordomeState(response.stock || stock, response.images, { image: response.image })) {
            return;
        }

        await refreshMajordomeAfterImageMutation(stock, { keepGalleryTab: true });
    } catch (err) {
        console.error("DeleteImages failed:", err);
        alert(err.message || "Failed to delete images on the server.");
    } finally {
        endMajordomeImageAction($overlay);
    }
}

async function deleteImage(id, file, object) {
    const stock = getActiveMajordomeStock();
    if (!stock) {
        alert("Please select a vehicle first.");
        return;
    }

    const $overlay = $("#inventoryOverlay");
    if (!beginMajordomeImageAction($overlay)) {
        return;
    }

    try {
        const response = await postMajordome(`${root}Majordome/DeleteImage`, { id, file, stock });
        if (!response || !response.success) {
            throw new Error((response && response.message) || "Failed to delete image.");
        }

        if (Array.isArray(response.images) && applyUploadedImagesToMajordomeState(response.stock || stock, response.images, { image: response.image })) {
            $("#close").click();
            return;
        }

        await refreshMajordomeAfterImageMutation(stock, { keepGalleryTab: true });
        $("#close").click();
    } catch (err) {
        console.error("DeleteImage failed:", err);
        alert(err.message || "Failed to delete image on the server.");
    } finally {
        endMajordomeImageAction($overlay);
    }
}

async function rotateImage(file, degrees, triggerElement) {
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
    if (!beginMajordomeImageAction($overlay)) {
        return;
    }

    window.majordomeSelectedStock = stock;
    window.majordomeForceActiveTab = "gallery-tab";

    const $card = triggerElement ? $(triggerElement).closest(".majordome-photo-card") : $();

    try {
        const response = await postMajordome(`${root}Majordome/RotateImage`, { file, stock, degrees: rotationDegrees });
        if (!response || !response.success) {
            throw new Error((response && response.message) || "Failed to rotate image.");
        }

        if ($card.length) {
            await refreshMajordomePhotoCardImage($card, file);
            updateGalleryDisplay();
            $("#gallery-tab").tab("show");
        } else {
            await refreshMajordomeAfterImageMutation(stock, { keepGalleryTab: true });
        }
    } catch (err) {
        console.error("RotateImage failed:", err);
        alert(err.message || "Failed to rotate image on the server.");
    } finally {
        endMajordomeImageAction($overlay);
    }
}

async function createStory(stock) {
    var targetStock = (stock || "").toString().trim();
    if (!targetStock) {
        alert("Please select a vehicle first.");
        return;
    }

    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    try {
        const response = await postMajordome(`${root}Majordome/CreateStory`, { stock: targetStock });
        if (!response || !response.success) {
            throw new Error((response && response.message) || "Failed to create story.");
        }

        const storyTitle = (response.Title || "").toString();
        const storyHtml = (response.Story || "").toString();

        if (typeof quill !== "undefined" && quill && quill.clipboard) {
            quill.setContents([]);
            quill.clipboard.dangerouslyPasteHTML(0, storyHtml, "api");
        }
        $("#storyTitle").val(storyTitle);

        if (typeof syncMajordomeStoryLocalState === "function") {
            syncMajordomeStoryLocalState(targetStock, storyTitle, storyHtml);
        } else {
            const data = await getUpdatedItems();
            updateRow(data);
        }
    } catch (err) {
        console.error("CreateStory failed:", err);
        alert((err && err.message) || "Failed to create story on the server.");
    } finally {
        if (typeof hideSpinner === "function") {
            hideSpinner($overlay);
        }
    }
}

async function deleteStory(stock) {
    var targetStock = (stock || "").toString().trim();
    if (!targetStock) {
        alert("Please select a vehicle first.");
        return;
    }

    const $overlay = $("#inventoryOverlay");
    showSpinner($overlay);

    try {
        const response = await postMajordome(`${root}Majordome/DeleteStory`, { stock: targetStock });
        if (!response || !response.success) {
            throw new Error((response && response.message) || "Failed to delete story.");
        }

        if (typeof quill !== "undefined" && quill && quill.clipboard) {
            quill.setContents([]);
            quill.clipboard.dangerouslyPasteHTML(0, "", "api");
        }
        $("#storyTitle").val("");

        if (typeof syncMajordomeStoryLocalState === "function") {
            syncMajordomeStoryLocalState(targetStock, "", "");
        } else {
            const data = await getUpdatedItems();
            updateRow(data);
        }
    } catch (err) {
        console.error("DeleteStory failed:", err);
        alert((err && err.message) || "Failed to delete story on the server.");
    } finally {
        if (typeof hideSpinner === "function") {
            hideSpinner($overlay);
        }
    }
}

async function saveOrder(sorted, options) {
    var settings = options || {};
    var fastMode = settings.fastMode === true;
    const stock = getActiveMajordomeStock();
    const $overlay = $("#inventoryOverlay");
    if (!beginMajordomeImageAction($overlay)) {
        return;
    }

    try {
        const response = await postMajordome(
            `${root}Majordome/SaveOrder`,
            { sorted: sorted },
            { traditional: true }
        );

        if (!response || !response.success) {
            throw new Error((response && response.message) || "Failed to save order.");
        }

        if (fastMode) {
            reorderMajordomeSelectedVehicleImages(sorted, stock);
            refreshMajordomeSelectedRowThumbnail(stock);
            window.majordomeForceActiveTab = "gallery-tab";
            $("#gallery-tab").tab("show");
            return;
        }

        await refreshMajordomeAfterImageMutation(stock, { keepGalleryTab: true });
    } catch (err) {
        console.error("SaveOrder failed:", err);
        alert(err.message || "Failed to save order on the server.");
    } finally {
        endMajordomeImageAction($overlay);
    }
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
        const background = getOverlayBackgroundSettings(data);

        overlay.empty(); // Clear previous content
        overlay.attr("style", data.overlay.style || "");
        overlay.css("background-color", buildMajordomeOverlayBackground(background.color, background.opacity));
        overlay.attr("data-background-color", background.color);
        overlay.attr("data-background-opacity", background.opacity);

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
    const background = getOverlayBackgroundSettings(data);

    let styleString = data.overlay.style;
    let parser = new StyleParser(styleString);

    $("#backgroundColor").val(background.color).trigger("change");
    $("#backgroundOpacity").val(background.opacity).trigger("change");

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

async function saveOverlayFile() {
    const context = getMajordomeOverlayContext();
    const stock = context && context.stock ? context.stock : getActiveMajordomeStock();
    const imagePath = context && context.imagePath ? context.imagePath : "";

    if (!stock || !imagePath) {
        alert("Overlay file context is missing.");
        return;
    }

    const $overlaySpinner = $("#inventoryOverlay");
    if (!beginMajordomeImageAction($overlaySpinner)) {
        return;
    }

    const overlay = $("#overlay");
    const backgroundColor = ($("#backgroundColor").val() || "black").toString();
    const backgroundOpacity = normalizeMajordomeOverlayOpacity($("#backgroundOpacity").val());
    const overlayStyle = `background-color:${backgroundColor};background-opacity:${backgroundOpacity};`;

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
            backgroundColor: backgroundColor,
            backgroundOpacity: backgroundOpacity,
            children: children
        }
    };

    try {
        const response = await postMajordome(`${root}Majordome/SaveOverlayFile`, {
            overlay: JSON.stringify(json),
            stock: stock,
            imagePath: imagePath
        });

        if (!response || !response.success) {
            throw new Error((response && response.message) || "Failed to save overlay file.");
        }

        $("#close").click();
    } catch (err) {
        console.error("SaveOverlayFile failed:", err);
        alert(err.message || "Failed to save overlay file.");
    } finally {
        endMajordomeImageAction($overlaySpinner);
    }
}

