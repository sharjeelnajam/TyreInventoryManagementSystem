window.initMultiSelect = (elementId, dotNetRef, callbackName) => {
    const el = $('#' + elementId);

    el.select2({
        width: '80%',
        placeholder: 'Select...',
        dropdownParent: el.closest('.modal-content'),
        allowClear: true
    });

    el.on('change', function () {
        const values = $(this).val() || []; // array of selected values
        dotNetRef.invokeMethodAsync(callbackName, values);
    });
};

window.setMultiSelectValues = (elementId, values) => {
    const el = $('#' + elementId);
    el.val(values).trigger('change');
};
