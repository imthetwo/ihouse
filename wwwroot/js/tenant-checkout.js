
// ========================================
// iHOUSE - TENANT CHECKOUT
// ========================================

document.addEventListener("DOMContentLoaded", () => {
    const checkoutPage = document.querySelector(".checkout-page");

    if (!checkoutPage) {
        return;
    }

    // PAYMENT METHODS
    const cashRadio = document.getElementById("cashMethod");
    const paypalRadio = document.getElementById("paypalMethod");

    const cashOption = document.getElementById("cashOption");
    const paypalOption = document.getElementById("paypalOption");

    // PAYMENT INFORMATION
    const cashInfo = document.getElementById("cashInfo");
    const paypalInfo = document.getElementById("paypalInfo");

    // PAYMENT FORMS
    const cashForm = document.getElementById("cashPaymentForm");
    const paypalForm = document.getElementById("paypalPaymentForm");

    // PAYMENT BUTTONS
    const cashButton = document.getElementById("cashPaymentButton");
    const paypalButton = document.getElementById("paypalPaymentButton");

    // Paid invoices do not have payment forms.
    if (
        !cashRadio ||
        !paypalRadio ||
        !cashOption ||
        !paypalOption ||
        !cashInfo ||
        !paypalInfo ||
        !cashForm ||
        !paypalForm
    ) {
        return;
    }

    // ========================================
    // UPDATE SELECTED PAYMENT METHOD
    // ========================================

    function updatePaymentMethod() {
        const isCash = cashRadio.checked;

        cashOption.classList.toggle("selected", isCash);
        paypalOption.classList.toggle("selected", !isCash);

        cashInfo.classList.toggle("d-none", !isCash);
        paypalInfo.classList.toggle("d-none", isCash);

        cashForm.classList.toggle("d-none", !isCash);
        paypalForm.classList.toggle("d-none", isCash);
    }

    // ========================================
    // SELECT CASH
    // ========================================

    cashOption.addEventListener("click", () => {
        if (cashRadio.disabled) {
            return;
        }

        cashRadio.checked = true;
        paypalRadio.checked = false;

        updatePaymentMethod();
    });

    // ========================================
    // SELECT PAYPAL
    // ========================================

    paypalOption.addEventListener("click", () => {
        if (paypalRadio.disabled) {
            return;
        }

        paypalRadio.checked = true;
        cashRadio.checked = false;

        updatePaymentMethod();
    });

    cashRadio.addEventListener("change", updatePaymentMethod);
    paypalRadio.addEventListener("change", updatePaymentMethod);

    // ========================================
    // PREVENT DUPLICATE SUBMISSIONS
    // ========================================

    let cashSubmitting = false;
    let paypalSubmitting = false;

    function setButtonLoading(button, message) {
        if (!button) {
            return;
        }

        button.disabled = true;

        button.replaceChildren();

        const spinner = document.createElement("span");
        spinner.className =
            "spinner-border spinner-border-sm me-2";
        spinner.setAttribute("aria-hidden", "true");

        const label = document.createElement("span");
        label.textContent = message;

        button.append(spinner, label);
    }

    // ========================================
    // CASH PAYMENT SUBMISSION
    // ========================================

    cashForm.addEventListener("submit", (event) => {
        if (
            !cashRadio.checked ||
            cashRadio.disabled ||
            cashSubmitting ||
            (cashButton && cashButton.disabled)
        ) {
            event.preventDefault();
            return;
        }

        const confirmed = window.confirm(
            "Submit your cash payment request?"
        );

        if (!confirmed) {
            event.preventDefault();
            return;
        }

        cashSubmitting = true;

        setButtonLoading(
            cashButton,
            "Submitting..."
        );
    });

    // ========================================
    // PAYPAL PAYMENT SUBMISSION
    // ========================================

    paypalForm.addEventListener("submit", (event) => {
        if (
            !paypalRadio.checked ||
            paypalRadio.disabled ||
            paypalSubmitting ||
            (paypalButton && paypalButton.disabled)
        ) {
            event.preventDefault();
            return;
        }

        paypalSubmitting = true;

        setButtonLoading(
            paypalButton,
            "Connecting to PayPal..."
        );
    });

    // ========================================
    // INITIALIZE
    // ========================================

    updatePaymentMethod();
});
