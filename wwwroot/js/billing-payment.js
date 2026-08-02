// ConceptFactory — Billing & Payment page (UI PREVIEW ONLY)
//
// Connected to:
//   View       : Views/Home/hBilling.cshtml
//   Controller : Controllers/HomeController.cs -> hBilling()
//   Styles     : wwwroot/css/home/billing-payment.css
//
// Everything here is purely presentational: switching between the Gcash
// and Cash panels, the "Copy" button, and drag-and-drop visual feedback
// on the upload box. Nothing is saved or submitted anywhere yet — that's
// a separate follow-up once the page is wired to a real order/payment
// flow.
(function () {
    "use strict";

    function bpSelectMethod(method) {
        document.querySelectorAll(".bp-method").forEach(function (el) {
            el.classList.toggle("active", el.dataset.method === method);
        });

        const isGcash = method === "gcash";
        const gcashPanel = document.getElementById("bpPanelGcash");
        const cashPanel = document.getElementById("bpPanelCash");
        if (gcashPanel) gcashPanel.style.display = isGcash ? "" : "none";
        if (cashPanel) cashPanel.style.display = isGcash ? "none" : "";

        // The reference/upload row underneath is shared by both methods,
        // just relabeled so it still makes sense for Cash (no Gcash
        // reference number to speak of there).
        const refLabel = document.getElementById("bpRefLabel");
        const refHint = document.getElementById("bpRefHint");
        const uploadLabel = document.getElementById("bpUploadLabel");
        if (refLabel) refLabel.textContent = isGcash ? "Gcash reference number" : "Reference / note (optional)";
        if (refHint) refHint.textContent = isGcash
            ? "You can find this in your Gcash transaction details"
            : "Add anything the staff should know when you visit to pay";
        if (uploadLabel) uploadLabel.textContent = isGcash ? "Upload payment screenshot" : "Upload proof of payment (optional)";
    }

    function bpCopyGcashNumber(btn) {
        const number = "09512388661";
        const original = btn.textContent;
        const done = () => { btn.textContent = "Copied!"; setTimeout(() => { btn.textContent = original; }, 1500); };

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(number).then(done).catch(done);
        } else {
            done();
        }
    }

    function bpShowFileName(input) {
        const label = document.getElementById("bpUploadFileName");
        if (!label) return;
        label.textContent = (input.files && input.files.length > 0) ? input.files[0].name : "";
    }

    // Drag & drop visual feedback only — the drop still just populates
    // the same hidden file input, no upload happens.
    document.addEventListener("DOMContentLoaded", function () {
        const box = document.getElementById("bpUploadBox");
        const input = document.getElementById("bpUploadInput");
        if (!box || !input) return;

        ["dragenter", "dragover"].forEach(evt =>
            box.addEventListener(evt, e => { e.preventDefault(); box.classList.add("bp-dragover"); }));
        ["dragleave", "drop"].forEach(evt =>
            box.addEventListener(evt, e => { e.preventDefault(); box.classList.remove("bp-dragover"); }));

        box.addEventListener("drop", function (e) {
            if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                input.files = e.dataTransfer.files;
                bpShowFileName(input);
            }
        });
    });

    window.bpSelectMethod = bpSelectMethod;
    window.bpCopyGcashNumber = bpCopyGcashNumber;
    window.bpShowFileName = bpShowFileName;
})();
