"use strict";

document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("connectContractForm");
    const input = document.getElementById("code");
    const button = document.getElementById("connectSubmitButton");
    if (!form || !input || !button) return;
    form.addEventListener("submit", function (event) {
        input.value = input.value.trim();
        if (button.disabled || !input.value) {
            event.preventDefault();
            if (!input.value) input.focus();
            return;
        }
        button.disabled = true;
        document.getElementById("connectButtonText").textContent = "Connecting...";
    });
});
