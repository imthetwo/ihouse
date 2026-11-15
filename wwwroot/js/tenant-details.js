"use strict";

const copyButton = document.getElementById("copyInvitationButton");
copyButton?.addEventListener("click", async function () {
    const input = document.getElementById("invitationCode");
    if (!input) return;
    let copied = false;
    try {
        await navigator.clipboard.writeText(input.value);
        copied = true;
    } catch {
        input.select();
        try { copied = document.execCommand("copy"); } catch { copied = false; }
    }
    if (copied) {
        copyButton.textContent = "Copied";
        copyButton.classList.replace("btn-outline-primary", "btn-success");
    } else {
        copyButton.textContent = "Select and copy the code";
    }
});
