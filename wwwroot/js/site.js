"use strict";

document.addEventListener("submit", function (event) {
    const message = event.target.dataset.confirm;
    if (message && !window.confirm(message)) event.preventDefault();
});

document.addEventListener("click", function (event) {
    const button = event.target.closest("[data-go-back]");
    if (!button) return;
    if (window.history.length > 1) window.history.back();
    else window.location.assign(button.dataset.backUrl);
});
