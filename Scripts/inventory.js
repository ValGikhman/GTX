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
    const filter = term.trim().toLowerCase();
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
