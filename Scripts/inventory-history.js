(function (window) {
    "use strict";

    function valueOf(source, name, fallback) {
        if (!source) return fallback;
        if (source[name] !== undefined && source[name] !== null) return source[name];

        var camel = name.charAt(0).toLowerCase() + name.slice(1);
        return source[camel] !== undefined && source[camel] !== null ? source[camel] : fallback;
    }

    function escapeHtml(value) {
        return (value == null ? "" : value.toString())
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function normalizeStockKey(value) {
        return (value || "").toString().trim().toUpperCase();
    }

    function statusLabel(value) {
        var label = (value || "Status").toString().trim();
        var normalized = label.toLowerCase();
        if (normalized === "skip" || normalized === "skipped" || normalized === "stall" || normalized === "stalled" || normalized === "existing") return "Stalled";
        if (normalized === "remove" || normalized === "removed") return "Sold";
        return label || "Status";
    }

    function statusClass(label) {
        var normalized = (label || "").toString().trim().toLowerCase();
        if (normalized === "added") return "is-added";
        if (normalized === "removed" || normalized === "sold") return "is-removed";
        if (normalized === "updated") return "is-updated";
        if (normalized === "purchased") return "is-purchased";
        return "";
    }

    function formatDateTime(value) {
        if (!value) return "";
        var msJsonDate = /^\/Date\((-?\d+)(?:[+-]\d+)?\)\/$/.exec(value.toString());
        var date = msJsonDate ? new Date(parseInt(msJsonDate[1], 10)) : new Date(value);
        if (Number.isNaN(date.getTime())) return "";
        return date.toLocaleString(undefined, {
            month: "short",
            day: "numeric",
            year: "numeric",
            hour: "numeric",
            minute: "2-digit"
        });
    }

    function formatDate(value) {
        var date = parseDate(value);
        if (!date) return text(value);
        return date.toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" });
    }

    function text(value) {
        return value === null || value === undefined ? "" : value.toString();
    }

    function parseDate(value) {
        if (!value) return null;
        var dateText = value.toString();
        var msJsonDate = /^\/Date\((-?\d+)(?:[+-]\d+)?\)\/$/.exec(dateText);
        var isoDate = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateText);
        var date = msJsonDate
            ? new Date(parseInt(msJsonDate[1], 10))
            : (isoDate
                ? new Date(parseInt(isoDate[1], 10), parseInt(isoDate[2], 10) - 1, parseInt(isoDate[3], 10))
                : new Date(value));
        return Number.isNaN(date.getTime()) ? null : date;
    }

    function formatShortDate(value) {
        var date = parseDate(value);
        if (!date) return text(value);
        var month = date.getMonth() + 1;
        var day = date.getDate();
        return (month < 10 ? "0" + month : month) + "/" + (day < 10 ? "0" + day : day) + "/" + date.getFullYear();
    }

    function formatInteger(value, useGrouping) {
        var raw = text(value).trim();
        if (!raw) return "";
        var numeric = Number(raw.replace(/[$,\s]/g, ""));
        if (!Number.isFinite(numeric)) return raw;
        return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0, useGrouping: useGrouping !== false }).format(numeric);
    }

    function formatCurrency(value) {
        var raw = text(value).trim();
        if (!raw) return "";
        var numeric = Number(raw.replace(/[$,\s]/g, ""));
        if (!Number.isFinite(numeric)) return raw;
        return new Intl.NumberFormat(undefined, { style: "currency", currency: "USD", maximumFractionDigits: 0 }).format(numeric);
    }

    function formatChangeValue(field, value) {
        var fieldName = text(field);
        if (fieldName === "Retail price" || fieldName === "Internet price") return formatCurrency(value);
        if (fieldName === "Mileage" || fieldName === "Weight") return formatInteger(value);
        if (fieldName === "Year" || fieldName === "Cylinders" || fieldName === "Transmission speed") return formatInteger(value, false);
        if (fieldName === "Purchase date" || fieldName === "Arrival date") return formatShortDate(value);
        return text(value);
    }

    function isNumericChangeValue(field, value) {
        var fieldName = text(field);
        if (fieldName === "Retail price" || fieldName === "Internet price" || fieldName === "Mileage" || fieldName === "Weight" || fieldName === "Year" || fieldName === "Cylinders" || fieldName === "Transmission speed") return true;
        return /^-?\$?\s*\d[\d,\s]*(?:\.\d+)?$/.test(text(value).trim());
    }

    function changeRowId(stockKey, index) {
        var normalized = (normalizeStockKey(stockKey) || "stock").replace(/[^A-Z0-9_-]/g, "-");
        return "inventoryHistoryChange-" + normalized + "-" + index;
    }

    function optionsWithDefaults(options) {
        var settings = options || {};
        return {
            escapeHtml: settings.escapeHtml || escapeHtml,
            formatDate: settings.formatDate || formatDate,
            formatDateTime: settings.formatDateTime || formatDateTime,
            formatChangeValue: settings.formatChangeValue || formatChangeValue,
            isNumericChangeValue: settings.isNumericChangeValue || isNumericChangeValue,
            statusLabel: settings.statusLabel || statusLabel,
            valueOf: Object.prototype.hasOwnProperty.call(settings, "valueOf") && typeof settings.valueOf === "function"
                ? settings.valueOf
                : valueOf
        };
    }

    function groupRowsByStockDateStatus(rows, stockKey, settings) {
        var read = settings.valueOf;
        var groups = [];
        var groupsByKey = {};

        rows.forEach(function (entry) {
            var status = settings.statusLabel(read(entry, "StatusText", "Status"));
            var explicitDateText = text(read(entry, "DateText", "")).trim();
            var created = read(entry, "DateCreated", "");
            var dateSource = explicitDateText || created;
            var date = parseDate(dateSource);
            var dateKey = date
                ? date.getFullYear() + "-" + (date.getMonth() + 1) + "-" + date.getDate()
                : "text:" + (explicitDateText || "unknown").toLowerCase();
            var entryStockKey = normalizeStockKey(stockKey || read(entry, "Stock", ""));
            var key = entryStockKey + "|" + dateKey + "|" + status.toLowerCase();
            var group = groupsByKey[key];

            if (!group) {
                group = {
                    stockKey: entryStockKey,
                    dateText: date ? settings.formatDate(dateSource) : (explicitDateText || "-"),
                    status: status,
                    entries: []
                };
                groupsByKey[key] = group;
                groups.push(group);
            }

            group.entries.push({
                entry: entry,
                updatedText: explicitDateText || settings.formatDateTime(created)
            });
        });

        return groups;
    }

    function renderChangesForEntries(items, settings) {
        var read = settings.valueOf;
        var entries = Array.isArray(items) ? items : [];
        var hasPreviousState = entries.some(function (item) {
            return read(item.entry, "HasPreviousState", false);
        });

        if (!hasPreviousState) {
            return '<div class="inventory-dashboard-change-detail-panel text-muted">No previous logged state exists for this stock number.</div>';
        }

        var rowsHtml = entries.map(function (item) {
            var changes = read(item.entry, "Changes", []);
            changes = Array.isArray(changes) ? changes : [];

            return changes.map(function (change) {
                var field = read(change, "Field", "");
                var previousValue = read(change, "PreviousValue", "");
                var currentValue = read(change, "CurrentValue", "");
                var previousClass = settings.isNumericChangeValue(field, previousValue) ? ' class="inventory-dashboard-change-number"' : "";
                var currentClass = settings.isNumericChangeValue(field, currentValue) ? ' class="inventory-dashboard-change-number"' : "";

                return "<tr>" +
                    '<td class="fw-semibold">' + settings.escapeHtml(field) + "</td>" +
                    "<td" + previousClass + ">" + settings.escapeHtml(settings.formatChangeValue(field, previousValue)) + "</td>" +
                    "<td" + currentClass + ">" + settings.escapeHtml(settings.formatChangeValue(field, currentValue)) + "</td>" +
                    "<td>" + settings.escapeHtml(item.updatedText) + "</td>" +
                    "</tr>";
            }).join("");
        }).join("");

        if (!rowsHtml) {
            return '<div class="inventory-dashboard-change-detail-panel text-muted">No field-level differences were found in the logged values.</div>';
        }

        return '<div class="inventory-dashboard-change-detail-panel">' +
            '<div class="table-responsive">' +
            '<table class="table table-sm inventory-dashboard-change-table">' +
            '<thead><tr><th>Field</th><th>Previous</th><th>New</th><th>Date updated</th></tr></thead>' +
            "<tbody>" + rowsHtml + "</tbody></table></div></div>";
    }

    function renderChanges(entry, dateText, options) {
        var settings = optionsWithDefaults(options);
        return renderChangesForEntries([{ entry: entry, updatedText: dateText }], settings);
    }

    function renderRows(history, stockKey, options) {
        var settings = optionsWithDefaults(options);
        var read = settings.valueOf;
        var rows = Array.isArray(history) ? history : [];
        if (!rows.length) return '<div class="text-muted small">No inventory history found for this stock.</div>';

        var groups = groupRowsByStockDateStatus(rows, stockKey, settings);
        var body = groups.map(function (group, index) {
            var isUpdated = group.status.toLowerCase() === "updated";
            var rowId = changeRowId(group.stockKey, index);
            var toggleHtml = isUpdated
                ? '<button type="button" class="inventory-dashboard-history-change-toggle" aria-expanded="false" aria-controls="' + settings.escapeHtml(rowId) + '" data-history-change-target="' + settings.escapeHtml(rowId) + '" title="Show updated values">' +
                  '<i class="bi bi-chevron-right" aria-hidden="true"></i><span class="visually-hidden">Show updated values</span></button>'
                : "";

            return "<tr>" +
                '<td class="inventory-dashboard-history-toggle-cell">' + toggleHtml + "</td>" +
                "<td>" + settings.escapeHtml(group.dateText || "-") + "</td>" +
                '<td><span class="inventory-dashboard-history-status ' + statusClass(group.status) + '">' + settings.escapeHtml(group.status) + "</span></td></tr>" +
                (isUpdated
                    ? '<tr id="' + settings.escapeHtml(rowId) + '" class="inventory-dashboard-history-change-row d-none"><td class="inventory-dashboard-change-detail-cell" colspan="3">' + renderChangesForEntries(group.entries, settings) + "</td></tr>"
                    : "");
        }).join("");

        return '<div class="table-responsive"><table class="table inventory-dashboard-history-table">' +
            '<thead><tr><th class="inventory-dashboard-history-toggle-cell"></th><th>Date</th><th>Status</th></tr></thead>' +
            "<tbody>" + body + "</tbody></table></div>";
    }

    function renderPanel(stockKey, history, options) {
        var normalized = normalizeStockKey(stockKey);
        var content = Array.isArray(history)
            ? renderRows(history, normalized, options)
            : '<div class="text-muted small">Loading history...</div>';
        return '<div class="inventory-dashboard-history-panel"><div class="inventory-dashboard-history-content" data-stock-key="' + escapeHtml(normalized) + '">' + content + "</div></div>";
    }

    window.gtxInventoryHistory = {
        normalizeStockKey: normalizeStockKey,
        renderChanges: renderChanges,
        renderPanel: renderPanel,
        renderRows: renderRows
    };
})(window);
