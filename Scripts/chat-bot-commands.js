(function ($, window, document) {
    "use strict";

    $(function () {
        var $page = $("[data-command-page]");
        if (!$page.length) return;

        var $grid = $("#chatCommandAddGrid");
        var $modal = $("#chatCommandWizardModal");
        var modalElement = $modal.get(0);
        var modal = window.bootstrap && modalElement ? window.bootstrap.Modal.getOrCreateInstance(modalElement) : null;
        var $form = $modal.find("[data-command-wizard-form]");
        var currentStep = 1;
        var mode = "create";

        function errorMessage(xhr, fallback) {
            var response = xhr && xhr.responseJSON;
            return response && (response.message || response.Message) || fallback;
        }

        function initializeGrid() {
            var $source = $("#chatCommandTableSource");
            var rows = $source.find("tbody tr").map(function () {
                var $row = $(this);
                var $cells = $row.children("td");

                return {
                    Id: Number($row.attr("data-command-id")),
                    Phrase: $row.attr("data-command-phrase") || "",
                    ActionLabel: $.trim($cells.eq(1).find(".chat-command-action-name").text()),
                    ActionKey: $row.attr("data-command-action") || "",
                    Description: $.trim($cells.eq(2).text()),
                    CreatedBy: $.trim($cells.eq(3).text()),
                    Updated: $.trim($cells.eq(4).text()),
                    UpdatedSort: $row.attr("data-command-updated-sort") || "",
                    SearchText: $row.attr("data-command-search-text") || "",
                    PhraseHtml: $cells.eq(0).html(),
                    ActionHtml: $cells.eq(1).html(),
                    DescriptionHtml: $cells.eq(2).html(),
                    RoleHtml: $cells.eq(3).html(),
                    UpdatedHtml: $cells.eq(4).html(),
                    ActionsHtml: $cells.eq(5).html()
                };
            }).get();

            $source.parent().remove();
            if (!$grid.length || typeof $.fn.addGrid !== "function") return;

            $grid.addGrid({
                data: rows,
                columns: [
                    { field: "Phrase", title: "Command phrase", width: 240, filterable: false, searchField: "SearchText", template: "#chat-command-phrase-template" },
                    { field: "ActionLabel", title: "Action dependency", width: 220, searchable: false, template: "#chat-command-action-template" },
                    { field: "Description", title: "Description", width: 300, searchable: false, template: "#chat-command-description-template" },
                    { field: "CreatedBy", title: "Created by", width: 130, searchable: false, template: "#chat-command-role-template" },
                    { field: "Updated", title: "Updated", width: 130, sortField: "UpdatedSort", searchable: false, template: "#chat-command-updated-template" },
                    { field: "Actions", title: "Actions", width: 120, sortable: false, filterable: false, searchable: false, resizable: false, reorderable: false, headerClass: "text-center", cellClass: "text-center", template: "#chat-command-actions-template" }
                ],
                tableClass: "table align-middle m-0 chat-command-grid",
                rowClass: "chat-command-main-row",
                rowAttributes: function (item) {
                    return { "data-command-row": item.Id };
                },
                filterDropdownClass: "majordome-add-grid-filter",
                height: null,
                pageable: false,
                showRecordCount: true,
                recordType: { singular: "command", plural: "commands", icon: "bi bi-command" },
                sortable: true,
                filterable: true,
                resizable: true,
                reorderable: true,
                searchable: true,
                showSearch: true,
                searchPlaceholder: "Search phrase or action…",
                showFilterChips: true,
                exportToExcel: false,
                exportToPdf: false,
                groupable: true,
                alternateRows: true,
                emptyText: "No commands match the current filters.",
                emptyHint: "Adjust the search, grouping, or column filters.",
                onRowDblClick: function (detail) {
                    if ($(detail.event.target).closest("button, a, input, select").length) return;
                    openWizard({
                        id: detail.dataItem.Id,
                        phrase: detail.dataItem.Phrase,
                        actionKey: detail.dataItem.ActionKey
                    });
                }
            });

            var $newButton = $("#chatCommandNew").detach().removeClass("d-none");
            $grid.find(".pg-search-input-group").after($newButton);
        }

        function selectedAction() {
            return $form.find("[name='ActionKey'] option:selected");
        }

        function commandMapping() {
            var phrase = $.trim($form.find("[name='Phrase']").val() || "").replace(/"/g, "'");
            var actionKey = $form.find("[name='ActionKey']").val() || "";
            return phrase && actionKey ? '"' + phrase + '" maps to ' + actionKey : "";
        }

        function showStep(step) {
            currentStep = step;
            $form.find("[data-command-step]").prop("hidden", true);
            $form.find("[data-command-step='" + step + "']").prop("hidden", false);
            $form.find("[data-command-progress]").each(function () {
                var itemStep = Number($(this).attr("data-command-progress"));
                $(this).toggleClass("is-current", itemStep === step)
                    .toggleClass("is-complete", itemStep < step);
            });

            $form.find("[data-command-back]").prop("hidden", step === 1);
            $form.find("[data-command-next]").prop("hidden", step === 3)
                .html(step === 1
                    ? 'Choose action <i class="bi bi-arrow-right" aria-hidden="true"></i>'
                    : 'Review <i class="bi bi-arrow-right" aria-hidden="true"></i>');
            $form.find("[data-command-save]").prop("hidden", step !== 3);

            if (step === 3) {
                var $action = selectedAction();
                $form.find("[data-command-statement]").text(commandMapping());
                $form.find("[data-command-review-phrase]").text($.trim($form.find("[name='Phrase']").val()));
                $form.find("[data-command-review-action]").text(($action.data("label") || "") + " (" + ($action.val() || "") + ")");
                $form.find("[data-command-review-access]").text($action.data("access") || "");
            }

            $form.find("[data-command-step='" + step + "']").find("input, select").first().trigger("focus");
        }

        function validateStep(step) {
            if (step === 1) {
                var validPhrase = $.trim($form.find("[name='Phrase']").val() || "").length > 0;
                $form.find("[data-command-error='phrase']").prop("hidden", validPhrase);
                return validPhrase;
            }
            if (step === 2) {
                var validAction = Boolean($form.find("[name='ActionKey']").val());
                $form.find("[data-command-error='action']").prop("hidden", validAction);
                return validAction;
            }
            return true;
        }

        function openWizard(editData) {
            mode = editData ? "edit" : "create";
            $form.get(0).reset();
            $form.find("[name='Id']").val(editData ? editData.id : 0);
            $form.find("[name='Phrase']").val(editData ? editData.phrase : "");
            $form.find("[name='ActionKey']").val(editData ? editData.actionKey : "").trigger("change");
            $form.find("[data-command-wizard-title]").text(editData ? "Edit command" : "Add command");
            $form.find("[data-command-save]").html(editData
                ? '<i class="bi bi-check2-circle" aria-hidden="true"></i> Save changes'
                : '<i class="bi bi-check2-circle" aria-hidden="true"></i> Save command');
            $form.find("[data-command-save-status]").prop("hidden", true).text("");
            $form.find("[data-command-error]").prop("hidden", true);
            showStep(1);
            if (modal) modal.show();
        }

        $(document).on("click", "[data-command-add]", function () {
            openWizard(null);
        });

        $page.on("click", "[data-command-edit]", function () {
            var $button = $(this);
            openWizard({
                id: Number($button.attr("data-command-id")),
                phrase: $button.attr("data-command-phrase") || "",
                actionKey: $button.attr("data-command-action") || ""
            });
        });

        $form.on("input", "[name='Phrase']", function () {
            $form.find("[data-command-error='phrase']").prop("hidden", true);
        });

        $form.on("change", "[name='ActionKey']", function () {
            var $action = selectedAction();
            var hasAction = Boolean($action.val());
            $form.find("[data-command-error='action']").prop("hidden", true);
            $form.find("[data-command-action-preview]").prop("hidden", !hasAction);
            $form.find("[data-command-action-label]").text($action.data("label") || "");
            $form.find("[data-command-action-access]").text($action.data("access") || "");
            $form.find("[data-command-action-key]").text($action.val() || "");
            $form.find("[data-command-action-description]").text($action.data("description") || "");
        });

        $form.on("click", "[data-command-next]", function () {
            if (validateStep(currentStep)) showStep(currentStep + 1);
        });

        $form.on("click", "[data-command-back]", function () {
            showStep(currentStep - 1);
        });

        $form.on("submit", function (event) {
            event.preventDefault();
            if (!validateStep(1) || !validateStep(2)) return;

            var $save = $form.find("[data-command-save]");
            var $status = $form.find("[data-command-save-status]").prop("hidden", true);
            var originalHtml = $save.html();
            $save.prop("disabled", true).html('<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Saving...');

            $.ajax({
                url: mode === "edit" ? $page.data("edit-url") : $page.data("create-url"),
                method: "POST",
                data: $form.serialize()
            }).done(function () {
                window.location.reload();
            }).fail(function (xhr) {
                $status.text(errorMessage(xhr, "The command could not be saved.")).prop("hidden", false);
                $save.prop("disabled", false).html(originalHtml);
            });
        });

        $page.on("click", "[data-command-delete]", function () {
            var $button = $(this);
            var id = $button.attr("data-command-id");
            var phrase = $button.attr("data-command-phrase") || "this command";
            var confirmation = window.gtxConfirm
                ? window.gtxConfirm({
                    title: "Delete chatbot command?",
                    message: 'The phrase "' + phrase + '" will stop working. Its audit history will be preserved.',
                    confirmText: "Delete command",
                    variant: "danger",
                    iconClass: "bi bi-trash3"
                })
                : Promise.resolve(false);

            confirmation.then(function (confirmed) {
                if (!confirmed) return;
                $button.prop("disabled", true);
                $.ajax({
                    url: $page.data("delete-url"),
                    method: "POST",
                    data: {
                        requestToken: $form.find("input[name='RequestToken']").val(),
                        id: id
                    }
                }).done(function () {
                    var rows = $grid.addGrid("getData").filter(function (item) {
                        return String(item.Id) !== String(id);
                    });
                    $grid.addGrid("setData", rows, { preserveState: true });
                }).fail(function (xhr) {
                    $button.prop("disabled", false);
                    if (window.gtxAlert) {
                        window.gtxAlert(errorMessage(xhr, "The command could not be deleted."));
                    }
                });
            });
        });

        initializeGrid();
    });
})(jQuery, window, document);
