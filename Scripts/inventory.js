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

    if (filter == "@") return;

    const vehicles = document.querySelectorAll("#inventory > li");
    let combined;

    let i = 0;
    vehicles.forEach(vehicle => {
        const stock = $(vehicle).data("stock") || "";
        const vin = $(vehicle).data("vin") || "";
        const make = $(vehicle).data("make") || "";
        const model = $(vehicle).data("model") || "";
        const style = $(vehicle).data("style") || "";
        const type = $(vehicle).data("type") || "";
        const transmission = $(vehicle).data("transmission") || "";
        const year = $(vehicle).data("year") || "";
        const color = $(vehicle).data("color") || "";
        const color2 = $(vehicle).data("color2") || "";
        const location = $(vehicle).data("location-code") || "";
        const story = $(vehicle).data("story") || "";
        const images = $(vehicle).data("images") || "";

        if (filter.startsWith("@@")) {
            if (filter === "@@ADMIN" || filter === "@@MAJORDOME") {
                $("#MajordomeLink").removeClass("d-none");
                $.post(`${root}Majordome/ShowAdmin`)
                    .done(function (response) {
                        if (response.redirectUrl) { }
                    })
                    .fail(function (error) {
                        console.error("Error setting admin:", error);
                    });
                return;
            }
            // Hidden  features
            $("#filterTerm").addClass("text-info").addClass("border-info");
            combined = `@@${location} @@${story} @@${images}`;
        }
        else {
            // Normal search
            $("#filterTerm").removeClass("text-info").removeClass("border-info");
            combined = `${stock} ${vin} ${make} ${model} ${style} ${type} ${transmission} ${year} ${color} ${color2}`;
        }

        if (combined.includes(filter)) {
            vehicle.style.display = "";
            i++;
        } else {
            vehicle.style.display = "none";
        }
    });
    if (filter === "") {
        $("#filterResults").empty();
    }
    else {
        $("#filterResults").empty().html(`${i} record(s) found.`);
    }
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

function applyFilterLast() {
    updateFilterLast();
    const items = document.querySelectorAll("#inventory > li");
    items.forEach(item => {
        if ($("#filterLast").hasClass("bi-journal-album")) {
            const stock = $(item).data("stock") || "";
            if (isCarLast(stock)) {
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