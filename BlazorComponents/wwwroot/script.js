"use strict";
var BlazorComponents;
(function (components) {
    components.setFocus = (id) => {
        const control = document.getElementById(id);
        if (control) {
            control.focus();
        }
    }

    components.saveAsFile = async (filename, contentType, stream) => {
        const buffer = await stream.arrayBuffer();
        const blob = new Blob([buffer], { type: contentType });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.target = "_self";
        link.download = filename;
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 0);
    }

    components.openNewWindow = async (contentType, stream) => {
        const buffer = await stream.arrayBuffer();
        const blob = new Blob([buffer], { type: contentType });
        const url = URL.createObjectURL(blob);
        const win = window.open(url);
        if (win) {
            setTimeout(() => URL.revokeObjectURL(url), 60000);
        } else {
            URL.revokeObjectURL(url);
        }
    }
})(BlazorComponents || (BlazorComponents = {}));
