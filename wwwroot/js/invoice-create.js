"use strict";

document.addEventListener("DOMContentLoaded", function () {
    const contract = document.getElementById("ContractId");
    const invoiceDate = document.getElementById("InvoiceDate");
    const dueDate = document.getElementById("DueDate");
    const info = document.getElementById("contractInfo");
    const error = document.getElementById("contractLoadError");
    const submit = document.getElementById("createInvoiceButton");
    if (!contract || !invoiceDate || !dueDate || !info || !error || !submit) return;
    const url = contract.closest("form").dataset.periodUrl;
    let request;

    function dateISO(date) {
        return date.getFullYear() + "-" + String(date.getMonth() + 1).padStart(2, "0") + "-" + String(date.getDate()).padStart(2, "0");
    }

    function updateDueDate() {
        if (!invoiceDate.value) return;
        const date = new Date(invoiceDate.value + "T12:00:00");
        date.setDate(date.getDate() + 7);
        dueDate.min = invoiceDate.value;
        dueDate.value = dateISO(date);
    }

    async function loadContract(resetDates) {
        if (request) request.abort();
        info.classList.add("d-none");
        error.classList.add("d-none");
        submit.disabled = true;
        invoiceDate.removeAttribute("min");
        invoiceDate.removeAttribute("max");
        if (!contract.value || contract.value === "0") return;

        const current = new AbortController();
        request = current;
        try {
            const response = await fetch(url + "?id=" + encodeURIComponent(contract.value), {
                signal: current.signal, headers: { Accept: "application/json" }
            });
            if (!response.ok) throw new Error("Contract unavailable");
            const data = await response.json();
            if (request !== current) return;
            invoiceDate.min = data.startDate;
            invoiceDate.max = data.endDate;
            if (resetDates || !invoiceDate.value || invoiceDate.value < data.startDate || invoiceDate.value > data.endDate) {
                const today = dateISO(new Date());
                invoiceDate.value = today < data.startDate ? data.startDate : today > data.endDate ? data.endDate : today;
            }
            if (resetDates || !dueDate.value || dueDate.value < invoiceDate.value) updateDueDate();
            else dueDate.min = invoiceDate.value;

            function displayDate(value) { return value.split("-").reverse().join("/"); }
            document.getElementById("contractStart").textContent = displayDate(data.startDate);
            document.getElementById("contractEnd").textContent = displayDate(data.endDate);
            document.getElementById("contractRent").textContent = new Intl.NumberFormat("en-US", {
                style: "currency", currency: "USD"
            }).format(data.monthlyRent);
            info.classList.remove("d-none");
            submit.disabled = false;
        } catch (failure) {
            if (failure.name !== "AbortError" && request === current) error.classList.remove("d-none");
        } finally {
            if (request === current) request = null;
        }
    }

    contract.addEventListener("change", () => loadContract(true));
    invoiceDate.addEventListener("change", updateDueDate);
    document.getElementById("retryContract").addEventListener("click", () => loadContract(false));
    document.querySelectorAll(".invoice-date-input").forEach(input => {
        input.addEventListener("click", () => {
            try { input.showPicker?.(); } catch { /* The native date input still works. */ }
        });
    });
    loadContract(false);
});
