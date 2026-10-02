(function ($) {
    'use strict';
    $(function () {
        var $links = $('[data-specials-feed]');
        if (!$links.length) return;
        $.getJSON($links.first().attr('data-specials-feed')).done(function (offers) {
            if (!Array.isArray(offers) || !offers.length) return;
            offers = offers.filter(function (offer) {
                return offer && Number.isInteger(offer.id) && offer.id > 0 &&
                    typeof offer.title === 'string' && offer.title.trim().length > 0;
            });
            if (!offers.length) return;
            var hasMultipleOffers = offers.length > 1;
            // Overlapping invisible titles let CSS size the background to the widest
            // rendered title, including after font loading or responsive font changes.
            $links.each(function () {
                var $link = $(this);
                offers.forEach(function (offer) {
                    $link.append($('<span>', {
                        'class': 'specials-player-link-measure',
                        'aria-hidden': 'true'
                    }).text(offer.title));
                });
            });
            var current = 0;
            function render() {
                var offer = offers[current];
                var position = hasMultipleOffers ? (current + 1) + ' / ' + offers.length : '';
                $links.each(function () {
                    var $link = $(this);
                    $link.find('.specials-player-link-title').text(offer.title);
                    $link.find('.specials-player-link-counter').text(position);
                    $link.attr({
                        href: $link.attr('data-specials-url').replace('__id__', String(offer.id)),
                        title: offer.title,
                        'aria-label': 'View special: ' + offer.title,
                        'data-specials-position': position
                    });
                });
            }
            render();
            if (hasMultipleOffers) {
                window.setInterval(function () {
                    current = (current + 1) % offers.length;
                    render();
                }, 10000);
            }
        });
        // Keep the ordinary Specials-page link if no offers are available or loading fails.
    });
})(jQuery);
