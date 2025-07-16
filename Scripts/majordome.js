function applyFilterTerm(term) {
    gridApi.setGridOption('quickFilterText', term);
}

function saveDetails(model) {
    fetch('/Majordome/SaveDetails', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(model)
    })
        .then(response => {
            if (!response.ok) throw new Error("Server error");
            return response.json();
        })
        .then(data => {
            console.log("✅ Server response:", data);;
        })
        .catch(error => {
            console.log("❌ Error submitting:", error);
        });
}

function loadGallery(vehicle) {
    var container = $("#sortable-gallery");
    container.empty();

    vehicle.Images.forEach(function (imgPath) {
        var item = `
            <div class="col-md-2 gallery-item well p-3" data-filename="${imgPath}">
                <a href="${imgPath}" data-lightbox="gallery">
                    <img src="${imgPath}"/>
                </a>
                <span class="delete-image bi bi-trash" data-file="${imgPath}"></span>
            </div>
        `;

        container.append(item);
    });
}

// File upload jazz
function actionsRenderer(params) {
    const container = document.createElement('div');
    const fileInput = document.createElement('input');

    fileInput.type = 'file';
    fileInput.multiple = true;
    fileInput.style.display = 'none';

    const icon = document.createElement('span');
    icon.className = 'bi bi-upload fs-5 px-2';
    icon.style.cursor = 'pointer';
    icon.title = `Upload images for Stock# ${JSON.stringify(params.data.Stock)}`;

    icon.onclick = () => {
        fileInput.click();
    };

    fileInput.addEventListener('change', () => {
        const files = Array.from(fileInput.files);
        if (files.length > 0) {
            uploadFiles(params.data.Stock, fileInput);
        }
    });

    container.appendChild(reStoryIconRenderer(params));
    container.appendChild(icon);
    container.appendChild(fileInput);
    if (params.data.Images && params.data.Images.length > 0) {
        container.appendChild(deleteIconRenderer(params));
    }
    return container;
}

function uploadFiles(stock, input) {
    const files = input.files;
    if (files.length === 0) return;

    const formData = new FormData();
    for (let i = 0; i < files.length; i++) {
        formData.append("files", files[i]);
    }
    formData.append("stock", stock);

    fetch("/Majordome/Upload", {
        method: "POST",
        body: formData
    })
        .then(response => {
            if (response.ok) {
                fetch('/Majordome/GetUpdatedItems')
                    .then(res => res.json())
                    .then(data => {
                        gridApi.setRowData(data);

                        loadGallery(data);
                    });
                alert(`Uploaded ${files.length} file(s) for ${stock}`);
            } else {
                alert("Upload failed.");
            }
        })
        .catch(error => {
            alert(error);
        });
}

function decodeVin(vin) {
    fetch('/Inventory/DecodeVin', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ vin: vin })
    })
        .then(res => res.json())
        .then(data => {
            console.log(data);
            $("#details-content").html(data.Error);
        });
}

function reStoryAll() {
    showSpinner();

    fetch('/Majordome/ReStoryAll', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' }
    })
        .then(response => {
            if (response.ok) {
                fetch('/Majordome/GetUpdatedItems')
                    .then(res => res.json())
                    .then(data => {
                        gridApi.setRowData(data);
                    });
                alert(`Restory is done`);
            } else {
                alert("Restory failed.");
            }
            hideSpinner();

        })
        .catch(error => {
            alert(error);
        });
}

function deleteImage(file, object) {
    var item = $(object).closest('.gallery-item');
    $.post(`${root}Majordome/DeleteImage`, { file })
        .done(function (response) {
            if (response.success) {
                item.fadeOut(300, function () { item.remove(); });
                            fetch('/Majordome/GetUpdatedItems')
                .then(res => res.json())
                .then(data => {
                    gridApi.setRowData(data);
                });
            }
        })
};

function deleteImages(stock) {
    $.post(`${root}Majordome/DeleteImages`, { stock })
        .done(function (response) {
            fetch('/Majordome/GetUpdatedItems')
                .then(res => res.json())
                .then(data => {
                    gridApi.setRowData(data);
                });
        })
};

function createStory(stock) {
    showSpinner();
    $.post(`${root}Majordome/CreateStory`, { stock })
        .done(function (response) {
            fetch('/Majordome/GetUpdatedItems')
                .then(res => res.json())
                .then(data => {
                    gridApi.setRowData(data);
                    hideSpinner();
                });
        })
};

