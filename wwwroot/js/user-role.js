document.addEventListener("DOMContentLoaded", function () {
	const passwordInput =
		document.getElementById("AdminPassword");

	const toggleButton =
		document.getElementById("toggleAdminPassword");

	const passwordIcon =
		document.getElementById("passwordIcon");

	if (passwordInput && toggleButton && passwordIcon) {
		toggleButton.addEventListener("click", function () {
			const showPassword =
				passwordInput.type === "password";

			passwordInput.type =
				showPassword ? "text" : "password";

			passwordIcon.className = showPassword
				? "bi bi-eye-slash"
				: "bi bi-eye";

			toggleButton.setAttribute(
				"aria-label",
				showPassword ? "Hide password" : "Show password"
			);

			toggleButton.setAttribute(
				"aria-pressed",
				String(showPassword)
			);
		});
	}

	const summary =
		document.getElementById("validationSummary");

	if (summary &&
		summary.querySelectorAll("li").length === 0) {
		summary.style.display = "none";
	}
});
