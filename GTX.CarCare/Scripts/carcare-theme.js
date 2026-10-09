// Match GTX's palette, legacy names, localStorage priority, and one-year cookies.
// Run in the head so saved colors are applied before the first paint.
(function () {
    "use strict";
    var root = document.documentElement;
    var themes = ["blue", "grey", "navy", "green", "red", "deep-purple", "metallica", "silver", "black"];
    var backgrounds = ["light", "grey", "dark"];
    var legacy = { "pink": "navy", "dark-green": "green", "dark-red": "red", "dark": "black" };

    function readPreference(key, fallback) {
        try { return localStorage.getItem(key) || fallback; }
        catch (e) { return fallback; }
    }
    function savePreference(key, value) {
        try { localStorage.setItem(key, value); } catch (e) { /* Cookie fallback. */ }
        document.cookie = key + "=" + encodeURIComponent(value) + "; path=/; max-age=31536000; samesite=lax";
    }
    function pretty(value) {
        return value.split("-").map(function (word) { return word.charAt(0).toUpperCase() + word.slice(1); }).join(" ");
    }
    function normalizeTheme(value) {
        value = Object.prototype.hasOwnProperty.call(legacy, value) ? legacy[value] : value;
        return themes.indexOf(value) >= 0 ? value : "blue";
    }
    function applyTheme(value) {
        value = normalizeTheme(value);
        themes.concat(Object.keys(legacy)).forEach(function (name) { root.classList.remove("theme-" + name); });
        root.classList.add("theme-" + value);
        return value;
    }
    function applyBackground(value) {
        value = backgrounds.indexOf(value) >= 0 ? value : "light";
        root.dataset.backgroundTheme = value;
        root.dataset.bsTheme = value === "dark" ? "dark" : "light";
        return value;
    }
    var serverTheme = themes.filter(function (name) { return root.classList.contains("theme-" + name); })[0] || "blue";
    var theme = applyTheme(readPreference("gtx-theme", serverTheme));
    var background = applyBackground(readPreference("gtx-background-theme", root.dataset.backgroundTheme));

    document.addEventListener("DOMContentLoaded", function () {
        var items = Array.from(document.querySelectorAll(".theme-item"));
        var backgroundItems = Array.from(document.querySelectorAll(".background-theme-item"));
        function selectTheme(value) {
            theme = applyTheme(value);
            savePreference("gtx-theme", theme);
            document.getElementById("themeLabel").textContent = pretty(theme);
            items.forEach(function (item) {
                var selected = item.dataset.theme === theme;
                item.classList.toggle("active", selected);
                item.setAttribute("aria-current", selected ? "true" : "false");
            });
            var color = getComputedStyle(root).getPropertyValue("--gtx-accent-solid").trim();
            if (color) document.querySelector('meta[name="theme-color"]').setAttribute("content", color);
        }
        function selectBackground(value) {
            background = applyBackground(value);
            savePreference("gtx-background-theme", background);
            document.getElementById("backgroundThemeLabel").textContent = pretty(background);
            document.getElementById("backgroundThemeIcon").className = "theme-swatch swatch-background-" + background;
            backgroundItems.forEach(function (item) {
                var selected = item.dataset.background === background;
                item.classList.toggle("active", selected);
                item.setAttribute("aria-pressed", selected ? "true" : "false");
            });
        }
        selectTheme(theme);
        selectBackground(background);
        items.forEach(function (item) {
            item.addEventListener("click", function (event) { event.preventDefault(); selectTheme(item.dataset.theme); });
        });
        backgroundItems.forEach(function (item) {
            item.addEventListener("click", function () { selectBackground(item.dataset.background); });
        });
    });
})();
