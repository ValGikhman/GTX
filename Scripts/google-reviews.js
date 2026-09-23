(function ($) {
    "use strict";

    $(function () {
        var list = $("#googleReviewsList");
        if (!list.length) return;
        var status = $("#googleReviewsStatus");
        var more = $("#googleReviewsMore");
        var page = 1;
        var busy = false;
        var seen = {};

        function addReview(review) {
            // Hibu marks direct reviews with a null platform (or FIRST_PARTY).
            var isGoogle = review.platform === "GOOGLEMYBUSINESS";
            var isHibu = review.platform == null || review.platform === "FIRST_PARTY";
            if ((!isGoogle && !isHibu) || seen[review.id]) return;
            var source = isGoogle ? "Google" : "Hibu";
            seen[review.id] = true;
            var card = $("<article>", { "class": "t-card" });
            var header = $("<div>", { "class": "review-header" }).appendTo(card);
            $("<div>", { "class": "hr-review-icon", "aria-hidden": "true" })
                .append($("<img>", {
                    src: list.attr(isGoogle ? "data-google-icon" : "data-hibu-icon"),
                    alt: "",
                    width: 42,
                    height: 42
                })).appendTo(header);
            var details = $("<div>", { "class": "review-details" }).appendTo(header);
            var nameRow = $("<div>", { "class": "review-name-row" }).appendTo(details);
            $("<h2>", { "class": "t-title" }).text(review.name || source).appendTo(nameRow);
            $("<div>", { "class": "small mb-1 review-source" }).text(source).appendTo(details);
            var rating = Number(review.rating);
            if (rating >= 1 && rating <= 5) {
                var ratingDisplay = $("<span>", { "class": "google-review-rating" }).appendTo(details);
                var stars = $("<span>", { "class": "google-review-stars", "aria-hidden": "true" }).appendTo(ratingDisplay);
                for (var star = 1; star <= 5; star++) {
                    var icon = rating >= star ? "bi-star-fill" : rating >= star - 0.5 ? "bi-star-half" : "bi-star";
                    $("<i>", { "class": "bi " + icon }).appendTo(stars);
                }
                $("<span>", { "class": "google-review-rating-label" }).text(list.attr("data-rating-label").replace("{0}", rating)).appendTo(ratingDisplay);
            }
            $("<p>", { "class": "google-review-comment" }).text(review.comment || "").appendTo(card);
            // Preserve the calendar date in Hibu's ISO timestamp without shifting
            // it into the visitor's time zone.
            var date = /^(\d{4})-(\d{2})-(\d{2})(?:T|$)/.exec(review.created_at || "");
            if (date) {
                var published = new Date(Number(date[1]), Number(date[2]) - 1, Number(date[3]));
                $("<div>", { "class": "review-date small text-body-secondary" }).append($("<time>", {
                    datetime: date[1] + "-" + date[2] + "-" + date[3]
                }).text(list.attr("data-created-label").replace("{0}", published.toLocaleDateString(document.documentElement.lang || "en-US")))).appendTo(nameRow);
            }
            list.append(card);
        }

        function loadReviews() {
            if (busy) return;
            busy = true;
            list.attr("aria-busy", "true");
            more.prop("disabled", true);
            status.text(status.attr("data-loading")).prop("hidden", false);
            $.ajax({
                url: "https://hibu.us/api/public/v2/merchants/77018/reviews.json",
                data: { limit: 10, page: page, filter: "null", thirdParty: true },
                dataType: "json",
                timeout: 15000
            }).done(function (data) {
                if (!data || !Array.isArray(data.reviews)) {
                    showError();
                    return;
                }
                data.reviews.forEach(addReview);
                page++;
                var hasReviews = list.children().length > 0;
                status.text(status.attr("data-empty")).prop("hidden", hasReviews);
                more.text(more.attr("data-more")).prop("hidden", data.lastPage === true);
            }).fail(showError).always(function () {
                busy = false;
                list.attr("aria-busy", "false");
                more.prop("disabled", false);
            });
        }

        function showError() {
            status.text(status.attr("data-error")).prop("hidden", false);
            more.text(more.attr("data-retry")).prop("hidden", false);
        }

        more.on("click", loadReviews);
        loadReviews();
    });
})(jQuery);
