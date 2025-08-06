'use strict';

(function ($) {
    /*------------------
        Preloader
    --------------------*/
    $(window).on("load", function () {
        $(".main-menu li ").removeClass("active");
    });

    $(".copyable").on("dblclick", function () {
        const htmlContent = $(this).html();
        const temp = $('<textarea>');
        $('body').append(temp);
        temp.val(htmlContent).select();
        document.execCommand('copy');
        temp.remove();
        playBeep();
    })

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

    window.addEventListener("scroll", function () {
        var filter = document.getElementById("filter-container");
        if (window.scrollY > 100) {
            filter.classList.add("fixed-top");
        } else {
            filter.classList.remove("fixed-top");
        }
    });

    typePlaceholder();

})(jQuery);


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

function playBeep() {
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    const oscillator = ctx.createOscillator();
    const gainNode = ctx.createGain();

    oscillator.type = 'triangle';       // 'sine', 'square', 'triangle', or 'sawtooth'
    oscillator.frequency.setValueAtTime(1100, ctx.currentTime); // Frequency in Hz

    oscillator.connect(gainNode);
    gainNode.connect(ctx.destination);

    oscillator.start();
    oscillator.stop(ctx.currentTime + 0.01); // Beep duration: 0.1 second
}


function showSpinner(object) {
    $(object).removeClass("spinner-hidden");
}

function hideSpinner(object) {
    $(object).addClass("spinner-hidden");
}