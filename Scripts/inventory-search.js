(function (window, $) {
    "use strict";

    var termStorageKey = "term";

    function rawText(value) {
        if (value === null || value === undefined) return "";
        return value.toString();
    }

    function trimText(value) {
        var text = rawText(value);
        return $ && $.trim ? $.trim(text) : text.trim();
    }

    function normalize(value) {
        return trimText(value).replace(/\s+/g, " ").toUpperCase();
    }

    function splitTerms(value) {
        var normalized = normalize(value);
        return normalized ? normalized.split(/\s+/) : [];
    }

    function containsAllTerms(haystack, terms) {
        var normalizedHaystack = normalize(haystack);
        var normalizedTerms = Array.isArray(terms) ? terms : splitTerms(terms);

        if (!normalizedTerms.length) return true;

        for (var i = 0; i < normalizedTerms.length; i++) {
            if (normalizedHaystack.indexOf(normalizedTerms[i]) === -1) {
                return false;
            }
        }

        return true;
    }

    function firstValue(source, names) {
        if (!source) return "";

        for (var i = 0; i < names.length; i++) {
            var name = names[i];
            if (source[name] !== undefined && source[name] !== null) {
                return source[name];
            }
        }

        return "";
    }

    function valuesText(values) {
        return normalize((values || []).map(rawText).join(" "));
    }

    function vehicleObject(fields, overrides) {
        var vehicle = {
            Stock: firstValue(fields, ["stock", "Stock"]),
            VIN: firstValue(fields, ["vin", "VIN"]),
            DataOne: firstValue(fields, ["dataone", "DataOne", "DataOneStatus"]),
            Year: firstValue(fields, ["year", "Year"]),
            Make: firstValue(fields, ["make", "Make"]),
            Model: firstValue(fields, ["model", "Model"]),
            Trim: firstValue(fields, ["trim", "Trim"]),
            VehicleStyle: firstValue(fields, ["style", "Style", "vehicleStyle", "VehicleStyle"]),
            VehicleType: firstValue(fields, ["type", "Type", "vehicleType", "VehicleType"]),
            Transmission: firstValue(fields, ["transmission", "Transmission"]),
            TransmissionSpeed: firstValue(fields, ["transmissionSpeed", "TransmissionSpeed"]),
            Cylinders: firstValue(fields, ["cylinders", "Cylinders"]),
            FuelType: firstValue(fields, ["fuel", "Fuel", "fuelType", "FuelType"]),
            DriveTrain: firstValue(fields, ["drive", "Drive", "driveType", "DriveType", "driveTrain", "DriveTrain"]),
            Body: firstValue(fields, ["body", "Body", "bodyType", "BodyType"]),
            Engine: firstValue(fields, ["engine", "Engine"]),
            Color: firstValue(fields, ["color", "Color"]),
            Color2: firstValue(fields, ["color2", "Color2"]),
            LocationCode: firstValue(fields, ["location", "Location", "locationCode", "LocationCode"]),
            Story: firstValue(fields, ["story", "Story"]),
            Images: firstValue(fields, ["images", "Images"]),
            Status: firstValue(fields, ["status", "Status"]),
            StatusText: firstValue(fields, ["statusText", "StatusText"]),
            DaysInInventory: firstValue(fields, ["days", "Days", "daysInInventory", "DaysInInventory"]),
            InventoryStartSource: firstValue(fields, ["inventoryStartSource", "InventoryStartSource"]),
            SetToUpload: firstValue(fields, ["setToUpload", "SetToUpload"])
        };

        if (overrides) {
            for (var key in overrides) {
                if (Object.prototype.hasOwnProperty.call(overrides, key)) {
                    vehicle[key] = overrides[key];
                }
            }
        }

        return vehicle;
    }

    function vehiclePlainValues(fields, extraValues) {
        var vehicle = vehicleObject(fields);
        var values = [
            vehicle.Stock,
            vehicle.VIN,
            vehicle.DataOne,
            vehicle.Year,
            vehicle.Make,
            vehicle.Model,
            vehicle.Trim,
            vehicle.VehicleStyle,
            vehicle.VehicleType,
            vehicle.Transmission,
            vehicle.TransmissionSpeed,
            vehicle.Cylinders,
            vehicle.FuelType,
            vehicle.DriveTrain,
            vehicle.Body,
            vehicle.Engine,
            vehicle.Color,
            vehicle.Color2,
            vehicle.LocationCode,
            vehicle.Story,
            vehicle.Images,
            vehicle.Status,
            vehicle.StatusText,
            vehicle.DaysInInventory,
            vehicle.InventoryStartSource,
            vehicle.SetToUpload
        ];

        return values.concat(extraValues || []);
    }

    function vehicleTokenValues(fields) {
        var vehicle = vehicleObject(fields);
        var dataOne = vehicle.DataOne;
        var story = vehicle.Story;
        var images = vehicle.Images;

        return [
            "@@YR " + rawText(vehicle.Year),
            "@@MK " + rawText(vehicle.Make),
            "@@MD " + rawText(vehicle.Model),
            "@@TR " + rawText(vehicle.Transmission),
            "@@CY " + rawText(vehicle.Cylinders),
            "@@OW " + rawText(vehicle.LocationCode),
            story ? "@@" + rawText(story) : "",
            images ? "@@" + rawText(images) : "",
            dataOne ? "@@" + rawText(dataOne) : ""
        ];
    }

    function buildVehicleSearch(fields, extraValues) {
        return {
            text: valuesText(vehiclePlainValues(fields, extraValues)),
            tokens: valuesText(vehicleTokenValues(fields))
        };
    }

    function matches(searchText, query, tokenSearchText) {
        var terms = splitTerms(query);
        var firstTerm = terms.length ? terms[0] : "";
        var haystack = firstTerm.indexOf("@@") === 0 ? tokenSearchText : searchText;

        if (!terms.length) return true;
        if (terms.length === 1 && firstTerm === "@@") return false;

        return containsAllTerms(haystack || "", terms);
    }

    function vehicleMatches(fields, query, extraValues) {
        var search = buildVehicleSearch(fields, extraValues);
        return matches(search.text, query, search.tokens);
    }

    function readStoredTerm() {
        try {
            return window.sessionStorage.getItem(termStorageKey) || "";
        } catch (error) {
            return "";
        }
    }

    function writeStoredTerm(term) {
        var raw = rawText(term);

        try {
            if (normalize(raw)) {
                window.sessionStorage.setItem(termStorageKey, raw);
            } else {
                window.sessionStorage.removeItem(termStorageKey);
            }
        } catch (error) {
            return;
        }
    }

    window.gtxInventorySearch = {
        termStorageKey: termStorageKey,
        text: rawText,
        normalize: normalize,
        splitTerms: splitTerms,
        containsAllTerms: containsAllTerms,
        valuesText: valuesText,
        vehicleObject: vehicleObject,
        buildVehicleSearch: buildVehicleSearch,
        matches: matches,
        vehicleMatches: vehicleMatches,
        readStoredTerm: readStoredTerm,
        writeStoredTerm: writeStoredTerm
    };
})(window, window.jQuery);
