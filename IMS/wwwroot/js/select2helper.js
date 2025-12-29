window.initMultiSelect = function (elementId, dotNetRef, callbackName) {
    const el = $('#' + elementId);

    if (el.hasClass("select2-hidden-accessible")) {
        el.select2('destroy');
    }

    let dropdownParent = $('body');
    const modalParent = el.closest('.modal-content');
    if (modalParent.length > 0) {
        dropdownParent = modalParent;
    }

    el.select2({
        width: '100%',
        placeholder: 'Select...',
        allowClear: true,
        closeOnSelect: false,
        dropdownParent: dropdownParent
    });

    el.on('change', function () {
        const values = $(this).val() || [];
        dotNetRef.invokeMethodAsync(callbackName, values);
    });
};

window.setMultiSelectValues = function (elementId, values) {
    const el = $('#' + elementId);
    el.val(values).trigger('change.select2');
};


