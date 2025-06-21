'use strict';

(function ($) {
    /*------------------
        Preloader
    --------------------*/
    $(window).on("load", function () {
        $(".main-menu li ").removeClass("active");

        'use strict';
        var forms = $(".needs-validation");
        Array.prototype.slice.call(forms).forEach(function (form) {
            form.addEventListener("submit", function (event) {
                if (!form.checkValidity()) {
                    event.preventDefault();
                    event.stopPropagation();
                }
                form.classList.add("was-validated");
            }, false);
        });
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
