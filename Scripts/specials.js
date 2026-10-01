(function ($) {
    'use strict';
    window.initializeSpecialEditor = function () {
        var $form = $('form[data-special-ajax="true"]');
        var pending = 0;
        var $status = $form.find('.special-upload-status');
        function upload(file, done) {
            if (!file || file.size > 5 * 1024 * 1024 || !/^image\/(png|jpeg|gif|bmp)$/.test(file.type)) {
                $status.text('Choose a PNG, JPEG, GIF or BMP image up to 5 MB.').addClass('text-danger');
                return;
            }
            var data = new FormData();
            data.append('file', file);
            data.append('__RequestVerificationToken', $form.find('[name="__RequestVerificationToken"]').val());
            pending++;
            $form.find('[type="submit"]').prop('disabled', true);
            $status.removeClass('text-danger').text('Uploading image...');
            $.ajax({ url: $form.data('upload-url'), type: 'POST', data: data, processData: false, contentType: false })
                .done(function (result) {
                    if (!result || !result.url) {
                        $status.addClass('text-danger').text('Image upload failed. Please sign in again and retry.');
                        return;
                    }
                    done(result.url);
                    $status.text('Image uploaded. Save the special to keep your changes.');
                })
                .fail(function () { $status.addClass('text-danger').text('Image upload failed. Check the image size and format, then try again.'); })
                .always(function () {
                    pending--;
                    $form.find('[type="submit"]').prop('disabled', pending > 0);
                });
        }

        $form.find('.special-rich-text').each(function () {
            var $field = $(this);
            $field.addEdit({ height: 260, placeholder: 'Write your special...', onImageSelect: function () { chooseImage(); } });
            var editor = $field.data('add-edit');
            function selection() {
                var selected = window.getSelection();
                return selected.rangeCount && editor.body.contains(selected.anchorNode) ? selected.getRangeAt(0).cloneRange() : null;
            }
            function insertImage(url, range) {
                if (!document.documentElement.contains(editor.body)) return;
                editor.body.focus();
                var selected = window.getSelection();
                if (!range || !editor.body.contains(range.commonAncestorContainer)) {
                    range = document.createRange(); range.selectNodeContents(editor.body); range.collapse(false);
                }
                selected.removeAllRanges(); selected.addRange(range);
                document.execCommand('insertImage', false, url);
                $field.val(editor.value());
            }
            function chooseImage() {
                var range = selection();
                var $input = $('<input type="file" accept="image/png,image/jpeg,image/gif,image/bmp">');
                $input.on('change', function () { upload(this.files[0], function (url) { insertImage(url, range); }); });
                $input.trigger('click');
            }
            $(editor.body).on('dragover drop paste', function (e) {
                var event = e.originalEvent;
                var transfer = event.dataTransfer || event.clipboardData;
                if (e.type === 'dragover') {
                    if (transfer && Array.prototype.indexOf.call(transfer.types || [], 'Files') !== -1) e.preventDefault();
                    return;
                }
                if (!transfer || !transfer.files || !transfer.files.length) return;
                e.preventDefault(); e.stopPropagation();
                var range = selection();
                upload(transfer.files[0], function (url) { insertImage(url, range); });
            });
        });
        $form.on('submit.specialUploads', function (e) {
            if (pending) { e.preventDefault(); e.stopImmediatePropagation(); }
        });
    };
})(jQuery);
