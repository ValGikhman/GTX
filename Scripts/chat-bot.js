(function ($, window, document) {
    "use strict";

    $(function () {
        var $widget = $("#gtxChatWidget");
        if (!$widget.length) return;

        var $launcher = $widget.find(".gtx-chat-launcher");
        var $panel = $widget.find("#gtxChatPanel");
        var $messages = $widget.find("[data-chat-messages]");
        var $chatForm = $widget.find("[data-chat-form]");
        var $message = $widget.find("#gtxChatMessage");
        var $commandGuide = $widget.find("#gtxChatCommandGuide");
        var $commandGuideButton = $widget.find("[data-chat-help]");
        var $lead = $widget.find("[data-chat-lead]");
        var $leadForm = $widget.find("[data-chat-lead-form]");
        var $leadFeedback = $widget.find("[data-chat-lead-feedback]");
        var $salesperson = $leadForm.find("[name='EmployerId']");
        var chatStorageKey = "gtx-chat-session-v1";
        var requestHistoryStorageKey = "gtx-chat-request-history-v1";
        var chatStorageLifetime = 60 * 60 * 1000;
        var maximumStoredMessages = 40;
        var maximumStoredRequests = 20;
        var responseId = null;
        var sending = false;
        var salespeopleLoaded = false;
        var salespeopleRequest = null;
        var salespeople = [];
        var initialGreeting = $.trim($messages.find(".gtx-chat-message.is-assistant").first().text());
        var conversation = initialGreeting
            ? [{ role: "assistant", text: initialGreeting, extraClass: "", vehicles: [], totalMatches: null }]
            : [];
        var requestHistory = loadRequestHistory();
        var requestHistoryIndex = -1;
        var requestHistoryDraft = "";
        var applyingRequestHistory = false;

        function loadRequestHistory() {
            try {
                var stored = JSON.parse(window.sessionStorage.getItem(requestHistoryStorageKey) || "[]");
                if (!Array.isArray(stored)) return [];

                return $.map(stored.slice(-maximumStoredRequests), function (item) {
                    var value = $.trim(String(item || ""));
                    return value && value.length <= 800 ? value : null;
                });
            } catch (_) {
                return [];
            }
        }

        function rememberRequest(text) {
            requestHistory.push(text);
            requestHistory = requestHistory.slice(-maximumStoredRequests);
            requestHistoryIndex = -1;
            requestHistoryDraft = "";

            try {
                window.sessionStorage.setItem(requestHistoryStorageKey, JSON.stringify(requestHistory));
            } catch (_) {
                // History remains available in memory when session storage is unavailable.
            }
        }

        function showRequestHistoryValue(value) {
            applyingRequestHistory = true;
            $message.val(value).trigger("input");
            applyingRequestHistory = false;

            var input = $message.get(0);
            if (input && typeof input.setSelectionRange === "function") {
                input.setSelectionRange(input.value.length, input.value.length);
            }
        }

        function moveThroughRequestHistory(direction) {
            if (!requestHistory.length) return;

            if (requestHistoryIndex === -1) {
                requestHistoryDraft = $message.val();
                requestHistoryIndex = direction < 0 ? requestHistory.length - 1 : 0;
                showRequestHistoryValue(requestHistory[requestHistoryIndex]);
                return;
            }

            requestHistoryIndex += direction;
            if (requestHistoryIndex < 0 || requestHistoryIndex >= requestHistory.length) {
                requestHistoryIndex = -1;
                showRequestHistoryValue(requestHistoryDraft);
                return;
            }

            showRequestHistoryValue(requestHistory[requestHistoryIndex]);
        }

        function removeStoredChat() {
            try {
                window.sessionStorage.removeItem(chatStorageKey);
            } catch (_) {
                // Storage can be unavailable in strict privacy modes.
            }
        }

        function saveChat() {
            try {
                window.sessionStorage.setItem(chatStorageKey, JSON.stringify({
                    savedAt: Date.now(),
                    responseId: responseId,
                    isOpen: !$panel.prop("hidden"),
                    messages: conversation.slice(-maximumStoredMessages)
                }));
            } catch (_) {
                // Keep the chatbot usable when storage is unavailable or full.
            }
        }

        function loadStoredChat() {
            try {
                var raw = window.sessionStorage.getItem(chatStorageKey);
                if (!raw) return null;

                var state = JSON.parse(raw);
                if (!state || !state.savedAt || Date.now() - Number(state.savedAt) > chatStorageLifetime) {
                    removeStoredChat();
                    return null;
                }

                var messages = Array.isArray(state.messages) ? state.messages.slice(-maximumStoredMessages) : [];
                state.messages = $.map(messages, function (item) {
                    if (!item || (item.role !== "assistant" && item.role !== "user")) return null;
                    var extraClass = /^(is-error|is-warning)$/.test(item.extraClass || "") ? item.extraClass : "";
                    return {
                        role: item.role,
                        text: String(item.text || ""),
                        extraClass: extraClass,
                        vehicles: Array.isArray(item.vehicles) ? item.vehicles.slice(0, 5) : [],
                        totalMatches: item.totalMatches == null ? null : Number(item.totalMatches)
                    };
                });
                state.responseId = /^resp_[A-Za-z0-9_-]+$/.test(state.responseId || "") ? state.responseId : null;
                state.isOpen = state.isOpen === true;
                return state;
            } catch (_) {
                removeStoredChat();
                return null;
            }
        }

        function openChat() {
            $panel.prop("hidden", false);
            $launcher.attr("aria-expanded", "true").addClass("d-none");
            saveChat();
            window.setTimeout(function () { $message.trigger("focus"); }, 50);
        }

        function closeChat() {
            closeCommandGuide(false);
            $panel.prop("hidden", true);
            $launcher.attr("aria-expanded", "false").removeClass("d-none").trigger("focus");
            saveChat();
        }

        function openCommandGuide() {
            $commandGuide.prop("hidden", false);
            $commandGuideButton.attr("aria-expanded", "true");
            $commandGuide.find("[data-chat-help-close]").trigger("focus");
        }

        function closeCommandGuide(restoreFocus) {
            $commandGuide.prop("hidden", true);
            $commandGuideButton.attr("aria-expanded", "false");
            if (restoreFocus !== false) $commandGuideButton.trigger("focus");
        }

        function scrollMessages() {
            var element = $messages.get(0);
            if (element) element.scrollTop = element.scrollHeight;
        }

        function appendLinkedText($container, value) {
            var text = String(value || "");
            var expression = /(https?:\/\/[^\s]+)/g;
            var lastIndex = 0;
            var match;
            while ((match = expression.exec(text)) !== null) {
                $container.append(document.createTextNode(text.substring(lastIndex, match.index)));
                var cleanUrl = match[0].replace(/[),.;]+$/, "");
                var trailing = match[0].substring(cleanUrl.length);
                $("<a>", { href: cleanUrl, target: "_blank", rel: "noopener", text: "View vehicle" }).appendTo($container);
                if (trailing) $container.append(document.createTextNode(trailing));
                lastIndex = expression.lastIndex;
            }
            $container.append(document.createTextNode(text.substring(lastIndex)));
        }

        function appendRichText($container, value) {
            var text = String(value || "");
            var expression = /\*\*([^*]+)\*\*|\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)|(https?:\/\/[^\s]+)/g;
            var lastIndex = 0;
            var match;

            while ((match = expression.exec(text)) !== null) {
                $container.append(document.createTextNode(text.substring(lastIndex, match.index)));

                if (match[1]) {
                    $("<strong>", { text: match[1] }).appendTo($container);
                } else {
                    var url = match[3] || match[4];
                    var cleanUrl = url.replace(/[),.;]+$/, "");
                    var label = match[2] || "View vehicle";
                    var $link = $("<a>", {
                        "class": "gtx-chat-vehicle-link",
                        href: cleanUrl,
                        target: "_blank",
                        rel: "noopener",
                        title: label
                    }).appendTo($container);
                    $("<i>", { "class": "bi bi-car-front-fill", "aria-hidden": "true" }).appendTo($link);
                    $("<span>", { text: label }).appendTo($link);
                }

                lastIndex = expression.lastIndex;
            }

            $container.append(document.createTextNode(text.substring(lastIndex)));
        }

        function appendAssistantText($container, value) {
            var lines = String(value || "").replace(/\r\n/g, "\n").split("\n");
            var $vehicleCard = null;
            var hasVehicleCards = false;

            $.each(lines, function (_, rawLine) {
                var line = $.trim(rawLine);
                if (!line) {
                    $vehicleCard = null;
                    return;
                }

                var vehicleHeading = line.match(/^(?:\d+\.\s*)?(\*\*.+?\*\*)(?:\s+(?:is|with)\b.*)?[:.]?$/i);
                var vehicleTitle = vehicleHeading && vehicleHeading[1];
                if (!vehicleTitle) {
                    var proseHeading = line.match(/^we (?:have|found)(?:\s+an?)?\s+((?:19|20)\d{2}\s+.*?)(?=\s+with\s+\d+\s+cyl|\s+available|[:.]?$)/i);
                    vehicleTitle = proseHeading && proseHeading[1];
                }

                if (vehicleTitle) {
                    $vehicleCard = $("<section>", { "class": "gtx-chat-vehicle-card" });
                    var $heading = $("<div>", { "class": "gtx-chat-vehicle-title" }).appendTo($vehicleCard);
                    appendRichText($heading, vehicleTitle);
                    $container.append($vehicleCard);
                    hasVehicleCards = true;
                    return;
                }

                if ($vehicleCard && /^[-\u2022]\s*/.test(line)) {
                    var detail = line.replace(/^[-\u2022]\s*/, "");
                    var detailClass = /^price\s*:/i.test(detail) ? " is-price" : "";
                    var $detail = $("<div>", { "class": "gtx-chat-vehicle-detail" + detailClass }).appendTo($vehicleCard);
                    appendRichText($detail, detail);
                    return;
                }

                $vehicleCard = null;
                var $copy = $("<div>", { "class": "gtx-chat-copy" }).appendTo($container);
                appendRichText($copy, line);
            });

            if (hasVehicleCards) $container.addClass("has-vehicle-results");
        }

        function resultValue(vehicle, pascalName, camelName) {
            if (!vehicle) return null;
            return vehicle[pascalName] != null ? vehicle[pascalName] : vehicle[camelName];
        }

        function formatWholeNumber(value) {
            var number = Number(value || 0);
            return number.toLocaleString("en-US", { maximumFractionDigits: 0 });
        }

        function appendVehicleDetail($card, iconClass, label, value, isPrice) {
            var $detail = $("<div>", { "class": "gtx-chat-vehicle-detail" + (isPrice ? " is-price" : "") }).appendTo($card);
            $("<i>", { "class": "bi " + iconClass, "aria-hidden": "true" }).appendTo($detail);
            $("<span>", { "class": "gtx-chat-vehicle-detail-label", text: label + ":" }).appendTo($detail);
            $("<span>", { text: value }).appendTo($detail);
        }

        function appendStructuredVehicles($container, vehicles, totalMatches) {
            var total = Number(totalMatches);
            if (!Number.isFinite(total)) total = vehicles.length;

            var intro = total > vehicles.length
                ? "I found " + formatWholeNumber(total) + " matching vehicles. Here are the first " + vehicles.length + ":"
                : vehicles.length === 1
                    ? "I found this vehicle:"
                    : "I found " + vehicles.length + " matching vehicles:";
            $("<div>", { "class": "gtx-chat-copy", text: intro }).appendTo($container);

            $.each(vehicles, function (_, vehicle) {
                var title = resultValue(vehicle, "Title", "title") || "Vehicle";
                var stock = resultValue(vehicle, "Stock", "stock");
                var mileage = resultValue(vehicle, "Mileage", "mileage");
                var cylinders = Number(resultValue(vehicle, "Cylinders", "cylinders") || 0);
                var advertisedPrice = resultValue(vehicle, "AdvertisedPrice", "advertisedPrice");
                var documentaryFee = resultValue(vehicle, "DocumentaryFee", "documentaryFee");
                var totalPrice = resultValue(vehicle, "PriceWithDocumentaryFee", "priceWithDocumentaryFee");
                var url = resultValue(vehicle, "Url", "url");
                var $card = $("<section>", { "class": "gtx-chat-vehicle-card" }).appendTo($container);

                $("<div>", { "class": "gtx-chat-vehicle-title", text: title }).appendTo($card);
                if (stock) appendVehicleDetail($card, "bi-hash", "Stock", stock, false);
                appendVehicleDetail($card, "bi-speedometer2", "Mileage", formatWholeNumber(mileage) + " miles", false);
                if (cylinders > 0) appendVehicleDetail($card, "bi-gear-wide-connected", "Engine", cylinders + " cylinders", false);
                appendVehicleDetail(
                    $card,
                    "bi-receipt",
                    "Price",
                    "$" + formatWholeNumber(advertisedPrice) + " + $" + formatWholeNumber(documentaryFee)
                        + " documentary fee = $" + formatWholeNumber(totalPrice),
                    true);

                if (url && /^https?:\/\//i.test(url)) {
                    var $link = $("<a>", {
                        "class": "gtx-chat-vehicle-link",
                        href: url,
                        target: "_blank",
                        rel: "noopener",
                        title: "View details for " + title
                    }).appendTo($card);
                    $("<i>", { "class": "bi bi-car-front-fill", "aria-hidden": "true" }).appendTo($link);
                    $("<span>", { text: "View Details" }).appendTo($link);
                }
            });

            $container.addClass("has-vehicle-results");
        }

        function addMessage(role, text, extraClass, vehicles, totalMatches, persist) {
            var $item = $("<div>", { "class": "gtx-chat-message is-" + role + (extraClass ? " " + extraClass : "") });
            if (role === "assistant" && vehicles && vehicles.length) {
                appendStructuredVehicles($item, vehicles, totalMatches);
            } else if (role === "assistant") {
                appendAssistantText($item, text);
            } else {
                appendLinkedText($item, text);
            }
            if (extraClass === "is-thinking") {
                $("<span>", {
                    "class": "spinner-border spinner-border-sm gtx-chat-thinking-spinner",
                    "aria-hidden": "true"
                }).appendTo($item.find(".gtx-chat-copy").last());
            }
            $messages.append($item);
            scrollMessages();
            if (persist !== false && extraClass !== "is-thinking") {
                conversation.push({
                    role: role,
                    text: String(text || ""),
                    extraClass: extraClass || "",
                    vehicles: vehicles && vehicles.length ? vehicles.slice(0, 5) : [],
                    totalMatches: totalMatches == null ? null : Number(totalMatches)
                });
                conversation = conversation.slice(-maximumStoredMessages);
                saveChat();
            }
            return $item;
        }

        function errorMessage(xhr, fallback) {
            var body = xhr && xhr.responseJSON;
            return (body && (body.reply || body.Reply || body.message || body.Message)) || fallback;
        }

        function setSending(value) {
            sending = value;
            $chatForm.find("button, textarea").prop("disabled", value);
            $widget.find("[data-chat-prompt]").prop("disabled", value);
            $messages.attr("aria-busy", value ? "true" : "false");
        }

        function requestsContactForm(text) {
            var normalized = $.trim(text || "").toLowerCase();
            return normalized.length <= 100
                && (/\b(contact(?:s|ing)?|e-?mails?|emailing|messages?|messaging|reach|talk|speak|connect|write)\b/.test(normalized)
                    || /\bget\s+in\s+touch\b/.test(normalized));
        }

        function normalizedNavigationText(text) {
            return $.trim(String(text || "").toLowerCase().replace(/[^a-z0-9$,.]+/g, " "));
        }

        function hasNavigationVerb(text) {
            return /\b(take|go|open|navigate|send|bring|show|visit|view|reset|clear|remove)\b/.test(text);
        }

        function requestedMaximumPrice(text) {
            if (!/\b(under|below|less than|up to|maximum|max)\b/.test(text)) return null;

            var match = text.match(/\b(?:under|below|less than|up to|maximum|max)\b(?:\s+price(?:d)?)?\s*(?:of\s*)?\$?\s*(\d{1,3}(?:,\d{3})+|\d+(?:\.\d+)?)\s*(k|thousand)?\b/);
            if (!match) return null;

            var amount = Number(match[1].replace(/,/g, ""));
            if (match[2]) amount *= 1000;
            amount = Math.round(amount);
            return amount > 0 && amount <= 10000000 ? amount : null;
        }

        function requestedMaximumYear(text) {
            var match = text.match(/\b(?:under|before|older than)\s+(?:model year\s+|year\s+)?((?:19|20)\d{2})\b/);
            if (!match) return null;

            var exclusiveYear = Number(match[1]) - 1;
            var latestReasonableYear = new Date().getFullYear() + 2;
            return exclusiveYear >= 1886 && exclusiveYear <= latestReasonableYear ? exclusiveYear : null;
        }

        function requestedInventoryMake(text) {
            var match = text.match(/\binventory\s+(?:for|of)\s+([a-z][a-z0-9 .'-]*?)(?:\s+(?:under|before|older than|from|after|over|with)\b|$)/);
            if (!match) return null;

            var make = $.trim(match[1]);
            return make && make.length <= 40 ? make : null;
        }

        function withMaximumPrice(url, maximumPrice) {
            if (!maximumPrice) return url;
            var target = new URL(url, window.location.origin);
            target.searchParams.set("maximumPrice", maximumPrice);
            return target.pathname + target.search + target.hash;
        }

        function withInventoryFilters(url, make, maximumYear) {
            var target = new URL(url, window.location.origin);
            if (make) target.searchParams.set("make", make);
            if (maximumYear) target.searchParams.set("maximumYear", maximumYear);
            return target.pathname + target.search + target.hash;
        }

        function recentVehicleDestination() {
            for (var index = conversation.length - 1; index >= 0; index -= 1) {
                var vehicles = conversation[index].vehicles;
                if (!vehicles || !vehicles.length) continue;
                if (vehicles.length !== 1) return { ambiguous: true };

                var url = resultValue(vehicles[0], "Url", "url");
                var title = resultValue(vehicles[0], "Title", "title") || "vehicle";
                if (!url) return null;

                try {
                    var target = new URL(url, window.location.origin);
                    if (target.origin !== window.location.origin) return null;
                    return { url: target.pathname + target.search + target.hash, label: title };
                } catch (_) {
                    return null;
                }
            }
            return null;
        }

        function navigationRequest(text) {
            var normalized = normalizedNavigationText(text);
            var hasVerb = hasNavigationVerb(normalized);
            var maximumPrice = requestedMaximumPrice(normalized);
            var maximumYear = requestedMaximumYear(normalized);
            var inventoryMake = requestedInventoryMake(normalized);
            var destination;

            if (/\b(reset|clear|remove)\b.*\b(?:inventory\s+)?filters?\b/.test(normalized)) {
                destination = { url: $widget.data("inventory-url"), label: "inventory with filters cleared" };
            } else if (/\b(this|that|the) vehicle\b/.test(normalized) && /\b(open|view|show|take|go)\b/.test(normalized)) {
                var vehicleDestination = recentVehicleDestination();
                if (vehicleDestination && vehicleDestination.ambiguous) return { ambiguousVehicle: true };
                return vehicleDestination || { missingVehicle: true };
            } else if (/\b(inventory dashboard|dashboard)\b/.test(normalized)) {
                destination = { url: $widget.data("dashboard-url"), label: "inventory dashboard", requiredRole: "owner" };
            } else if (/\b(inventory upload|upload inventory|inventory management)\b/.test(normalized)) {
                destination = { url: $widget.data("inventory-management-url"), label: "inventory management", requiredRole: "owner" };
            } else if (/\b(majordome|manage vehicles?|vehicle management|edit inventory)\b/.test(normalized)) {
                destination = { url: $widget.data("majordome-url"), label: "vehicle management", requiredRole: "admin" };
            } else if (/\b(employee management|manage employees?|edit employees?)\b/.test(normalized)) {
                destination = { url: $widget.data("employees-url"), label: "employee management", requiredRole: "admin" };
            } else if (/\b(vin decoder|decode vin)\b/.test(normalized)) {
                destination = { url: $widget.data("vin-decoder-url"), label: "VIN decoder", requiredRole: "admin" };
            } else if (/\b(announcement management|manage announcements?|edit announcements?)\b/.test(normalized)) {
                destination = { url: $widget.data("announcements-url"), label: "announcement management", requiredRole: "admin" };
            } else if (/\b(blog management|manage blogs?|edit blogs?)\b/.test(normalized)) {
                destination = { url: $widget.data("blog-management-url"), label: "blog management", requiredRole: "admin" };
            } else if (/\b(test drive|schedule (?:a )?drive)\b/.test(normalized)) {
                destination = { url: $widget.data("test-drive-url"), label: "test-drive page" };
            } else if (/\b(financing|finance application|financing application|credit application|loan application|apply for (?:a )?(?:loan|financing))\b/.test(normalized)) {
                destination = { url: $widget.data("financing-url"), label: "financing application" };
            } else if (/\b(staff|staff page|our team|team page)\b/.test(normalized)) {
                destination = { url: $widget.data("staff-url"), label: "staff page" };
            } else if (/\btestimonials?\b/.test(normalized)) {
                destination = { url: $widget.data("testimonials-url"), label: "testimonials" };
            } else if (/\b(?:customer )?blogs?(?: page)?\b/.test(normalized)) {
                destination = { url: $widget.data("blog-url"), label: "blog" };
            } else if (/\bprivacy(?: policy)?\b/.test(normalized)) {
                destination = { url: $widget.data("privacy-url"), label: "privacy policy" };
            } else if (/\bterms(?: and conditions| of use)?\b/.test(normalized)) {
                destination = { url: $widget.data("terms-url"), label: "terms and conditions" };
            } else if (/\babout(?: us)?(?: page)?\b/.test(normalized)) {
                destination = { url: $widget.data("about-url"), label: "About Us page" };
            } else if (/\b(suvs?|sport utility vehicles?)\b/.test(normalized)) {
                destination = { url: withMaximumPrice($widget.data("suvs-url"), maximumPrice), label: "SUV inventory" };
            } else if (/\bsedans?\b/.test(normalized)) {
                destination = { url: withMaximumPrice($widget.data("sedans-url"), maximumPrice), label: "sedan inventory" };
            } else if (/\bwagons?\b/.test(normalized)) {
                destination = { url: withMaximumPrice($widget.data("wagons-url"), maximumPrice), label: "wagon inventory" };
            } else if (/\btrucks?\b/.test(normalized)) {
                destination = { url: withMaximumPrice($widget.data("trucks-url"), maximumPrice), label: "truck inventory" };
            } else if (/\bvans?\b/.test(normalized)) {
                destination = { url: withMaximumPrice($widget.data("vans-url"), maximumPrice), label: "van inventory" };
            } else if (/\bconvertibles?\b/.test(normalized)) {
                destination = { url: withMaximumPrice($widget.data("convertibles-url"), maximumPrice), label: "convertible inventory" };
            } else if (/\bhatchbacks?\b/.test(normalized)) {
                destination = { url: withMaximumPrice($widget.data("hatchbacks-url"), maximumPrice), label: "hatchback inventory" };
            } else if (/\bcoupes?\b/.test(normalized)) {
                destination = { url: withMaximumPrice($widget.data("coupes-url"), maximumPrice), label: "coupe inventory" };
            } else if (/\b(inventory|vehicles? for sale|all vehicles?)\b/.test(normalized)) {
                destination = {
                    url: withInventoryFilters($widget.data("inventory-url"), inventoryMake, maximumYear),
                    label: inventoryMake || maximumYear ? "filtered inventory" : "inventory"
                };
            } else if (/\b(home|home page|homepage)\b/.test(normalized)) {
                destination = { url: $widget.data("home-url"), label: "home page" };
            }

            if (!destination) return null;
            if (hasVerb) return destination;

            var exactDestination = /^(?:the |gtx )?(?:home|home page|homepage|inventory|dashboard|staff|staff page|financing|financing application|suvs?|sedans?|wagons?|trucks?|vans?|convertibles?|hatchbacks?|coupes?)$/;
            return exactDestination.test(normalized) ? destination : null;
        }

        function hasNavigationAccess(requiredRole) {
            if (!requiredRole) return true;
            var role = $.trim(String(window.gtx && window.gtx.currentRole || "user")).toLowerCase();
            return requiredRole === "owner" ? role === "owner" : role !== "" && role !== "user";
        }

        function openNavigationDestination(destination) {
            if (destination.ambiguousVehicle) {
                addMessage("assistant", "I found more than one vehicle in the latest results. Please use the View Details button on the vehicle you want.");
                return;
            }

            if (!destination.url) {
                addMessage("assistant", "I do not have a vehicle to open yet. Ask me to find one first.");
                return;
            }

            if (destination.requiredRole && !hasNavigationAccess(destination.requiredRole)) {
                addMessage("assistant", "Please log in to open " + destination.label + ". After login, I'll take you there.");
                window.gtxReturnUrl = destination.url;
                window.gtxRequiredRole = destination.requiredRole;
                if (typeof window.getLoginModal === "function" && $("#loginModal").length) {
                    window.getLoginModal().show();
                } else {
                    window.location.assign(destination.url);
                }
                return;
            }

            addMessage("assistant", "Opening " + destination.label + "...", "is-thinking", null, null, false);
            window.setTimeout(function () {
                window.location.assign(destination.url);
            }, 150);
        }

        function sendMessage(text) {
            text = $.trim(text || "");
            if (!text || sending) return;

            rememberRequest(text);
            addMessage("user", text);
            $message.val("");

            if (requestsContactForm(text)) {
                addMessage("assistant", "Please complete this short form and our sales team will follow up with you.");
                showContactForm(text);
                return;
            }

            var destination = navigationRequest(text);
            if (destination) {
                openNavigationDestination(destination);
                return;
            }

            setSending(true);
            var $thinking = addMessage("assistant", "Checking that for you...", "is-thinking", null, null, false);
            var token = $chatForm.find("input[name='ChatRequestToken']").val();

            $.ajax({
                url: $widget.data("message-url"),
                method: "POST",
                data: {
                    ChatRequestToken: token,
                    Message: text,
                    PreviousResponseId: responseId || ""
                }
            }).done(function (data) {
                $thinking.remove();
                var reply = data && (data.Reply || data.reply);
                responseId = data && (data.ResponseId || data.responseId) || null;
                var vehicles = data && (data.Vehicles || data.vehicles) || [];
                var totalMatches = data && (data.TotalVehicleMatches != null ? data.TotalVehicleMatches : data.totalVehicleMatches);
                addMessage("assistant", reply || "I could not prepare an answer. Please try again.", "", vehicles, totalMatches);
            }).fail(function (xhr) {
                $thinking.remove();
                if (responseId && xhr && xhr.status === 502) responseId = null;
                addMessage("assistant", errorMessage(xhr, "I could not reach the assistant. Please try again."), "is-error");
            }).always(function () {
                setSending(false);
                $message.trigger("focus");
            });
        }

        function resetChat() {
            closeCommandGuide(false);
            responseId = null;
            conversation = [];
            removeStoredChat();
            $messages.empty();
            addMessage("assistant", "New conversation started. What kind of vehicle can I help you find?");
            $lead.addClass("d-none");
            $panel.removeClass("is-contact-open");
            $chatForm.removeClass("d-none");
        }

        function loadSalespeople() {
            if (salespeopleLoaded || !$salesperson.length) {
                return $.Deferred().resolve().promise();
            }
            if (salespeopleRequest) return salespeopleRequest;

            $salesperson.prop("disabled", true);
            salespeopleRequest = $.ajax({
                url: $widget.data("salespeople-url"),
                method: "GET",
                cache: false
            }).done(function (employees) {
                $.each(employees || [], function (_, employee) {
                    var id = employee && (employee.id != null ? employee.id : employee.Id);
                    var name = employee && (employee.name || employee.Name);
                    if (id > 0 && name) {
                        salespeople.push({ id: Number(id), name: String(name) });
                        $("<option>", { value: id, text: name }).appendTo($salesperson);
                    }
                });
                salespeopleLoaded = true;
            }).fail(function () {
                salespeopleRequest = null;
            }).always(function () {
                $salesperson.prop("disabled", false);
            });

            return salespeopleRequest;
        }

        function normalizedWords(value) {
            return $.trim(String(value || "").toLowerCase().replace(/[^a-z0-9]+/g, " "))
                .split(/\s+/)
                .filter(Boolean);
        }

        function selectNamedSalesperson(contactText) {
            if (!contactText || !salespeople.length) return;

            var requestWords = normalizedWords(contactText);
            var requestText = " " + requestWords.join(" ") + " ";
            var fullNameMatches = $.grep(salespeople, function (employee) {
                var name = normalizedWords(employee.name).join(" ");
                return name && requestText.indexOf(" " + name + " ") >= 0;
            });
            var matches = fullNameMatches;

            if (matches.length !== 1) {
                matches = $.grep(salespeople, function (employee) {
                    var nameWords = normalizedWords(employee.name);
                    return nameWords.length && $.inArray(nameWords[0], requestWords) >= 0;
                });
            }
            if (matches.length !== 1) {
                matches = $.grep(salespeople, function (employee) {
                    var nameWords = normalizedWords(employee.name);
                    var lastName = nameWords[nameWords.length - 1];
                    return lastName && $.inArray(lastName, requestWords) >= 0;
                });
            }

            $salesperson.val(matches.length === 1 ? String(matches[0].id) : "0");
        }

        function showContactForm(contactText) {
            $lead.removeClass("d-none");
            $panel.addClass("is-contact-open");
            $chatForm.addClass("d-none");
            if (typeof contactText === "string" && contactText) {
                $salesperson.val("0");
            }
            loadSalespeople().done(function () {
                selectNamedSalesperson(typeof contactText === "string" ? contactText : "");
            });
            var stock = new URLSearchParams(window.location.search).get("stock");
            if (stock) $leadForm.find("[name='VehicleStock']").val(stock);
            $leadForm.find("[name='FirstName']").trigger("focus");
        }

        $launcher.on("click", openChat);
        $widget.on("click", "[data-chat-close]", closeChat);
        $widget.on("click", "[data-chat-help]", function () {
            if ($commandGuide.prop("hidden")) openCommandGuide();
            else closeCommandGuide();
        });
        $widget.on("click", "[data-chat-help-close]", function () { closeCommandGuide(); });
        $widget.on("click", "[data-chat-reset]", resetChat);
        $widget.on("click", "[data-chat-contact]", function () {
            closeCommandGuide(false);
            showContactForm("");
        });
        $widget.on("click", "[data-chat-contact-close]", function () {
            $lead.addClass("d-none");
            $panel.removeClass("is-contact-open");
            $chatForm.removeClass("d-none");
            $message.trigger("focus");
        });
        $widget.on("click", "[data-chat-prompt]", function () {
            closeCommandGuide(false);
            openChat();
            sendMessage($(this).data("chat-prompt"));
        });

        $widget.on("keydown", function (event) {
            if (event.key === "Escape" && !$commandGuide.prop("hidden")) {
                event.preventDefault();
                closeCommandGuide();
            }
        });

        $chatForm.on("submit", function (event) {
            event.preventDefault();
            sendMessage($message.val());
        });

        $message.on("keydown", function (event) {
            if ((event.key === "ArrowUp" || event.key === "ArrowDown") && !event.shiftKey && !event.altKey && !event.ctrlKey && !event.metaKey) {
                event.preventDefault();
                moveThroughRequestHistory(event.key === "ArrowUp" ? -1 : 1);
            } else if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                $chatForm.trigger("submit");
            }
        }).on("input", function () {
            if (!applyingRequestHistory) {
                requestHistoryIndex = -1;
                requestHistoryDraft = this.value;
            }
            this.style.height = "auto";
            this.style.height = Math.min(this.scrollHeight, 112) + "px";
        });

        $leadForm.on("submit", function (event) {
            event.preventDefault();
            if (!this.checkValidity()) {
                this.reportValidity();
                return;
            }

            var $submit = $leadForm.find("button[type='submit']");
            var $submitLabel = $submit.find("[data-chat-submit-label]");
            $submit.prop("disabled", true);
            $submitLabel.text("Sending...");
            $leadFeedback.removeClass("is-error is-success").text("");

            $.ajax({
                url: $widget.data("lead-url"),
                method: "POST",
                data: $leadForm.serialize()
            }).done(function (data) {
                var message = data && (data.message || data.Message) || "Your request was sent.";
                var delivered = data && data.success !== false;
                var saved = data && (data.saved === true || data.Saved === true);
                $leadFeedback.addClass(delivered ? "is-success" : "is-warning").text(message);
                if (delivered || saved) {
                    $leadForm.get(0).reset();
                    addMessage("assistant", message, delivered ? "" : "is-warning");
                }
            }).fail(function (xhr) {
                $leadFeedback.addClass("is-error").text(errorMessage(xhr, "We could not submit your request."));
            }).always(function () {
                $submit.prop("disabled", false);
                $submitLabel.text("Send request");
            });
        });

        (function restoreChat() {
            var state = loadStoredChat();
            if (!state) return;

            responseId = state.responseId;
            conversation = state.messages;
            if (conversation.length) {
                $messages.empty();
                $.each(conversation, function (_, item) {
                    addMessage(
                        item.role,
                        item.text,
                        item.extraClass,
                        item.vehicles,
                        item.totalMatches,
                        false);
                });
            }

            if (state.isOpen) {
                $panel.prop("hidden", false);
                $launcher.attr("aria-expanded", "true").addClass("d-none");
                scrollMessages();
            }
        })();
    });
})(jQuery, window, document);
