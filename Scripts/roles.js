$(function () {
    var $page = $("#rolesPage");
    var rows = $("#rolesSource tr").map(function () {
        var $cells = $(this).children("td");
        return { Id: $cells.eq(2).find(".edit-role").data("id"), Name: $.trim($cells.eq(0).text()), NameHtml: $cells.eq(0).html(), AccessHtml: $cells.eq(1).html(), ActionsHtml: $cells.eq(2).html() };
    }).get();
    rows.sort(function (a, b) {
        var aName = a.Name.toLowerCase(), bName = b.Name.toLowerCase();
        if (aName === "owner" && bName !== "owner") return -1;
        if (bName === "owner" && aName !== "owner") return 1;
        return aName.localeCompare(bName);
    });
    $("#rolesSource").parent().remove();
    $("#rolesGrid").addGrid({ data: rows, columns: [
        { field: "Name", title: "Role", searchable: true, filterable: false, template: "#role-name-template", width: 180 },
        { field: "AccessHtml", title: "Menu access", searchable: true, filterable: false, template: "#role-access-template", width: 650 },
        { field: "ActionsHtml", title: "Actions", searchable: false, filterable: false, template: "#role-actions-template", headerClass: "text-center", cellClass: "text-center", width: 150 }
    ], tableClass: "table align-middle m-0", height: null, pageable: false, showRecordCount: true,
        recordType: { singular: "role", plural: "roles", icon: "bi bi-people-fill" }, sortable: false,
        resizable: false, reorderable: false, searchable: true, showSearch: true, searchPlaceholder: "Search roles…",
        exportToExcel: false, exportToPdf: false, groupable: false, alternateRows: true,
        dblClick: function (detail) {
            if ($(detail.event.target).closest("a, button, input, select, textarea").length) return;
            detail.event.preventDefault();
            if (detail.dataItem && detail.dataItem.Id) open(detail.dataItem.Id);
        }
    });
    $("#rolesGrid .pg-search-input-group").after($("#newRole").detach().addClass("btn-sm"));
    function open(id) {
        $("#roleModalTitle").text(id ? "Edit role" : "New role");
        $.get($page.data("edit-url"), { id: id || 0 }).done(function (html) {
            $("#roleModalBody").html(html);
            validateRolePassword(document.getElementById("roleForm"));
            bootstrap.Modal.getOrCreateInstance(document.getElementById("roleModal")).show();
        }).fail(function () { window.gtxAlert("Could not load role."); });
    }
    $("#newRole").on("click", function () { open(0); });
    $page.on("click", ".edit-role", function (event) { event.preventDefault(); open($(this).data("id")); });
    $page.on("click", ".login-as-role", function () {
        var $buttons = $page.find(".login-as-role").prop("disabled", true);
        $.post($page.data("login-as-url"), {
            id: $(this).data("id"),
            __RequestVerificationToken: $page.find('[name="__RequestVerificationToken"]').val()
        }).done(function (res) {
            if (res && res.ok) window.location.assign(res.returnUrl);
            else window.gtxAlert("Could not switch role. Please sign in again.");
        }).fail(function () {
            window.gtxAlert("Could not switch role. Owner access is required.");
        }).always(function () { $buttons.prop("disabled", false); });
    });
    $("#roleModalBody").on("click", ".toggle-role-password", function () {
        var $button = $(this), $password = $button.siblings("input");
        var show = $password.attr("type") === "password";
        $password.attr("type", show ? "text" : "password");
        $button.attr({ "aria-label": show ? "Hide password" : "Show password", "aria-pressed": show ? "true" : "false", title: show ? "Hide password" : "Show password" });
        $button.find("i").attr("class", show ? "bi bi-eye-slash" : "bi bi-eye");
    });
    function passwordPolicyResults(value) {
        return {
            length: value.length >= 8 && value.length <= 32,
            uppercase: /[A-Z]/.test(value),
            lowercase: /[a-z]/.test(value),
            number: /[0-9]/.test(value),
            symbol: /[!-/:-@\[-`{-~]/.test(value),
            whitespace: value.length > 0 && !/[\s\u0085\uFEFF]/.test(value)
        };
    }
    function setPasswordRuleStatus($item, passed, active) {
        $item.removeClass("text-muted text-success text-danger").addClass(!active ? "text-muted" : passed ? "text-success" : "text-danger");
        $item.find("i").attr("class", "bi me-1 " + (!active ? "bi-dash-circle" : passed ? "bi-check-circle-fill" : "bi-x-circle-fill"));
        $item.find(".password-rule-status").text(!active ? "Not checked: " : passed ? "Met: " : "Not met: ");
    }
    function validateRolePassword(form) {
        if (!form) return;
        var password = form.elements.Password, confirmation = form.elements.ConfirmPassword;
        var active = form.elements.Id.value === "0" || password.value.length > 0 || confirmation.value.length > 0;
        var rules = passwordPolicyResults(password.value), passed = true;
        $(form).find("[data-password-rule]").each(function () {
            var met = rules[$(this).data("password-rule")];
            setPasswordRuleStatus($(this), met, active);
            passed = passed && met;
        });
        var matches = password.value.length > 0 && password.value === confirmation.value;
        setPasswordRuleStatus($(form).find("#rolePasswordMatch"), matches, active);
        password.setCustomValidity(active && !passed ? "Password must meet every requirement below." : "");
        confirmation.setCustomValidity(active && !matches ? "Passwords do not match." : "");
    }
    $("#roleModalBody").on("input change", "#Password, #ConfirmPassword", function () {
        validateRolePassword(this.form);
    });
    $("#roleModalBody").on("submit", "#roleForm", function (event) {
        validateRolePassword(this);
        if (!this.reportValidity()) { event.preventDefault(); return; }
        event.preventDefault(); var $form = $(this); var data = $form.serialize();
        $form.find(":submit").prop("disabled", true);
        $.post($form.attr("action"), data).done(function (res) {
            if (res && res.ok) window.location.reload();
            else {
                $("#roleModalBody").html(res);
                validateRolePassword(document.getElementById("roleForm"));
            }
        }).fail(function () { window.gtxAlert("Could not save role. Please try again."); })
          .always(function () { $form.find(":submit").prop("disabled", false); });
    });
    $page.on("click", ".delete-role", async function () {
        var $button = $(this);
        if (!await window.gtxConfirm({ title: "Delete role", message: "Delete " + $button.data("name") + "? This cannot be undone.", confirmText: "Delete", variant: "danger" })) return;
        $.post($page.data("delete-url"), { id: $button.data("id"), __RequestVerificationToken: $page.find('[name="__RequestVerificationToken"]').val() })
            .done(function () { window.location.reload(); }).fail(function (xhr) { window.gtxAlert(xhr.responseJSON && xhr.responseJSON.message || "Could not delete role."); });
    });
});
