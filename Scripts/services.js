$(function () {
    var $page = $("#servicesPage");
    function updateRequiredFields(form) {
        var $form = $(form);
        var enabled = $form.find(".service-enabled-row input[type='checkbox']").prop("checked");
        $form.find("[data-required-when-enabled]").each(function () {
            $(this).prop("required", enabled && $(this).attr("data-required-when-enabled") === "true");
        });
    }
    $page.find(".service-settings-card").each(function () { updateRequiredFields(this); });
    $page.on("change", ".service-enabled-row input[type='checkbox']", function () {
        updateRequiredFields(this.form);
    });

    var $picker = $page.find("[data-openai-model-picker]");
    if (!$picker.length) return;
    var $model = $picker.find("[data-openai-model]");
    var $form = $picker.closest("form");
    var $apiKey = $form.find('[data-setting-key="OpenAI:ApiKey"]');
    var $refresh = $picker.find("[data-refresh-models]");
    var $status = $picker.find("[data-models-status]");
    var activeRequest = null;
    var requestSequence = 0;

    function currentKey() { return String($apiKey.val() || ""); }
    function cancelModelRequest() {
        requestSequence++;
        if (activeRequest) {
            activeRequest.abort();
            activeRequest = null;
        }
        $refresh.prop("disabled", false);
    }
    function keyChangedStatus() {
        $status.text($.trim(currentKey()) ? "API key changed. Refresh models to update the list." : "Enter an API key to load available models.");
    }
    function isCurrentRequest(sequence, key) {
        return sequence === requestSequence && key === currentKey();
    }
    function loadModels() {
        cancelModelRequest();
        var sequence = requestSequence;
        var key = currentKey();
        if (!$.trim(key)) { keyChangedStatus(); return; }

        $refresh.prop("disabled", true);
        $status.text("Loading available models…");
        activeRequest = $.ajax({
            url: $picker.attr("data-models-url"),
            type: "POST",
            dataType: "json",
            data: {
                apiKey: key,
                __RequestVerificationToken: $form.find('[name="__RequestVerificationToken"]').val()
            }
        }).done(function (result) {
            if (!isCurrentRequest(sequence, key)) return;
            if (!result || !result.ok || !Array.isArray(result.models)) {
                $status.text(result && result.message || "Could not load models. Refresh to try again.");
                return;
            }

            // Read the selection now so a choice made while loading is retained.
            var selected = String($model.val() || "");
            $model.empty();
            if (result.models.indexOf(selected) < 0)
                $model.append($("<option>").val(selected).text(selected || "Select a model"));
            $.each(result.models, function (_, model) {
                $model.append($("<option>").val(model).text(model));
            });
            $model.val(selected);
            $status.text(result.models.length ? result.models.length + " models available." : "No models were returned. Current selection kept.");
        }).fail(function (_, status) {
            if (isCurrentRequest(sequence, key) && status !== "abort")
                $status.text("Could not load models. Refresh to try again.");
        }).always(function () {
            if (sequence !== requestSequence) return;
            activeRequest = null;
            $refresh.prop("disabled", false);
            if (key !== currentKey()) keyChangedStatus();
        });
    }

    $apiKey.on("input", function () { cancelModelRequest(); keyChangedStatus(); });
    $apiKey.on("change", loadModels);
    $refresh.on("click", loadModels);
    $(window).on("pagehide", cancelModelRequest);
    loadModels();
});
