document.addEventListener("DOMContentLoaded", function () {
    "use strict";

    const form = document.getElementById("paypalCaptureForm");

    const button = document.getElementById("captureButton");

    const processing = document.getElementById("paypalProcessing");

    if (!form || !button) {
        return;
    }

    const orderInput = form.querySelector(
        'input[name="orderId"]'
    );

    const orderId = orderInput
        ? orderInput.value.trim()
        : "";

    if (!orderId) {
        if (processing) {
            processing.innerHTML = `
                <div class="alert alert-danger">
                    We couldn't find your payment details.
                    Please return to your invoice and try again.
                </div>
            `;
        }

        button.disabled = true;
        return;
    }

    let submitted = false;

    form.addEventListener("submit", function (event) {
        if (submitted) {
            event.preventDefault();
            return;
        }

        submitted = true;

        button.disabled = true;

        button.innerHTML = `
            <span class="spinner-border spinner-border-sm me-2"
                  aria-hidden="true"></span>
            Confirming Payment...
        `;
    });

    // Automatically confirm the approved PayPal payment.
    form.requestSubmit();
});