var selectedVehicle;

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
    var i = 0;
    vehicle.Images.forEach(function (imgPath) {
        var item = `
        <div class="col-lg-2 col-md-3 col-sm-4 gallery-item well pt-3 shadow m-2" data-filename="${imgPath}">
            <a href="${imgPath}" data-lightbox="gallery">
                <img src="${imgPath}"/>
            </a>
            <span class="delete-image bi bi-trash btn btn-light shadow my-3" data-file="${imgPath}"></span>
        </div>
        `;

        container.append(item);
        i++;
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
    showSpinner();
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
                    const vehicle = data.find(v => v.Stock === stock);
                    loadGallery(vehicle);
                    updateRow(data);
            });
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

function setDetails(stock) {
    fetch('/Majordome/SetDetails', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ stock: stock })
    })
    .then(data => {
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
                    updateRow(data);
                });
            alert(`Restory is done`);
        } else {
            alert("Restory failed.");
        }
    })
    .catch(error => {
        alert(error);
    });
}

function deleteImages(stock) {
    showSpinner();
    $.post(`${root}Majordome/DeleteImages`, { stock })
        .done(function (response) {
            if (response.success) {
                fetch('/Majordome/GetUpdatedItems')
                    .then(res => res.json())
                    .then(data => {
                        const vehicle = data.find(v => v.Stock === stock);
                        loadGallery(vehicle);
                        updateRow(data);
                });
            }
        })
};

function deleteImage(file, object) {
    showSpinner();
    const stock = selectedVehicle.Stock;
    var item = $(object).closest('.gallery-item');
    $.post(`${root}Majordome/DeleteImage`, { file: file, stock: stock })
        .done(function (response) {
            if (response.success) {
                item.fadeOut(300, function () { item.remove(); });
                fetch('/Majordome/GetUpdatedItems')
                    .then(res => res.json())
                    .then(data => {
                        updateRow(data);
                });
            }   
    })
};

function createStory(stock) {
    showSpinner();
    $.post(`${root}Majordome/CreateStory`, { stock })
        .done(function (response) {
            fetch('/Majordome/GetUpdatedItems')
            .then(res => res.json())
            .then(data => {
                updateRow(data);
            });
        })
};

function saveOrder(sorted, stock) {
    showSpinner();
    $.post(`${root}Majordome/SaveOrder`, { sorted, stock  })
        .done(function (response) {
            if (response.success) {
                fetch('/Majordome/GetUpdatedItems')
                .then(res => res.json())
                    .then(data => {
                        updateRow(data);
                });
            }
        })
}

function updateRow(data) {
    const stock = selectedVehicle.Stock;
    const vehicle = data.find(v => v.Stock === stock);
    gridApi.forEachNode(function (node) {
        if (node.data.Stock === stock) {
            node.setData(vehicle);
        }
    });

    hideSpinner();
}

