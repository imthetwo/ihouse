document.querySelectorAll('input[type="password"]').forEach(function (input) {

    // Avoid duplicate wrappers
    if (input.closest(".password-box")) return;

    const wrapper = document.createElement("div");
    wrapper.className = "password-box";

    input.parentNode.insertBefore(wrapper, input);
    wrapper.appendChild(input);

    input.classList.add("password-input");

    const button = document.createElement("button");
    button.type = "button";
    button.className = "password-toggle";
    button.setAttribute("aria-label", "Show password");
    button.setAttribute("aria-pressed", "false");

    const icon = document.createElement("i");
    icon.className = "bi bi-eye";
    icon.setAttribute("aria-hidden", "true");

    button.appendChild(icon);

    button.addEventListener("click", function () {
        const show = input.type === "password";

        input.type = show ? "text" : "password";
        icon.className = show
            ? "bi bi-eye-slash"
            : "bi bi-eye";

        button.setAttribute(
            "aria-label",
            show ? "Hide password" : "Show password"
        );

        button.setAttribute("aria-pressed", String(show));
    });

    wrapper.appendChild(button);
});
