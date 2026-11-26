(function ($) {
    'use strict';

    window.initializeSpecialEditor = function () {
        var $form = $('form[data-special-ajax="true"]');
        window.initializeEditorImageUpload($form, $form.find('.special-rich-text'), {
            placeholder: 'Write your special...',
            localImagePicker: true,
            uploadedMessage: 'Image uploaded. Save the special to keep your changes.'
        });
    };
})(jQuery);
