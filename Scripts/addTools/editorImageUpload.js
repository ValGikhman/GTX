(function ($) {
    'use strict';

    window.initializeEditorImageUpload = function ($form, $field, options) {
        if (!$field.length) return;

        var existingEditor = $field.data('add-edit');
        if (existingEditor) return existingEditor;

        var pending = 0;
        var $status = $form.find('[data-editor-upload-status]');
        var $submit = $form.find('[type="submit"]');
        var editorOptions = { height: 260, placeholder: options.placeholder };
        if (options.localImagePicker) editorOptions.onImageSelect = chooseImage;
        $field.addEdit(editorOptions);
        var editor = $field.data('add-edit');

        function upload(file, range) {
            if ($form.data('saving')) return;
            if (!file || !file.size || file.size > 5 * 1024 * 1024 || !/^image\/(png|jpeg|gif|bmp)$/.test(file.type)) {
                $status.text('Choose a PNG, JPEG, GIF or BMP image up to 5 MB.').addClass('text-danger');
                return;
            }

            var data = new FormData();
            data.append('file', file);
            data.append('__RequestVerificationToken', $form.find('[name="__RequestVerificationToken"]').val());
            pending++;
            $submit.prop('disabled', true);
            $status.removeClass('text-danger').text('Uploading image...');

            $.ajax({ url: $form.data('upload-url'), type: 'POST', data: data, processData: false, contentType: false })
                .done(function (result) {
                    if (!result || !result.url) {
                        $status.addClass('text-danger').text('Image upload failed. Please sign in again and retry.');
                        return;
                    }
                    if (insertImage(result.url, range)) {
                        $status.removeClass('text-danger').text(options.uploadedMessage);
                    } else {
                        $status.addClass('text-danger').text('Image uploaded but could not be inserted. Return to the editor and try again.');
                    }
                })
                .fail(function () {
                    $status.addClass('text-danger').text('Image upload failed. Check the image size and format, then try again.');
                })
                .always(function () {
                    pending--;
                    $submit.prop('disabled', pending > 0 || !!$form.data('saving'));
                });
        }

        function selection() {
            var selected = window.getSelection();
            return selected.rangeCount && editor.body.contains(selected.anchorNode) ? selected.getRangeAt(0).cloneRange() : null;
        }

        function insertImage(url, range) {
            if (!document.documentElement.contains(editor.body)) return false;

            if (editor._sourceViewOn) {
                var source = editor.$sourceArea[0];
                var image = document.createElement('img');
                var end = source.value.length;
                image.setAttribute('src', url);
                source.setRangeText(image.outerHTML, end, end);
                editor.$sourceArea.trigger('input');
                $field.val(editor.value());
                return true;
            }

            editor.body.focus();
            var selected = window.getSelection();
            if (!range || !editor.body.contains(range.commonAncestorContainer)) {
                range = document.createRange();
                range.selectNodeContents(editor.body);
                range.collapse(false);
            }
            selected.removeAllRanges();
            selected.addRange(range);
            if (!document.execCommand('insertImage', false, url)) return false;
            $field.val(editor.value());
            return true;
        }

        function chooseImage() {
            var range = selection();
            var $input = $('<input type="file" accept="image/png,image/jpeg,image/gif,image/bmp">');
            $input.on('change', function () { if (this.files.length) upload(this.files[0], range); });
            $input.trigger('click');
        }

        $(editor.body).on('dragover drop paste', function (e) {
            var event = e.originalEvent;
            var transfer = event && (event.dataTransfer || event.clipboardData);
            if (!transfer) return;
            if (e.type === 'dragover') {
                if (Array.prototype.indexOf.call(transfer.types || [], 'Files') !== -1) e.preventDefault();
                return;
            }
            if (!transfer.files || !transfer.files.length) return;

            e.preventDefault();
            e.stopPropagation();
            upload(transfer.files[0], selection());
        });

        $form.on('submit.editorImageUpload', function (e) {
            if (pending) {
                e.preventDefault();
                e.stopImmediatePropagation();
            }
        });

        return editor;
    };
})(jQuery);
