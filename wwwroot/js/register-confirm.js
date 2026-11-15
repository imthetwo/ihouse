
"use strict";

/* ==========================================
   IHOUSE - REGISTER CONFIRMATION
========================================== */

document.addEventListener("DOMContentLoaded", function () {

    const notice = document.getElementById(
        "verifyNotice"
    );

    const dismissButton = document.getElementById(
        "dismissVerifyNotice"
    );

    if (!notice || !dismissButton) {
        return;
    }

    dismissButton.addEventListener("click", function () {

        notice.remove();

    });

});
