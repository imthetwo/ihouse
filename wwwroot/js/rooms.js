document.addEventListener("DOMContentLoaded", function () {

    const rows = Array.from(
        document.querySelectorAll(".room-row")
    );

    const search = document.getElementById("roomSearch");
    const type = document.getElementById("typeFilter");
    const status = document.getElementById("statusFilter");

    const pageNumbers = document.getElementById("pageNumbers");
    const resultCount = document.getElementById("resultCount");
    const emptyMessage = document.getElementById("emptyMessage");

    const prevBtn = document.getElementById("prevBtn");
    const nextBtn = document.getElementById("nextBtn");

    const pageSize = 10;
    let currentPage = 1;

    // FILTER AND PAGINATION
    function render() {

        const keyword = search.value.trim().toLowerCase();

        const filtered = rows.filter(function (row) {

            const number =
                (row.dataset.number || "").toLowerCase();

            const roomType =
                (row.dataset.type || "").toLowerCase();

            return (
                number.includes(keyword) ||
                roomType.includes(keyword)
            ) &&
            (!type.value || row.dataset.type === type.value) &&
            (!status.value || row.dataset.status === status.value);
        });

        const total = filtered.length;

        const totalPages = Math.max(
            1,
            Math.ceil(total / pageSize)
        );

        currentPage = Math.min(currentPage, totalPages);

        // Hide all rows
        rows.forEach(function (row) {
            row.hidden = true;
        });

        // Show current page
        const start = (currentPage - 1) * pageSize;

        filtered.slice(start, start + pageSize)
            .forEach(function (row) {
                row.hidden = false;
            });

        // Empty message
        emptyMessage.classList.toggle("d-none", total > 0);

        // Result count
        const first = total === 0 ? 0 : start + 1;
        const last = Math.min(start + pageSize, total);

        resultCount.textContent =
            "Showing " + first + "-" + last +
            " of " + total + " rooms";

        // Page buttons
        pageNumbers.replaceChildren();

        for (let i = 1; i <= totalPages; i++) {

            const button = document.createElement("button");

            button.type = "button";
            button.textContent = i;

            button.className = i === currentPage
                ? "btn btn-sm btn-primary"
                : "btn btn-sm btn-outline-secondary";

            button.classList.add("room-page-button");

            button.onclick = function () {
                currentPage = i;
                render();
            };

            pageNumbers.appendChild(button);
        }

        prevBtn.disabled = currentPage === 1 || total === 0;
        nextBtn.disabled = currentPage === totalPages || total === 0;
    }

    // Reset pagination after filtering
    function resetPage() {
        currentPage = 1;
        render();
    }

    search.addEventListener("input", resetPage);
    type.addEventListener("change", resetPage);
    status.addEventListener("change", resetPage);

    document.getElementById("resetFilters").onclick = function () {
        search.value = "";
        type.value = "";
        status.value = "";

        resetPage();
    };

    // Previous
    prevBtn.onclick = function () {
        if (currentPage > 1) {
            currentPage--;
            render();
        }
    };

    // Next
    nextBtn.onclick = function () {
        currentPage++;
        render();
    };

    // Initial render
    render();
});
