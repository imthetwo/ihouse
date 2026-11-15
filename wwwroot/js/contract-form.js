document.addEventListener("DOMContentLoaded", function () {
    "use strict";

    // DATE PICKER
    const dateInputs = document.querySelectorAll(
        ".contract-date-input"
    );

    dateInputs.forEach(function (input) {
        input.addEventListener("click", function () {
            if (typeof input.showPicker === "function") {
                try {
                    input.showPicker();
                } catch (error) {
                    // Keep native browser behavior.
                }
            }
        });
    });

    // TENANT SEARCH ELEMENTS
    const searchInput = document.getElementById(
        "tenantSearch"
    );

    const tenantIdInput = document.getElementById(
        "PrimaryTenantId"
    );

    const resultsBox = document.getElementById(
        "tenantSearchResults"
    );

    const container = document.getElementById(
        "tenantSearchContainer"
    );

    if (
        !searchInput ||
        !tenantIdInput ||
        !resultsBox ||
        !container
    ) {
        return;
    }

    const searchUrl = container.dataset.searchUrl;

    if (!searchUrl) {
        return;
    }

    let debounceTimer = null;
    let currentRequest = null;

    function hideResults() {
        resultsBox.style.display = "none";

        searchInput.setAttribute(
            "aria-expanded",
            "false"
        );
    }

    function showResults() {
        resultsBox.style.display = "block";

        searchInput.setAttribute(
            "aria-expanded",
            "true"
        );
    }

    function showMessage(message) {
        resultsBox.replaceChildren();

        const element = document.createElement("div");

        element.className =
            "list-group-item text-muted small py-3";

        element.textContent = message;

        resultsBox.appendChild(element);

        showResults();
    }

    function selectTenant(tenant) {
        tenantIdInput.value = tenant.id;

        searchInput.value =
            tenant.name + " (ID: " + tenant.id + ")";

        hideResults();
    }

    function renderResults(tenants) {
        resultsBox.replaceChildren();

        if (!Array.isArray(tenants) || tenants.length === 0) {
            showMessage("No matching tenants found.");
            return;
        }

        tenants.forEach(function (tenant) {
            const item = document.createElement("button");

            item.type = "button";

            item.className =
                "list-group-item list-group-item-action text-start";

            item.setAttribute("role", "option");

            const name = document.createElement("div");

            name.className = "fw-semibold";

            name.textContent =
                tenant.name + " (ID: " + tenant.id + ")";

            const phone = document.createElement("small");

            phone.className = "text-muted";

            phone.textContent = tenant.phone
                ? "Phone: " + tenant.phone
                : "No phone number";

            item.appendChild(name);
            item.appendChild(phone);

            item.addEventListener("click", function () {
                selectTenant(tenant);
            });

            resultsBox.appendChild(item);
        });

        showResults();
    }

    async function searchTenants(term) {
        if (currentRequest) {
            currentRequest.abort();
        }

        const controller = new AbortController();

        currentRequest = controller;

        showMessage("Searching tenants...");

        try {
            const url =
                searchUrl +
                "?term=" +
                encodeURIComponent(term);

            const response = await fetch(url, {
                signal: controller.signal,
                headers: {
                    Accept: "application/json"
                }
            });

            if (!response.ok) {
                throw new Error("Search failed");
            }

            const tenants = await response.json();

            if (currentRequest !== controller) {
                return;
            }

            renderResults(tenants);
        } catch (error) {
            if (
                error.name !== "AbortError" &&
                currentRequest === controller
            ) {
                showMessage(
                    "Unable to search tenants."
                );
            }
        } finally {
            if (currentRequest === controller) {
                currentRequest = null;
            }
        }
    }

    // SEARCH INPUT
    searchInput.addEventListener("input", function () {
        tenantIdInput.value = "0";

        clearTimeout(debounceTimer);

        if (currentRequest) {
            currentRequest.abort();
            currentRequest = null;
        }

        const term = searchInput.value.trim();

        const isNumeric = /^\d+$/.test(term);

        if (term.length < 2 && !isNumeric) {
            hideResults();
            return;
        }

        debounceTimer = setTimeout(function () {
            searchTenants(term);
        }, 300);
    });

    // KEYBOARD NAVIGATION
    searchInput.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            hideResults();
            return;
        }

        if (event.key === "ArrowDown") {
            const firstResult =
                resultsBox.querySelector("button");

            if (
                firstResult &&
                resultsBox.style.display !== "none"
            ) {
                event.preventDefault();
                firstResult.focus();
            }
        }
    });

    resultsBox.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            hideResults();
            searchInput.focus();
            return;
        }

        if (
            event.key !== "ArrowDown" &&
            event.key !== "ArrowUp"
        ) {
            return;
        }

        const buttons = Array.from(
            resultsBox.querySelectorAll("button")
        );

        const index = buttons.indexOf(
            document.activeElement
        );

        if (index === -1) {
            return;
        }

        event.preventDefault();

        if (event.key === "ArrowDown") {
            buttons[
                Math.min(index + 1, buttons.length - 1)
            ].focus();
        } else if (index === 0) {
            searchInput.focus();
        } else {
            buttons[index - 1].focus();
        }
    });

    // CLOSE WHEN CLICKING OUTSIDE
    document.addEventListener("click", function (event) {
        if (!container.contains(event.target)) {
            hideResults();
        }
    });
});