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

function applyFilterTerm(term) {
    const filter = term.trim().toUpperCase();
    const items = document.querySelectorAll("#inventory > li");

    items.forEach(item => {
        const stock = $(item).data('stock') || '';
        const make = $(item).data('make') || '';
        const model = $(item).data('model') || '';
        const style = $(item).data('style') || '';
        const type = $(item).data('type') || '';
        const year = $(item).data('year') || '';

        const combined = `${stock} ${make} ${model} ${style} ${type} ${year}`;

        if (combined.includes(filter)) {
            item.style.display = '';
        } else {
            item.style.display = 'none';
        }
    });
}

function applyFilterLiked() {
    const items = document.querySelectorAll("#inventory > li");
    updateFilterLiked();
    items.forEach(item => {
        if ($("#filterLiked").hasClass("bi-heart-fill")) {
            var heart = $(item).find(".liked");
            if (heart && heart[0].style.display === "") {
                item.style.display = "";
            }
            else {
                item.style.display = "none";
            }
        }
        else {
            item.style.display = "";
        }
    });
}

function getInterestRate(creditScore) {
    if (creditScore >= 750) return 5.0;
    if (creditScore >= 700) return 6.5;
    if (creditScore >= 650) return 8.0;
    if (creditScore >= 600) return 10.0;
    return 15.0; // bad credit
}

function calculateMonthlyPayment(P, rate, month) {
    const r = rate / 100 / 12;
    const n = month;
    return (P * r) / (1 - Math.pow(1 + r, -n));
}