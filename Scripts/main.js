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
        });

    const placeholders = ['Type something to search inventory like bmw', 'toyota', 'tes for tesla', 'civi for civics', 'or year like 2015', 'Type ele for electric', 'Or use filters below'];
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
