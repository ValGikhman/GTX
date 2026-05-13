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
    const filter = term.trim().toUpperCase();

    if (filter === undefined || filter === "" || filter === null) {
        sessionStorage.removeItem("term");
    }

    if (filter == "@") return;

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

        if (filter.startsWith("@@")) {
            // Hidden  features
            $("#filterTerm").addClass("text-info").addClass("border-info");

            // Map prefix to data
            const prefixMap = {
                "@@YR": `@@YR ${year}`,
                "@@MK": `@@MK ${make}`,
                "@@MD": `@@MD ${model}`,
                "@@TR": `@@TR ${transmission}`,
                "@@CY": `@@CY ${cylinders}`,
                "@@OW": `@@OW ${location}`
            };

            combined = `@@${story} @@${images} @@${dataone}`;

            // Override combined if matching a special prefix
            for (const key in prefixMap) {
                if (filter.startsWith(key)) {
                    combined = prefixMap[key];
                    break;
                }
            }
        }
        else {
            // Normal search
            $("#filterTerm").removeClass("text-info").removeClass("border-info");
            combined = `${stock} ${vin} ${make} ${model} ${style} ${type} ${transmission} ${year} ${color} ${color2}`;
        }

        if (combined.includes(filter)) {
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

    sessionStorage.setItem("term", term);
}

function applyFilterLiked() {
    const items = document.querySelectorAll("#inventory > li");
    updateFilterLiked();
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
}

function applyFilterLast() {
    updateFilterLast();
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
            var max = parseInventoryNumber($panel.find(".inventory-range-input").val());

            if (!field || max === null || minLimit === null || maxLimit === null) return;

            ranges[field] = {
                min: minLimit,
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

            if (value === null || value > range.max) {
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
            var maxLimit = parseInventoryNumber($panel.data("range-max"));
            var $input = $panel.find(".inventory-range-input");
            var max = parseInventoryNumber($input.val());

            if (max === null || maxLimit === null) return;

            if (max > maxLimit) {
                max = maxLimit;
                $input.val(max);
            }

            var maxText = formatInventoryRangeValue(max, format);

            $panel.find("[data-range-value-label='" + field + "']").text(maxText);
        });
    }

    function resetInventoryRanges() {
        $(".inventory-range-filter").each(function () {
            var $panel = $(this);

            $panel.find(".inventory-range-input").val($panel.data("range-max"));
        });

        syncInventoryRangeLabels();
    }

    function inventoryTermMatches($vehicle, filter) {
        if (!filter) return true;
        if (filter === "@@") return false;

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
                normalizeInventoryValue($vehicle.data("body"))
            ].join(" ");
        }

        return combined.indexOf(filter) !== -1;
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

    function activeInventoryFilterCount(selected, ranges, filter) {
        var count = filter ? 1 : 0;

        $.each(selected, function (_, values) {
            count += values.length;
        });

        $.each(ranges, function (_, range) {
            if (range.max < range.maxLimit) {
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

            $vehicle.toggle(isMatch);
            if (isMatch) visibleCount++;
        });

        syncInventoryVisibleCount(visibleCount);
        syncInventoryMobileFilterState(visibleCount, selected, ranges, filter);
        $(".inventory-empty-filter").toggle(visibleCount === 0);

        var stocks = $(".card:visible").map(function () {
            return $(this).data("stock");
        }).get();

        if (stocks.length > 0) {
            localStorage.setItem("matchedStocks", JSON.stringify(stocks));
        } else {
            localStorage.removeItem("matchedStocks");
        }

        updateInventoryFilterCounts(selected, filter, ranges);
    };

    $(function () {
        if (!$("#inventoryFilterRail").length) return;

        initInventorySplitPanel();

        syncInventoryRangeLabels();

        $(document).on("change", ".inventory-filter-check", window.applyInventoryPanelFilters);

        $(document).on("input change", ".inventory-range-input", function () {
            syncInventoryRangeLabels();
            window.applyInventoryPanelFilters();
        });

        $(document).on("input", "#filterTerm", function () {
            window.setTimeout(window.applyInventoryPanelFilters, 0);
        });

        $(document).on("click", "#filterLiked, #filterLast", function () {
            window.setTimeout(window.applyInventoryPanelFilters, 0);
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
    });
})();
