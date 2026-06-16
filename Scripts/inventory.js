function reset() {
    $.post(`${root}Inventory/Reset`)
        .done(function (response) {
            if (response.redirectUrl) {
                window.location.href = response.redirectUrl;
            }
        })
        .fail(function (xhr, status, error) {
            console.error("Resetting error", error);
        });
}

function applyTerm(term) {
    $.post(`${root}Inventory/ApplyTerm`, { term })
        .done(function (response) {
            if (response.redirectUrl) {
                window.location.href = response.redirectUrl;
            }
        })
        .fail(function (xhr, status, error) {
            console.error("Error applying term:", error);
        });
}

function applyFilterTerm(term) {
    const rawTerm = (term || "").toString();
    const filter = rawTerm.trim().toUpperCase();
    const terms = filter.split(/\s+/).filter(Boolean);

    if (!filter) {
        sessionStorage.removeItem("term");
    } else {
        sessionStorage.setItem("term", rawTerm);
    }

    if (window.applyInventoryPanelFilters && document.getElementById("inventory")) {
        $("#filterTerm").removeClass("text-info").removeClass("border-info");
        window.applyInventoryPanelFilters();
        return;
    }

    const vehicles = document.querySelectorAll(".card");
    let combined;

    vehicles.forEach(vehicle => {
        const stock = $(vehicle).data("stock") || "";
        const vin = $(vehicle).data("vin") || "";
        const dataone = $(vehicle).data("dataone") || "";
        const make = $(vehicle).data("make") || "";
        const model = $(vehicle).data("model") || "";
        const style = $(vehicle).data("style") || "";
        const type = $(vehicle).data("type") || "";
        const transmission = $(vehicle).data("transmission") || "";
        const year = $(vehicle).data("year") || "";
        const color = $(vehicle).data("color") || "";
        const color2 = $(vehicle).data("color2") || "";
        const location = $(vehicle).data("location-code") || "";
        const story = $(vehicle).data("story") || "";
        const images = $(vehicle).data("images") || "";
        const cylinders = $(vehicle).data("cylinders") || "";

        $("#filterTerm").removeClass("text-info").removeClass("border-info");
        combined = `${stock} ${vin} ${dataone} ${make} ${model} ${style} ${type} ${transmission} ${year} ${color} ${color2} ${location} ${story} ${images} ${cylinders} ${$(vehicle).data("fuel") || ""} ${$(vehicle).data("drive") || ""} ${$(vehicle).data("body") || ""}`.toUpperCase();

        const isMatch = terms.length === 0 || terms.every(t => combined.includes(t));

        if (isMatch) {
            vehicle.style.display = "";
        } else {
            vehicle.style.display = "none";
        }
    });

    let stocks = $(".card:visible").map(function () {
        return $(this).data("stock");
    }).get();

    if (stocks.length > 0) {
        localStorage.setItem("matchedStocks", JSON.stringify(stocks));
    } else {
        localStorage.removeItem("matchedStocks"); // Clear old if none matched
    }

    if (window.refreshInventoryCardImages) {
        window.refreshInventoryCardImages();
    }
}

function applyFilterLiked() {
    updateFilterLiked();

    if (window.applyInventoryPanelFilters && document.getElementById("inventory")) {
        window.applyInventoryPanelFilters();
        return;
    }

    const items = document.querySelectorAll("#inventory > li");
    items.forEach(item => {
        if ($("#filterLiked").hasClass("bi-heart-fill")) {
            var heart = $(item).find(".liked");
            if (heart && heart[0].style.display === "") {
                item.style.display = "";
            }
            else {
                item.style.display = "none";
            }
        }
        else {
            item.style.display = "";
        }
    });

    if (window.refreshInventoryCardImages) {
        window.refreshInventoryCardImages();
    }
}

function applyFilterLast() {
    updateFilterLast();

    if (window.applyInventoryPanelFilters && document.getElementById("inventory")) {
        window.applyInventoryPanelFilters();
        return;
    }

    const items = document.querySelectorAll("#inventory > li");
    items.forEach(item => {
        if ($("#filterLast").hasClass("bi-journal-album")) {
            const stock = $(item).data("stock") || "";
            if (isCarLast(stock)) {
                item.style.display = "";
            }
            else {
                item.style.display = "none";
            }
        }
        else {
            item.style.display = "";
        }
    });

    if (window.refreshInventoryCardImages) {
        window.refreshInventoryCardImages();
    }
}

function getInterestRate(creditScore) {
    if (creditScore >= 750) return 5.0;
    if (creditScore >= 700) return 6.5;
    if (creditScore >= 650) return 8.0;
    if (creditScore >= 600) return 10.0;
    return 15.0; // bad credit
}

function calculateMonthlyPayment(P, rate, month) {
    const r = rate / 100 / 12;
    const n = month;
    return (P * r) / (1 - Math.pow(1 + r, -n));
}

(function () {
    function selectedByField() {
        var selected = {};

        $(".inventory-filter-check:checked").each(function () {
            var field = $(this).data("field");
            var value = normalizeInventoryValue($(this).data("value"));

            if (!selected[field]) selected[field] = [];
            selected[field].push(value);
        });

        return selected;
    }

    function parseInventoryNumber(value) {
        var parsed = parseFloat(String(value == null ? "" : value).replace(/[^0-9.\-]/g, ""));
        return isNaN(parsed) ? null : parsed;
    }

    function normalizeInventoryValue(value) {
        return $.trim(String(value || "")).toUpperCase();
    }

    var inventoryImageObserver = null;
    var inventoryImageObserverRoot = undefined;
    var inventoryImageRefreshTimer = 0;

    function getInventoryImageRoot() {
        var root = document.getElementById("inventory");
        if (!root) return null;

        return root.scrollHeight > root.clientHeight + 1 ? root : null;
    }

    function isInventoryCardImageVisible(img) {
        return $(img).closest("li.card").is(":visible");
    }

    function isInventoryCardImageNearViewport(img, root) {
        if (!isInventoryCardImageVisible(img)) return false;

        var margin = 900;
        var rect = img.getBoundingClientRect();

        if (root) {
            var rootRect = root.getBoundingClientRect();
            return rect.top <= rootRect.bottom + margin && rect.bottom >= rootRect.top - margin;
        }

        var viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
        return rect.top <= viewportHeight + margin && rect.bottom >= -margin;
    }

    function setInventoryCardImageSource(img, source) {
        if (!source || img.getAttribute("src") === source) return;
        img.setAttribute("src", source);
    }

    function loadInventoryCardImage(img) {
        var source = img.getAttribute("data-src");
        if (!source) return;

        img.removeAttribute("data-src");
        img.setAttribute("data-inventory-image-loaded", "1");
        setInventoryCardImageSource(img, source);
    }

    function handleInventoryCardImageError() {
        var img = this;
        var current = img.getAttribute("src") || "";
        var fallback = img.getAttribute("data-fallback-src") || "";
        var finalFallback = img.getAttribute("data-final-fallback-src") || "";
        var state = img.getAttribute("data-fallback-state") || "";

        img.removeAttribute("data-src");

        if (fallback && state !== "proxy" && current !== fallback) {
            img.setAttribute("data-fallback-state", "proxy");
            setInventoryCardImageSource(img, fallback);
            return;
        }

        if (finalFallback && state !== "final" && current !== finalFallback) {
            img.setAttribute("data-fallback-state", "final");
            setInventoryCardImageSource(img, finalFallback);
        }
    }

    function bindInventoryCardImage(img) {
        if (img.getAttribute("data-inventory-image-bound") === "1") return;

        img.setAttribute("data-inventory-image-bound", "1");
        img.addEventListener("error", handleInventoryCardImageError);

        if (img.complete && img.naturalWidth === 0) {
            handleInventoryCardImageError.call(img);
        }
    }

    function refreshInventoryCardImagesNow() {
        var root = getInventoryImageRoot();
        var allImages = document.querySelectorAll("#inventory .gtx-img");
        var pendingImages = document.querySelectorAll("#inventory .gtx-img[data-src]");

        Array.prototype.forEach.call(allImages, bindInventoryCardImage);

        if ("IntersectionObserver" in window) {
            if (inventoryImageObserver && inventoryImageObserverRoot !== root) {
                inventoryImageObserver.disconnect();
                inventoryImageObserver = null;
            }

            if (!inventoryImageObserver) {
                inventoryImageObserverRoot = root;
                inventoryImageObserver = new IntersectionObserver(function (entries) {
                    entries.forEach(function (entry) {
                        if (!entry.isIntersecting || !isInventoryCardImageVisible(entry.target)) return;

                        loadInventoryCardImage(entry.target);
                        inventoryImageObserver.unobserve(entry.target);
                    });
                }, {
                    root: root,
                    rootMargin: "900px 0px",
                    threshold: 0.01
                });
            }

            Array.prototype.forEach.call(pendingImages, function (img) {
                if (isInventoryCardImageNearViewport(img, root)) {
                    loadInventoryCardImage(img);
                    return;
                }

                if (isInventoryCardImageVisible(img)) {
                    inventoryImageObserver.observe(img);
                }
            });

            return;
        }

        Array.prototype.forEach.call(pendingImages, function (img) {
            if (isInventoryCardImageNearViewport(img, root)) {
                loadInventoryCardImage(img);
            }
        });
    }

    function scheduleInventoryCardImageRefresh() {
        window.clearTimeout(inventoryImageRefreshTimer);
        inventoryImageRefreshTimer = window.setTimeout(refreshInventoryCardImagesNow, 40);
    }

    function initInventoryCardImages() {
        refreshInventoryCardImagesNow();

        $("#inventory")
            .off("scroll.inventoryImages")
            .on("scroll.inventoryImages", scheduleInventoryCardImageRefresh);

        $(window)
            .off("resize.inventoryImages pageshow.inventoryImages")
            .on("resize.inventoryImages pageshow.inventoryImages", scheduleInventoryCardImageRefresh);
    }

    window.refreshInventoryCardImages = scheduleInventoryCardImageRefresh;

    function splitInventoryTerms(value) {
        return normalizeInventoryValue(value).split(/\s+/).filter(function (term) {
            return term.length > 0;
        });
    }

    function inventoryContainsAllTerms(haystack, terms) {
        if (!terms.length) return true;

        for (var i = 0; i < terms.length; i++) {
            if (haystack.indexOf(terms[i]) === -1) {
                return false;
            }
        }

        return true;
    }

    function formatInventoryRangeValue(value, format) {
        var number = parseInventoryNumber(value);

        if (number === null) return "";

        if (format === "currency") {
            return "$" + Math.round(number).toLocaleString();
        }

        if (format === "number") {
            return Math.round(number).toLocaleString();
        }

        return String(Math.round(number));
    }

    function selectedRangeByField() {
        var ranges = {};

        $(".inventory-range-filter").each(function () {
            var $panel = $(this);
            var field = $panel.data("range-field");
            var minLimit = parseInventoryNumber($panel.data("range-min"));
            var maxLimit = parseInventoryNumber($panel.data("range-max"));
            var rangeFilter = $panel.data("gtx.rangeFilter") || this.__gtxRangeFilter;
            var rangeValues = rangeFilter && typeof rangeFilter.value === "function" ? rangeFilter.value() : null;
            var min = rangeValues ? parseInventoryNumber(rangeValues.min) : parseInventoryNumber($panel.find(".inventory-range-min-input").val());
            var max = rangeValues ? parseInventoryNumber(rangeValues.max) : parseInventoryNumber($panel.find(".inventory-range-max-input").val());

            if (min === null) {
                min = minLimit;
            }

            if (max === null) {
                max = parseInventoryNumber($panel.find(".inventory-range-input").val());
            }

            if (!field || min === null || max === null || minLimit === null || maxLimit === null) return;

            min = Math.max(minLimit, Math.min(maxLimit, min));
            max = Math.max(minLimit, Math.min(maxLimit, max));

            if (min > max) {
                var swap = min;
                min = max;
                max = swap;
            }

            ranges[field] = {
                min: min,
                max: max,
                minLimit: minLimit,
                maxLimit: maxLimit,
                format: $panel.data("range-format")
            };
        });

        return ranges;
    }

    function inventoryRangeMatches($vehicle, ranges) {
        var matched = true;

        $.each(ranges, function (field, range) {
            var value = parseInventoryNumber($vehicle.data(field));

            if (value === null || value < range.min || value > range.max) {
                matched = false;
                return false;
            }
        });

        return matched;
    }

    function syncInventoryRangeLabels() {
        $(".inventory-range-filter").each(function () {
            var $panel = $(this);
            var field = $panel.data("range-field");
            var format = $panel.data("range-format");
            var minLimit = parseInventoryNumber($panel.data("range-min"));
            var maxLimit = parseInventoryNumber($panel.data("range-max"));
            var rangeFilter = $panel.data("gtx.rangeFilter") || this.__gtxRangeFilter;

            if (rangeFilter && typeof rangeFilter.value === "function") {
                return;
            }

            if ($.fn.gtxRangeFilter && $panel.find("[data-range-slider]").length) {
                $panel.gtxRangeFilter();
                return;
            }

            var $minInput = $panel.find(".inventory-range-min-input");
            var $maxInput = $panel.find(".inventory-range-max-input");
            var min = parseInventoryNumber($minInput.val());
            var max = parseInventoryNumber($maxInput.val());

            if (rangeFilter && typeof rangeFilter.value === "function") {
                var value = rangeFilter.value();
                min = parseInventoryNumber(value.min);
                max = parseInventoryNumber(value.max);
            }

            if (min === null) min = minLimit;
            if (max === null) max = maxLimit;
            if (min === null || max === null || minLimit === null || maxLimit === null) return;

            if (max > maxLimit) {
                max = maxLimit;
                $maxInput.val(max);
            }

            if (min < minLimit) {
                min = minLimit;
                $minInput.val(min);
            }

            var minText = formatInventoryRangeValue(min, format);
            var maxText = formatInventoryRangeValue(max, format);

            $panel.find("[data-range-value-label-min='" + field + "'], [data-range-value-label='" + field + "-min']").text(minText);
            $panel.find("[data-range-value-label-max='" + field + "'], [data-range-value-label='" + field + "-max'], [data-range-value-label='" + field + "']").text(maxText);
        });
    }

    function resetInventoryRanges() {
        $(".inventory-range-filter").each(function () {
            var $panel = $(this);
            var rangeFilter = $panel.data("gtx.rangeFilter") || this.__gtxRangeFilter;

            if (rangeFilter && typeof rangeFilter.reset === "function") {
                rangeFilter.reset(true);
                return;
            }

            if ($.fn.gtxRangeFilter && $panel.find("[data-range-slider]").length) {
                $panel.gtxRangeFilter("reset", true);
                return;
            }

            $panel.find(".inventory-range-min-input").val($panel.data("range-min"));
            $panel.find(".inventory-range-max-input, .inventory-range-input").val($panel.data("range-max"));
        });

        syncInventoryRangeLabels();
    }

    function inventoryTermMatches($vehicle, filter) {
        var terms = splitInventoryTerms(filter);
        if (!terms.length) return true;
        if (terms.length === 1 && terms[0] === "@@") return false;

        var stock = normalizeInventoryValue($vehicle.data("stock"));
        var vin = normalizeInventoryValue($vehicle.data("vin"));
        var dataone = normalizeInventoryValue($vehicle.data("dataone"));
        var make = normalizeInventoryValue($vehicle.data("make"));
        var model = normalizeInventoryValue($vehicle.data("model"));
        var style = normalizeInventoryValue($vehicle.data("style"));
        var type = normalizeInventoryValue($vehicle.data("type"));
        var transmission = normalizeInventoryValue($vehicle.data("transmission"));
        var year = normalizeInventoryValue($vehicle.data("year"));
        var color = normalizeInventoryValue($vehicle.data("color"));
        var color2 = normalizeInventoryValue($vehicle.data("color2"));
        var location = normalizeInventoryValue($vehicle.data("location-code"));
        var story = normalizeInventoryValue($vehicle.data("story"));
        var images = normalizeInventoryValue($vehicle.data("images"));
        var cylinders = normalizeInventoryValue($vehicle.data("cylinders"));
        var combined;

        if (filter.indexOf("@@") === 0) {
            var prefixMap = {
                "@@YR": "@@YR " + year,
                "@@MK": "@@MK " + make,
                "@@MD": "@@MD " + model,
                "@@TR": "@@TR " + transmission,
                "@@CY": "@@CY " + cylinders,
                "@@OW": "@@OW " + location
            };

            combined = "@@" + story + " @@" + images + " @@" + dataone;

            $.each(prefixMap, function (key, text) {
                if (filter.indexOf(key) === 0) {
                    combined = text;
                    return false;
                }
            });
        } else {
            combined = [
                stock, vin, make, model, style, type, transmission, year,
                color, color2, normalizeInventoryValue($vehicle.data("fuel")),
                normalizeInventoryValue($vehicle.data("drive")),
                normalizeInventoryValue($vehicle.data("body")),
                dataone, location, story, images, cylinders
            ].join(" ");
        }

        return inventoryContainsAllTerms(combined, terms);
    }

    function inventoryCheckboxMatches($vehicle, selected) {
        var matched = true;

        $.each(selected, function (field, values) {
            if (!values.length) return;

            var vehicleValue = normalizeInventoryValue($vehicle.data(field));
            if ($.inArray(vehicleValue, values) === -1) {
                matched = false;
                return false;
            }
        });

        return matched;
    }

    function inventoryLikedMatches($vehicle) {
        if (!$("#filterLiked").hasClass("bi-heart-fill")) return true;
        return $vehicle.find(".liked").length && $vehicle.find(".liked")[0].style.display === "";
    }

    function inventoryLastMatches($vehicle) {
        if (!$("#filterLast").hasClass("bi-journal-album")) return true;
        return isCarLast(normalizeInventoryValue($vehicle.data("stock")));
    }

    function syncInventoryModelPanel(selected) {
        var selectedMakes = selected.make || [];
        var availableModels = {};
        var hasMakes = selectedMakes.length > 0;
        var $modelPanel = $("#invFilterModels").closest(".inventory-filter-panel");
        var $modelToggle = $modelPanel.find(".inventory-filter-panel-toggle");
        var $modelInstruction = $modelPanel.find("[data-empty-panel-for='model']");

        if (hasMakes) {
            $("#inventory > li.card").each(function () {
                var $vehicle = $(this);
                var make = normalizeInventoryValue($vehicle.data("make"));
                var model = normalizeInventoryValue($vehicle.data("model"));

                if (model && $.inArray(make, selectedMakes) !== -1) {
                    availableModels[model] = true;
                }
            });
        }

        $modelToggle.toggle(hasMakes);
        $modelInstruction.toggle(!hasMakes);

        if (!hasMakes) {
            $("#invFilterModels").removeClass("show");
            $modelToggle.addClass("collapsed").attr("aria-expanded", "false");
        }

        var visibleModels = 0;
        $("#invFilterModels .inventory-check-row").each(function () {
            var $row = $(this);
            var $check = $row.find(".inventory-filter-check");
            var model = normalizeInventoryValue($check.data("value"));
            var isAvailable = hasMakes && !!availableModels[model];

            if (!isAvailable) {
                $check.prop("checked", false);
            }

            $row.toggle(isAvailable);
            if (isAvailable) visibleModels++;
        });

        $("[data-bs-target='#invFilterModels'] .inventory-filter-panel-count").text(visibleModels);
    }

    function updateInventoryFilterCounts(selected, filter, ranges) {
        $(".inventory-check-hit").each(function () {
            var $count = $(this);
            var field = $count.data("count-field");
            var value = normalizeInventoryValue($count.data("count-value"));
            var count = 0;

            $("#inventory > li.card").each(function () {
                var $vehicle = $(this);
                var scopedSelected = $.extend(true, {}, selected);
                delete scopedSelected[field];

                if (normalizeInventoryValue($vehicle.data(field)) === value &&
                    inventoryCheckboxMatches($vehicle, scopedSelected) &&
                    inventoryRangeMatches($vehicle, ranges) &&
                    inventoryTermMatches($vehicle, filter) &&
                    inventoryLikedMatches($vehicle) &&
                    inventoryLastMatches($vehicle)) {
                    count++;
                }
            });

            var $row = $count.closest(".inventory-check-row");
            var isChecked = $row.find(".inventory-filter-check").is(":checked");

            $count.text(count);
            $row.toggle(count > 0 || isChecked);
            $row.removeClass("opacity-50");
        });
    }

    function syncInventoryVisibleCount(visibleCount) {
        var title = visibleCount + " vehicle(s)";

        $("#inventoryFilterCount").text(visibleCount);
        $("#inventoryMobileMatchCount").text(visibleCount);
        $(".main-title").text(title);
    }

    var inventoryAllPageSize = "all";
    var inventoryPageSizes = [20, 40, 60, 80];
    var inventoryDefaultPageSize = inventoryAllPageSize;
    var inventoryCurrentPage = 1;
    var inventoryPaginationResizeTimer = 0;
    var inventoryRangeFilterApplyTimer = 0;
    var inventoryPageSizeSelector = ".inventory-page-size-select";
    var inventoryPagerSelector = ".inventory-pager";
    var inventoryPageSizeDefaultKey = "inventoryPageSizeDefault";

    function isInventoryCardMatched($vehicle) {
        return $vehicle.attr("data-inventory-filter-match") !== "0";
    }

    function inventoryMatchedCards() {
        return $("#inventory > li.card").filter(function () {
            return isInventoryCardMatched($(this));
        });
    }

    function inventoryMatchedStocks($items) {
        return $items.map(function () {
            return $(this).data("stock");
        }).get();
    }

    function syncInventoryMatchedStocks($items) {
        var stocks = inventoryMatchedStocks($items);

        if (stocks.length > 0) {
            localStorage.setItem("matchedStocks", JSON.stringify(stocks));
        } else {
            localStorage.removeItem("matchedStocks");
        }

        return stocks;
    }

    function isInventoryAllPageSize(value) {
        return String(value || "").toLowerCase() === inventoryAllPageSize;
    }

    function validInventoryPageSize(value) {
        if (isInventoryAllPageSize(value)) return true;

        return $.inArray(parseInt(value, 10), inventoryPageSizes) !== -1;
    }

    function inventoryPageSize() {
        var selectedValue = $(inventoryPageSizeSelector).first().val();
        if (isInventoryAllPageSize(selectedValue)) return inventoryAllPageSize;

        var selected = parseInt(selectedValue, 10);
        return validInventoryPageSize(selected) ? selected : inventoryDefaultPageSize;
    }

    function inventoryTotalPages(totalCount, pageSize) {
        if (isInventoryAllPageSize(pageSize)) return 1;

        return Math.max(1, Math.ceil(totalCount / pageSize));
    }

    function setInventoryPagerDisabled($button, disabled) {
        $button.prop("disabled", disabled).attr("aria-disabled", disabled ? "true" : "false");
    }

    function syncInventoryPageSizeControls(pageSize) {
        $(inventoryPageSizeSelector).val(pageSize);
    }

    function renderInventoryPageNumberButtons(totalPages, hasRows, showAll) {
        var $numbers = $(".inventory-page-numbers");
        $numbers.empty().toggle(!showAll && hasRows);

        if (!$numbers.length || showAll || !hasRows) return;

        for (var page = 1; page <= totalPages; page++) {
            var isCurrent = page === inventoryCurrentPage;
            var ariaLabel = isCurrent ? "Page " + page + ", current page" : "Go to page " + page;

            var $button = $("<button>", {
                type: "button",
                "class": "inventory-page-number-btn shadow-sm" + (isCurrent ? " is-active" : ""),
                "data-page-action": "page",
                "data-page-number": page,
                "aria-label": ariaLabel,
                title: "Page " + page,
                text: page
            }).appendTo($numbers);

            if (isCurrent) {
                $button.attr("aria-current", "page");
            }
        }
    }

    function syncInventoryPager(totalCount, pageSize, totalPages, startIndex, endIndex) {
        var hasRows = totalCount > 0;
        var showAll = isInventoryAllPageSize(pageSize);
        var pageText = showAll
            ? (hasRows ? "All vehicles" : "No vehicles")
            : (hasRows ? "Page " + inventoryCurrentPage + " of " + totalPages : "Page 0 of 0");

        syncInventoryPageSizeControls(pageSize);
        $(".inventory-bottom-paging").toggleClass("is-hidden", showAll);
        $(".inventory-page-status").text(pageText).toggle(!showAll);
        $(inventoryPagerSelector + " [data-page-action]").toggle(!showAll);
        renderInventoryPageNumberButtons(totalPages, hasRows, showAll);

        setInventoryPagerDisabled($(inventoryPagerSelector + " [data-page-action='first']"), !hasRows || inventoryCurrentPage <= 1);
        setInventoryPagerDisabled($(inventoryPagerSelector + " [data-page-action='prev']"), !hasRows || inventoryCurrentPage <= 1);
        setInventoryPagerDisabled($(inventoryPagerSelector + " [data-page-action='next']"), !hasRows || inventoryCurrentPage >= totalPages);
        setInventoryPagerDisabled($(inventoryPagerSelector + " [data-page-action='last']"), !hasRows || inventoryCurrentPage >= totalPages);
        $(inventoryPageSizeSelector).prop("disabled", !hasRows);
    }

    function isInventoryPagingEnabled() {
        return $(inventoryPageSizeSelector).length > 0 &&
            $(inventoryPagerSelector).length > 0 &&
            $("#inventory").length > 0 &&
            isInventoryDesktopLayout();
    }

    function scrollInventoryGridToTop() {
        var grid = document.getElementById("inventory");
        if (grid) grid.scrollTop = 0;
    }

    function applyInventoryPagination(options) {
        options = options || {};

        var $allItems = $("#inventory > li.card");
        if (!$allItems.length) return;

        var $matched = inventoryMatchedCards();
        var totalCount = $matched.length;
        var pageSize = inventoryPageSize();
        var totalPages = inventoryTotalPages(totalCount, pageSize);
        var pagingEnabled = isInventoryPagingEnabled() && !isInventoryAllPageSize(pageSize);

        syncInventoryMatchedStocks($matched);

        if (options.resetPage) {
            inventoryCurrentPage = 1;
        }

        if (!pagingEnabled) {
            $allItems.each(function () {
                var $vehicle = $(this);
                $vehicle.toggle(isInventoryCardMatched($vehicle));
            });
            inventoryCurrentPage = Math.min(inventoryCurrentPage, totalPages);
            syncInventoryPager(totalCount, pageSize, totalPages, 0, totalCount);
            if (options.scrollToTop) {
                scrollInventoryGridToTop();
            }
            scheduleInventoryCardImageRefresh();
            return;
        }

        inventoryCurrentPage = Math.max(1, Math.min(inventoryCurrentPage, totalPages));

        var startIndex = (inventoryCurrentPage - 1) * pageSize;
        var endIndex = Math.min(startIndex + pageSize, totalCount);

        $allItems.hide();
        $matched.slice(startIndex, endIndex).show();
        syncInventoryPager(totalCount, pageSize, totalPages, startIndex, endIndex);

        if (options.scrollToTop) {
            scrollInventoryGridToTop();
        }

        scheduleInventoryCardImageRefresh();
    }

    function initInventoryPaginationControls() {
        var $pageSize = $(inventoryPageSizeSelector);
        var $pager = $(inventoryPagerSelector);

        if (!$pageSize.length || !$pager.length || !$("#inventory").length) return;

        if (localStorage.getItem(inventoryPageSizeDefaultKey) !== inventoryDefaultPageSize) {
            localStorage.setItem("inventoryPageSize", inventoryDefaultPageSize);
            localStorage.setItem(inventoryPageSizeDefaultKey, inventoryDefaultPageSize);
        }

        var savedPageSize = localStorage.getItem("inventoryPageSize");
        syncInventoryPageSizeControls(validInventoryPageSize(savedPageSize) ? savedPageSize : inventoryDefaultPageSize);

        $pageSize.off("change.inventoryPagination").on("change.inventoryPagination", function () {
            syncInventoryPageSizeControls($(this).val());
            var nextPageSize = inventoryPageSize();

            localStorage.setItem("inventoryPageSize", nextPageSize);
            applyInventoryPagination({ resetPage: true, scrollToTop: true });
        });

        $pager.off("click.inventoryPagination").on("click.inventoryPagination", "[data-page-action]", function (event) {
            event.preventDefault();
            var pageSize = inventoryPageSize();
            var totalPages = inventoryTotalPages(inventoryMatchedCards().length, pageSize);
            var action = $(this).data("page-action");

            if (action === "first") inventoryCurrentPage = 1;
            if (action === "prev") inventoryCurrentPage--;
            if (action === "next") inventoryCurrentPage++;
            if (action === "last") inventoryCurrentPage = totalPages;
            if (action === "page") {
                var targetPage = parseInt($(this).data("page-number"), 10);
                if (isNaN(targetPage)) return;
                inventoryCurrentPage = targetPage;
            }

            applyInventoryPagination({ scrollToTop: true });
        });

        $(window).off("resize.inventoryPagination").on("resize.inventoryPagination", function () {
            window.clearTimeout(inventoryPaginationResizeTimer);
            inventoryPaginationResizeTimer = window.setTimeout(function () {
                applyInventoryPagination();
            }, 80);
        });
    }

    window.getInventoryMatchedStocks = function () {
        return inventoryMatchedStocks(inventoryMatchedCards());
    };

    window.refreshInventoryPagination = applyInventoryPagination;

    function scheduleInventoryRangeFilterApply() {
        window.clearTimeout(inventoryRangeFilterApplyTimer);
        inventoryRangeFilterApplyTimer = window.setTimeout(function () {
            window.applyInventoryPanelFilters();
        }, 0);
    }

    function activeInventoryFilterCount(selected, ranges, filter) {
        var count = filter ? 1 : 0;

        $.each(selected, function (_, values) {
            count += values.length;
        });

        $.each(ranges, function (_, range) {
            if (range.min > range.minLimit || range.max < range.maxLimit) {
                count++;
            }
        });

        if ($("#filterLiked").hasClass("bi-heart-fill")) count++;
        if ($("#filterLast").hasClass("bi-journal-album")) count++;

        return count;
    }

    function syncInventoryMobileFilterState(visibleCount, selected, ranges, filter) {
        var activeCount = activeInventoryFilterCount(selected, ranges, filter);

        $("#inventoryMobileMatchCount").text(visibleCount);
        $("#inventoryMobileFilterCount").text(activeCount);
        $("#inventoryMobileActiveBadge")
            .text(activeCount)
            .toggleClass("is-active", activeCount > 0);
        $(".inventory-mobile-filter-btn").toggleClass("has-active-filters", activeCount > 0);
    }

    function clampInventorySidebarWidth(width) {
        var maxWidth = Math.min(560, Math.max(300, $(window).width() - 420));
        return Math.max(260, Math.min(maxWidth, width));
    }

    function setInventorySidebarState($split, isCollapsed) {
        var $toggle = $("#inventorySidebarToggle");
        var $icon = $toggle.find("i");
        var title = isCollapsed ? "Expand filters" : "Collapse filters";

        $split.toggleClass("inventory-sidebar-collapsed", isCollapsed);
        $toggle.attr("title", title).attr("aria-label", title);
        $icon.toggleClass("bi-chevron-left", !isCollapsed);
        $icon.toggleClass("bi-chevron-right", isCollapsed);
        localStorage.setItem("inventorySidebarCollapsed", isCollapsed ? "1" : "0");
    }

    function setInventorySidebarWidth($split, width) {
        var clampedWidth = clampInventorySidebarWidth(width);

        $split.css("--inventory-sidebar-width", clampedWidth + "px");
        localStorage.setItem("inventorySidebarWidth", clampedWidth);
    }

    function pointerClientX(event) {
        var originalEvent = event.originalEvent || event;
        var touch = originalEvent.touches && originalEvent.touches.length ? originalEvent.touches[0] : null;
        var changedTouch = originalEvent.changedTouches && originalEvent.changedTouches.length ? originalEvent.changedTouches[0] : null;

        return touch ? touch.clientX : (changedTouch ? changedTouch.clientX : event.clientX);
    }

    function isInventoryDesktopLayout() {
        return !window.matchMedia || window.matchMedia("(min-width: 992px)").matches;
    }

    function syncInventorySplitForViewport($split) {
        if (!isInventoryDesktopLayout()) {
            $split.removeClass("inventory-sidebar-collapsed");
            return;
        }

        var savedWidth = parseInt(localStorage.getItem("inventorySidebarWidth"), 10);
        if (savedWidth) {
            setInventorySidebarWidth($split, savedWidth);
        }

        setInventorySidebarState($split, localStorage.getItem("inventorySidebarCollapsed") === "1");
    }

    function initInventorySplitPanel() {
        var $split = $("#inventorySplit");
        var $divider = $("#inventorySplitDivider");
        var $toggle = $("#inventorySidebarToggle");

        if (!$split.length || !$divider.length || !$toggle.length) return;

        syncInventorySplitForViewport($split);

        $toggle.off("click.inventorySplit").on("click.inventorySplit", function (event) {
            event.preventDefault();
            event.stopPropagation();
            if (!isInventoryDesktopLayout()) return;
            setInventorySidebarState($split, !$split.hasClass("inventory-sidebar-collapsed"));
        });

        $divider.off(".inventorySplit").on("dblclick.inventorySplit", function () {
            if (!isInventoryDesktopLayout()) return;
            setInventorySidebarState($split, !$split.hasClass("inventory-sidebar-collapsed"));
        });

        $divider.on("mousedown.inventorySplit touchstart.inventorySplit", function (event) {
            if ($(event.target).closest("#inventorySidebarToggle").length || !isInventoryDesktopLayout()) return;

            var startX = pointerClientX(event);
            var startWidth = $("#inventorySidebarPane").outerWidth();
            var didMove = false;

            event.preventDefault();
            $("body").addClass("inventory-split-resizing");

            $(document)
                .on("mousemove.inventorySplit touchmove.inventorySplit", function (moveEvent) {
                    var nextWidth = startWidth + pointerClientX(moveEvent) - startX;

                    didMove = true;
                    setInventorySidebarState($split, false);
                    setInventorySidebarWidth($split, nextWidth);
                    moveEvent.preventDefault();
                })
                .on("mouseup.inventorySplit touchend.inventorySplit touchcancel.inventorySplit", function () {
                    $("body").removeClass("inventory-split-resizing");
                    $(document).off(".inventorySplit");

                    if (!didMove) {
                        setInventorySidebarState($split, !$split.hasClass("inventory-sidebar-collapsed"));
                    }
                });
        });

        $(window).off("resize.inventorySplit").on("resize.inventorySplit", function () {
            syncInventorySplitForViewport($split);
        });
    }

    function routeTypeFilter() {
        return normalizeInventoryValue($("#inventorySplit").data("route-type-filter"));
    }

    function syncRouteTypeFilter() {
        var type = routeTypeFilter();
        if (!type) return;

        $(".inventory-filter-check").filter(function () {
            return normalizeInventoryValue($(this).data("field")) === "TYPE" &&
                normalizeInventoryValue($(this).data("value")) === type;
        }).prop("checked", true);
    }

    window.applyInventoryPanelFilters = function () {
        var selected = selectedByField();
        var ranges = selectedRangeByField();
        var filter = normalizeInventoryValue($("#filterTerm").val());
        var visibleCount = 0;

        syncInventoryRangeLabels();
        syncInventoryModelPanel(selected);
        selected = selectedByField();

        $("#inventory > li.card").each(function () {
            var $vehicle = $(this);
            var isMatch = inventoryCheckboxMatches($vehicle, selected) &&
                inventoryRangeMatches($vehicle, ranges) &&
                inventoryTermMatches($vehicle, filter) &&
                inventoryLikedMatches($vehicle) &&
                inventoryLastMatches($vehicle);

            $vehicle.attr("data-inventory-filter-match", isMatch ? "1" : "0");
            $vehicle.toggle(isMatch);
            if (isMatch) visibleCount++;
        });

        syncInventoryVisibleCount(visibleCount);
        syncInventoryMobileFilterState(visibleCount, selected, ranges, filter);
        $(".inventory-empty-filter").toggle(visibleCount === 0);

        updateInventoryFilterCounts(selected, filter, ranges);
        applyInventoryPagination({ resetPage: true });
    };

    $(function () {
        initInventoryCardImages();

        if (!$("#inventoryFilterRail").length) {
            document.documentElement.classList.remove("inventory-page-loading");
            return;
        }

        initInventorySplitPanel();
        initInventoryPaginationControls();

        ensureInventoryDualRangeSlidersReady();
        syncInventoryRangeLabels();

        $(document).on("shown.bs.offcanvas", "#inventorySidebarPane", function () {
            ensureInventoryDualRangeSlidersReady();
        });

        $(document).on("shown.bs.collapse", ".inventory-range-filter .collapse", function () {
            ensureInventoryDualRangeSlidersReady();
        });

        $(window).off("resize.inventoryDualRanges").on("resize.inventoryDualRanges", function () {
            ensureInventoryDualRangeSlidersReady();
        });

        $(document).on("change", ".inventory-filter-check", window.applyInventoryPanelFilters);

        $(document).on("gtxrangefilterinput gtxrangefilterchange", ".inventory-range-filter", scheduleInventoryRangeFilterApply);

        $(document).on("input change", ".inventory-range-input", function () {
            if ($(this).closest(".inventory-range-filter").data("gtx.rangeFilter")) {
                return;
            }

            syncInventoryRangeLabels();
            window.applyInventoryPanelFilters();
        });

        $(document).on("input", "#filterTerm", function () {
            window.setTimeout(window.applyInventoryPanelFilters, 0);
        });

        $(document).on("click", "#filterLiked, #filterLast", function () {
            window.setTimeout(window.applyInventoryPanelFilters, 0);
        });

        $(document).on("click", "#inventoryMobileFilterToggle", function (event) {
            if ($(event.target).closest("#inventoryMobileClearFilters, #inventoryMobileCloseCanvas").length) {
                return;
            }

            var offcanvasElement = document.getElementById("inventorySidebarPane");
            if (!offcanvasElement || !window.bootstrap || !window.bootstrap.Offcanvas) return;

            window.bootstrap.Offcanvas.getOrCreateInstance(offcanvasElement).show();
        });

        $(document).on("keydown", "#inventoryMobileFilterToggle", function (event) {
            if (event.key !== "Enter" && event.key !== " ") return;
            event.preventDefault();
            $(this).trigger("click");
        });

        $(document).on("click", "#inventoryMobileClearFilters", function (event) {
            event.preventDefault();
            event.stopPropagation();
            $("#inventoryClearFilters").trigger("click");
        });

        $(document).on("click", "#inventoryMobileCloseCanvas", function (event) {
            event.preventDefault();
            event.stopPropagation();

            var offcanvasElement = document.getElementById("inventorySidebarPane");
            if (!offcanvasElement || !window.bootstrap || !window.bootstrap.Offcanvas) return;

            window.bootstrap.Offcanvas.getOrCreateInstance(offcanvasElement).hide();
        });

        $(document).on("click", "#inventoryClearFiltersDesktop", function (event) {
            event.preventDefault();
            $("#inventoryClearFilters").trigger("click");
        });

        $("#inventoryClearFilters").on("click", function () {
            var type = routeTypeFilter();
            var allInventoryUrl = $("#inventorySplit").data("all-inventory-url");

            if (type && allInventoryUrl) {
                window.location.href = allInventoryUrl;
                return;
            }

            $(".inventory-filter-check").prop("checked", false);
            resetInventoryRanges();
            window.applyInventoryPanelFilters();
        });

        syncRouteTypeFilter();
        window.applyInventoryPanelFilters();
        document.documentElement.classList.remove("inventory-page-loading");
    });
})();
