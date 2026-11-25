"use strict";

function hideFailedImage(image) {
    if (image instanceof HTMLImageElement && image.hasAttribute("data-hide-on-error")) {
        image.remove();
    }
}

document.addEventListener("error", function (event) {
    hideFailedImage(event.target);
}, true);

document.querySelectorAll("img[data-hide-on-error]").forEach(function (image) {
    if (image.complete && image.naturalWidth === 0) hideFailedImage(image);
});

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
