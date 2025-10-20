'use strict';

(function ($) {
    /*------------------
        Preloader
    --------------------*/
    $(document).on("click", ".card.V, #btnPrev, #btnNext", function (e) {
        showSpinner("#loadingOverlay");
    });

    $(window).on("load", function () {
        $(".main-menu li ").removeClass("active");
        hideSpinner("#loadingOverlay");
    });

    $(window).on("pageshow", function (event) {
        if (event.originalEvent.persisted) {
            hideSpinner("#loadingOverlay");
        }
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
            $("#schedule").text(html.Now);
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

    $("#filterLast").on("click", function () {
        applyFilterLast();
    });

    const filterResuls = ['Search within results... ', 'Then type something to search whithin result', 'year, make or model', '@@C 4/6/8 to filter by # of cilynders', '@@T Manual/Auto/Cont for transmission type'];
    const placeholders = ['Click here to search... ','Then type something to search inventory like bmw', 'toyota', 'tes for tesla', 'civi for civics', '2015 for year'];
    let currentText = "";
    let currentFilterText = "";
    let currentIndex = 0;
    let currentFilterIndex = 0;
    let charIndex = 0;
    let charFilterIndex = 0;

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

    function typeFilterPlaceholder() {
        const filterText = filterResuls[currentFilterIndex];

        if (charFilterIndex < filterText.length) {
            currentFilterText += filterText.charAt(charFilterIndex);
            $('#filterTerm').attr('placeholder', currentFilterText);
            charFilterIndex++;
            setTimeout(typeFilterPlaceholder, 80); // typing speed
        } else {
            setTimeout(() => {
                currentFilterText = '';
                charFilterIndex = 0;
                currentFilterIndex = (currentFilterIndex + 1) % filterResuls.length;
                typeFilterPlaceholder();
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
    typeFilterPlaceholder();
})(jQuery);

function loadLikedCars() {
    var cookieValue = Cookies.get(cookieLike);
    likedCars = cookieValue ? cookieValue.split(',') : [];
}

function saveLikedCars() {
    Cookies.set(cookieLike, likedCars.join(','), { expires: 30 });
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

function loadLastCars() {
    var cookieValue = Cookies.get(cookieLast);
    lastCars = cookieValue ? cookieValue.split(',') : [];
}

function saveLastCars() {
    Cookies.set(cookieLast, lastCars.join(','), { expires: 30 });
}

function isCarLast(stock) {
    return lastCars.includes(stock);
}

function updateFilterLast() {
    if ($("#filterLast").hasClass("bi-journal")) {
        $("#filterLast").removeClass("bi-journal").addClass("bi-journal-album").show();
    } else {
        $("#filterLast").removeClass("bi-journal-album").addClass("bi-journal").show();
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