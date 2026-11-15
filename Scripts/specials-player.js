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
            var $controls = $links.closest('[data-specials-player]').find('.specials-player-control');
            $controls.prop('hidden', !hasMultipleOffers);
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
            var timer = null;
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
            function advance(step) {
                current = (current + step + offers.length) % offers.length;
                render();
            }
            function restartTimer() {
                if (timer !== null) window.clearInterval(timer);
                timer = window.setInterval(function () { advance(1); }, 10000);
            }
            render();
            if (hasMultipleOffers) {
                $controls.on('click', function (event) {
                    event.preventDefault();
                    advance(Number($(this).attr('data-specials-step')));
                    restartTimer();
                });
                restartTimer();
            }
        });
        // Keep the ordinary Specials-page link if no offers are available or loading fails.
    });
})(jQuery);
