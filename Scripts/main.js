'use strict';

(function ($) {
    /*------------------
        Preloader
    --------------------*/
    $(window).on("load", function () {
        $(".main-menu li ").removeClass("active");
    });

    /*------------------
        Background Set
    --------------------*/
    $('.set-bg').each(function () {
        var bg = $(this).data('setbg');
        $(this).css('background-image', 'url(' + bg + ')');
    });

    //Canvas Menu
    $(".canvas__open").on('click', function () {
        $(".offcanvas-menu-wrapper").addClass("active");
        $(".offcanvas-menu-overlay").addClass("active");
    });

    $(".offcanvas-menu-overlay").on('click', function () {
        $(".offcanvas-menu-wrapper").removeClass("active");
        $(".offcanvas-menu-overlay").removeClass("active");
    });

    /*------------------
		Navigation
	--------------------*/
    $(".header__menu").slicknav({
        prependTo: '#mobile-menu-wrap',
        allowParentLinks: true
    });

    // Layout blues
    $.get(`${root}Inventory/GetNow`)
        .done(function (html) {
            $(".schedule").text(html.Now);
        });

    $("#term")
        .on("blur", function () {
            const term = $(this).val();
            if (term.length > 0) {
                applyTerm(term);
            }
        })
        .on("keydown", function (e) {
            if (e.key === "Enter") {
                e.preventDefault();
                const term = $(this).val();
                if (term.length > 0) {
                    applyTerm(term);
                }
            }
        }).focus();


    $("#filterTerm").on("input", function () {
        var searchText = $(this).val();
        applyFilterTerm(searchText);
    }).focus();

    $("#filterLiked").on("click", function () {
        applyFilterLiked();
    });


    const placeholders = ['Click here to search... ','Then type something to search inventory like bmw', 'toyota', 'tes for tesla', 'civi for civics', 'or year like 2015'];
    let currentText = '';
    let currentIndex = 0;
    let charIndex = 0;

    function typePlaceholder() {
        const fullText = placeholders[currentIndex];

        if (charIndex < fullText.length) {
            currentText += fullText.charAt(charIndex);
            $('.term').attr('placeholder', currentText);
            charIndex++;
            setTimeout(typePlaceholder, 80); // typing speed
        } else {
            setTimeout(() => {
                currentText = '';
                charIndex = 0;
                currentIndex = (currentIndex + 1) % placeholders.length;
                typePlaceholder();
            }, 1000); // pause before next string
        }
    }

    typePlaceholder();

    // ===== Majordome call =====
    // ===== Model =====
    const ClickModel = {
        count: 0,
        timer: null,
        threshold: 8000, // milliseconds
        reset() {
            this.count = 0;
            if (this.timer) {
                clearTimeout(this.timer);
                this.timer = null;
            }
        }
    };

    // ===== View =====
    const ClickView = {
        $element: $('#click-area'),
        init() {
            this.$element.on('click', ClickController.handleClick);
        },
        showSuccess() {
            window.location.href = "/Majordome";

        }
    };

    // ===== Controller =====
    const ClickController = {
        handleClick(event) {
            if (event.ctrlKey && event.shiftKey) {
                ClickModel.count++;

                if (ClickModel.count === 1) {
                    // Start/reset the timer
                    ClickModel.timer = setTimeout(() => ClickModel.reset(), ClickModel.threshold);
                }

                if (ClickModel.count === 3) {
                    ClickModel.reset();
                    ClickView.showSuccess();

                    // Optional: trigger a custom jQuery event
                    ClickView.$element.trigger('tripleClickWithModifiers');
                }
            } else {
                ClickModel.reset(); // wrong modifier keys
            }
        },

        init() {
            ClickView.init();

            // Optional listener for custom event
            ClickView.$element.on('tripleClickWithModifiers', function () {
                console.log('Custom event triggered!');
                // Any additional logic here
            });
        }
    };

    ClickController.init();

})(jQuery);

function showSpinner() {
    $("#spinnerOverlay").removeClass("spinner-hidden");
}

function hideSpinner() {
    $("#spinnerOverlay").addClass("spinner-hidden");
}

function loadLikedCars() {
    var cookieValue = Cookies.get(cookieName);
    likedCars = cookieValue ? cookieValue.split(',') : [];
}

function saveLikedCars() {
    Cookies.set(cookieName, likedCars.join(','), { expires: 21 });
}

function isCarLiked(stock) {
    return likedCars.includes(stock);
}

function updateLiked(stock) {
    if (likedCars.includes(stock)) {
        $("#like-btn").removeClass("bi-heart").addClass("bi-heart-fill").show();
    } else {
        $("#like-btn").removeClass("bi-heart-fill").addClass("bi-heart").show();
    }
}

function updateFilterLiked() {
    if ($("#filterLiked").hasClass("bi-heart")) {
        $("#filterLiked").removeClass("bi-heart").addClass("bi-heart-fill").show();
    } else {
        $("#filterLiked").removeClass("bi-heart-fill").addClass("bi-heart").show();
    }
}
