/* ============================================================
   CUSTOMIZE-DETAIL.JS
   Page logic for Views/Customize/custom.cshtml (the "build your own"
   product page) — color gallery, design upload, print-location overlay,
   silhouette masking, printing services, and the exit-confirmation modal.

   Everything here is specific to THIS page. The logic shared with
   Views/Home/hDetails.cshtml (size/color chip selection, quantity + bulk
   discount, Add to Cart / Buy Now, validation) lives in product-details.js
   instead, so hDetails.cshtml never has to load code it doesn't use.
   The two files talk through a couple of small global hooks
   (window.onMainImageChanged, window.getCustomizeCartExtras) that this
   file defines and product-details.js calls IF they exist.

   Depends on: storefront.js (window.CF), product-details.js,
   window.CF_PRODUCT, window.CF_COLOR_GALLERIES (set inline by
   custom.cshtml — see the CF_* convention note there).
============================================================ */
(function () {
    "use strict";

    // ── Exit-confirmation modal for the "Back to Home" link ──────────────
    function requestExitCustomize() {
        document.getElementById("exitConfirmOverlay").classList.add("is-open");
        return false; // always block the link's default navigation until confirmed
    }
    function closeExitConfirm() {
        document.getElementById("exitConfirmOverlay").classList.remove("is-open");
    }
    function confirmExitCustomize() {
        if (document.referrer && document.referrer.indexOf(window.location.host) !== -1) {
            history.back();
        } else {
            window.location.href = window.CF_PRODUCT.homeUrl;
        }
    }

    // Tracks the currently selected color, so the Front/Back print-location
    // buttons know which color's back photo to look up.
    let currentColorName = null;
    // Tracks which SIDE the customer last explicitly asked to view — only
    // "Front" and "Back" change this. Other locations (Left Chest, Front/Back,
    // etc.) just keep showing whichever side was already up, instead of
    // yanking the photo back to front every time a different location is picked.
    let currentViewPreference = "front";

    // Clips a design overlay to the CURRENT photo's real garment
    // silhouette, using that photo's own alpha-channel transparency.
    // maskId lets this serve both the single #designMask and, for
    // Front/Back's side-by-side view, #dualFrontMask/#dualBackMask.
    function applyMaskTo(maskId, src) {
        const mask = document.getElementById(maskId);
        if (!mask || !src) return;
        const url = `url('${src}')`;
        mask.style.webkitMaskImage = url;
        mask.style.maskImage = url;
    }
    function applyDesignMask(src) {
        applyMaskTo("designMask", src);
    }

    // A design only makes sense to preview on a front or back view — not on
    // a sleeve or neck close-up. Hides the overlay entirely (including the
    // "YOUR DESIGN HERE" placeholder) whenever the photo currently shown
    // isn't one of those two.
    function updateDesignOverlayVisibility() {
        const overlay = document.getElementById("designOverlay");
        const mainImg = document.getElementById("mainImg");
        if (!overlay || !mainImg) return;
        const viewType = mainImg.dataset.viewType || "front";
        overlay.style.display = (viewType === "front" || viewType === "back") ? "" : "none";
    }

    // Hook called by switchMainImage() in product-details.js whenever the
    // main photo changes (thumbnail click, etc.)
    window.onMainImageChanged = function (src, viewType) {
        applyDesignMask(src);
        const mainImg = document.getElementById("mainImg");
        if (viewType && mainImg) mainImg.dataset.viewType = viewType;
        renderDesignOverlay();
    };

    function findGalleryPhoto(colorName, viewType) {
        const gallery = (window.CF_COLOR_GALLERIES || {})[colorName] || [];
        return gallery.find(p => p.ViewType === viewType);
    }

    // Shows whichever photo matches BOTH the current color AND
    // currentViewPreference (front or back). Called whenever the color
    // changes, or whenever Front/Back is explicitly picked.
    function showImageForCurrentSelection() {
        if (!currentColorName) return;

        let photo = findGalleryPhoto(currentColorName, currentViewPreference);
        if (!photo) photo = findGalleryPhoto(currentColorName, "front"); // graceful fallback if no back photo exists yet
        if (!photo) return;

        const mainImg = document.getElementById("mainImg");
        if (mainImg) {
            if (mainImg.dataset) mainImg.dataset.fallback = "";
            mainImg.dataset.viewType = photo.ViewType;
            mainImg.src = photo.ImagePath;
            applyDesignMask(photo.ImagePath);
        }
        renderDesignOverlay();

        // Keep the thumbnail strip's highlight in sync, if that photo is
        // one of the currently visible thumbs.
        document.querySelectorAll("#pdThumbs .pd-thumb").forEach(t => {
            t.classList.toggle("active", t.querySelector("img")?.getAttribute("src") === photo.ImagePath);
        });
    }

    // Called when a color swatch is clicked: rebuilds the thumbnail strip
    // to that color's full photo set, then shows whichever photo matches
    // the currently selected print location (front/back) for that color.
    function selectCustomizeColor(colorName) {
        currentColorName = colorName;
        const gallery = (window.CF_COLOR_GALLERIES || {})[colorName] || [];
        renderGalleryThumbs(gallery);
        showImageForCurrentSelection();
        renderDualPhotos(); // keep Front/Back's two panes in sync with the new color too
    }

    // Rebuilds the #pdThumbs strip from a color's photo gallery (main photo
    // + any other angles). Clicking a thumb still uses the shared
    // switchMainImage() from product-details.js to swap the big picture,
    // now passing the photo's view type along too.
    function renderGalleryThumbs(gallery) {
        const host = document.getElementById("pdThumbs");
        if (!host) return;
        if (!gallery || !gallery.length) { host.innerHTML = ""; return; }

        host.innerHTML = gallery.map((p, i) => `
            <div class="pd-thumb${i === 0 ? " active" : ""}" onclick="switchMainImage(this, '${p.ImagePath}', '${p.ViewType}')">
                <img src="${p.ImagePath}" alt="${p.ViewType}" class="cz-thumb-img" />
            </div>
        `).join("");
    }

    // ── Front/Back side-by-side view ──────────────────────────────────
    // "Front / Back" is the one print location that shows BOTH sides at
    // once instead of a single current side, so it swaps #imgBox out for
    // #dualViewBox entirely rather than reusing the single mainImg/overlay.
    function showSingleView() {
        const imgBox = document.getElementById("imgBox");
        const dualBox = document.getElementById("dualViewBox");
        if (imgBox) imgBox.style.display = "";
        if (dualBox) dualBox.style.display = "none";
    }
    function showDualView() {
        const imgBox = document.getElementById("imgBox");
        const dualBox = document.getElementById("dualViewBox");
        if (imgBox) imgBox.style.display = "none";
        if (dualBox) dualBox.style.display = "flex";
        renderDualPhotos();
        renderDualOverlays();
    }

    // Puts the current color's front photo in the left pane and its back
    // photo in the right pane (falling back to the front photo if no back
    // angle exists yet for this color).
    function renderDualPhotos() {
        const frontImg = document.getElementById("dualFrontImg");
        const backImg = document.getElementById("dualBackImg");
        if (!frontImg || !backImg || !currentColorName) return;

        const frontPhoto = findGalleryPhoto(currentColorName, "front");
        const backPhoto = findGalleryPhoto(currentColorName, "back") || frontPhoto;

        if (frontPhoto) {
            frontImg.src = frontPhoto.ImagePath;
            applyMaskTo("dualFrontMask", frontPhoto.ImagePath);
        }
        if (backPhoto) {
            backImg.src = backPhoto.ImagePath;
            applyMaskTo("dualBackMask", backPhoto.ImagePath);
        }
    }

    // Front/Back upload rule: ONE uploaded design mirrors onto both sides
    // identically (the common case — same print front and back); a SECOND
    // upload lets the customer put a different design on the back
    // specifically (1st = front, 2nd = back), same ordering as everywhere
    // else in this file.
    function renderDualOverlays() {
        const frontOverlay = document.getElementById("dualFrontOverlay");
        const backOverlay = document.getElementById("dualBackOverlay");
        if (!frontOverlay || !backOverlay) return;

        const frontDesign = uploadedFiles[0];
        const backDesign = uploadedFiles.length >= 2 ? uploadedFiles[1] : uploadedFiles[0];

        setDualPane(frontOverlay, frontDesign, "FRONT");
        setDualPane(backOverlay, backDesign, "BACK");
    }
    function setDualPane(overlay, design, label) {
        if (design) {
            overlay.innerHTML = `<img src="${design.dataUrl}" alt="Your design" />`;
            overlay.classList.add("has-design");
        } else {
            overlay.innerHTML = `${label}<br>DESIGN<br>HERE`;
            overlay.classList.remove("has-design");
        }
    }


    let uploadedFiles = [];       // { file, dataUrl, thumbDataUrl } — index 0 = Front design, index 1 = Back design
    let designScale = 1;          // customer-controlled size multiplier, from the size slider

    // Which design applies to a given view: the FIRST photo uploaded prints
    // on the front, the SECOND prints on the back. (A dedicated "combine
    // front+back into one upload" flow can replace this later — for now
    // it's simply upload order.)
    function designForViewType(viewType) {
        if (viewType === "front") return uploadedFiles[0];
        if (viewType === "back") return uploadedFiles[1];
        return undefined;
    }

    function currentViewType() {
        const mainImg = document.getElementById("mainImg");
        return (mainImg && mainImg.dataset.viewType) || "front";
    }

    // Shows whichever design belongs to the CURRENTLY displayed side
    // (front or back) — or a placeholder prompting for that specific side
    // if nothing's been uploaded for it yet. Called whenever the photo
    // shown changes (color, print location) as well as after every upload/remove.
    function renderDesignOverlay() {
        const overlay = document.getElementById("designOverlay");
        if (!overlay) return;

        const viewType = currentViewType();
        const design = designForViewType(viewType);
        const sizeRow = document.getElementById("designSizeRow");

        if (design) {
            overlay.innerHTML = `<img src="${design.dataUrl}" alt="Your design" />`;
            overlay.classList.add("has-design");
            if (sizeRow) sizeRow.classList.add("is-visible");
        } else {
            overlay.innerHTML = viewType === "back" ? "BACK<br>DESIGN<br>HERE" : "FRONT<br>DESIGN<br>HERE";
            overlay.classList.remove("has-design");
            if (sizeRow) sizeRow.classList.remove("is-visible");
        }
        applyOverlayTransform();
        updateDesignOverlayVisibility();
        renderDualOverlays(); // keep Front/Back's two panes in sync too, regardless of which view is currently visible
    }

    // Applies the slider's size multiplier on top of the overlay's normal
    // centering transform. Scale is applied around the element's own
    // center (default transform-origin), so it grows/shrinks in place
    // instead of drifting off its print-location position.
    function applyOverlayTransform() {
        const scaleStr = `translate(-50%, -50%) scale(${designScale})`;
        const overlay = document.getElementById("designOverlay");
        if (overlay) overlay.style.transform = scaleStr;
        const dualFront = document.getElementById("dualFrontOverlay");
        if (dualFront) dualFront.style.transform = scaleStr;
        const dualBack = document.getElementById("dualBackOverlay");
        if (dualBack) dualBack.style.transform = scaleStr;
    }

    // Called by the "Design size" slider under Upload Your Design.
    function setDesignScale(percent) {
        designScale = Number(percent) / 100;
        const label = document.getElementById("designSizeValue");
        if (label) label.textContent = percent + "%";
        applyOverlayTransform();
    }

    // Moves the design overlay to sit over the chosen print location.
    // Position/size per location is handled in CSS via
    // .pd-design-overlay[data-location="..."] — this just flips the flag.
    function setPrintLocation(slug) {
        const overlay = document.getElementById("designOverlay");
        if (!overlay) return;
        overlay.dataset.location = slug;

        // "front" / "back" explicitly pick a side. Chest/sleeve locations
        // are inherently front-of-garment, so they switch to front too if
        // the customer was looking at the back. "front-back" (design on
        // both sides) and the bag locations ("center"/"edges") deliberately
        // do NOT change currentViewPreference — they just keep showing
        // whichever side was already up, since forcing a jump back to
        // front there would undo a "Back" choice for no reason.
        if (slug === "front" || slug === "left-chest" || slug === "right-chest" ||
            slug === "left-sleeve" || slug === "right-sleeve" || slug === "center" || slug === "edges") {
            currentViewPreference = "front";
        } else if (slug === "back") {
            currentViewPreference = "back";
        }

        if (slug === "front-back") {
            showDualView();
        } else {
            showSingleView();
            showImageForCurrentSelection();
        }
    }

    // Renders the small thumbnail strip under the upload button, labeled by
    // which print side each upload maps to (1st = Front, 2nd = Back).
    function renderUploadPreviews() {
        const host = document.getElementById("uploadPreviews");
        if (!host) return;

        host.innerHTML = "";
        uploadedFiles.forEach((f, i) => {
            const label = i === 0 ? "Front" : i === 1 ? "Back" : "Extra";
            const item = document.createElement("div");
            item.className = "pd-upload-preview-item";
            item.innerHTML = `
                <img src="${f.dataUrl}" alt="${f.file.name}" title="${label} design" />
                <span class="pd-upload-preview-label">${label}</span>
                <span class="rm">&times;</span>
            `;
            item.querySelector(".rm").onclick = (e) => {
                e.stopPropagation();
                uploadedFiles.splice(i, 1);
                renderUploadPreviews();
                renderDesignOverlay();
            };
            host.appendChild(item);
        });
    }

    // Reads the newly chosen design file(s) and previews them. The 1st
    // photo (across all uploads so far) prints on the front, the 2nd on
    // the back — order matters, so files are appended in the order chosen.
    function handleFiles(input) {
        if (!input.files || !input.files.length) return;

        Array.from(input.files).forEach(file => {
            if (!file.type.startsWith("image/")) return; // PDFs get listed but skip the visual overlay
            const reader = new FileReader();
            reader.onload = e => {
                const fullDataUrl = e.target.result;
                makeThumbnail(fullDataUrl, thumbDataUrl => {
                    uploadedFiles.push({ file, dataUrl: fullDataUrl, thumbDataUrl });
                    renderUploadPreviews();
                    renderDesignOverlay();
                });
            };
            reader.readAsDataURL(file);
        });

        input.value = ""; // allow re-selecting the same file later
    }

    // Full-resolution uploaded photos can easily be 1-2MB+ as a base64 data
    // URL — way too big to store in the cart (localStorage typically caps
    // out around 5-10MB total, and silently fails to save past that, which
    // makes Add to Cart look broken with no error shown). This shrinks the
    // design down to a small on-disk-cheap thumbnail (max 160px) before it
    // ever reaches the cart; the full-res version stays in memory only, for
    // the on-page preview/overlay.
    function makeThumbnail(dataUrl, callback) {
        const img = new Image();
        img.onload = () => {
            const maxDim = 160;
            const scale = Math.min(1, maxDim / Math.max(img.width, img.height));
            const canvas = document.createElement("canvas");
            canvas.width = Math.max(1, Math.round(img.width * scale));
            canvas.height = Math.max(1, Math.round(img.height * scale));
            const ctx = canvas.getContext("2d");
            ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
            try {
                callback(canvas.toDataURL("image/jpeg", 0.7));
            } catch (e) {
                callback(null); // e.g. tainted canvas — just skip the thumbnail, not fatal
            }
        };
        img.onerror = () => callback(null);
        img.src = dataUrl;
    }

    // Toggle-select for printing service cards (a customer can combine
    // more than one service, e.g. embroidery + screen print).
    function selectSvc(el) {
        const wasActive = el.classList.contains("active");
        document.querySelectorAll(".pd-svc.active").forEach(s => s.classList.remove("active"));
        if (!wasActive) el.classList.add("active");
    }

    // Percentage placement/size for each print location, mirroring the
    // .pd-design-overlay[data-location="..."] CSS rules exactly — kept in
    // one place so the on-page preview and the flattened cart thumbnail
    // (buildCompositeThumbnail) always agree on where the design sits on
    // the garment.
    const DESIGN_LOCATION_MAP = {
        "front":        { top: 45, left: 50, w: 30, h: 30 },
        "back":         { top: 45, left: 50, w: 30, h: 30 },
        "front-back":   { top: 45, left: 50, w: 32, h: 32 },
        "left-chest":   { top: 31, left: 40, w: 11, h: 11 },
        "right-chest":  { top: 31, left: 60, w: 11, h: 11 },
        "left-sleeve":  { top: 28, left: 24, w: 30, h: 30 },
        "right-sleeve": { top: 28, left: 76, w: 30, h: 30 },
        "center":       { top: 60, left: 50, w: 32, h: 32 },
        "right":        { top: 85, left: 80, w: 55, h: 55 },
        "left":         { top: 85, left: 50, w: 55, h: 55 },
        "full":         { top: 50, left: 50, w: 100, h: 100 }


    };

    // Flattens the CURRENTLY shown garment photo + whichever design is
    // printed on that side into a single square image — so the cart
    // thumbnail shows the design actually sitting on the shirt (same
    // color, same spot) instead of the garment and design as two
    // separate squares. Runs synchronously off the already-loaded
    // <img> elements already on the page, so no extra network/image
    // load is needed.
    function buildCompositeThumbnail() {
        const activeLocation = (document.getElementById("designOverlay") || {}).dataset?.location;
        if (activeLocation === "front-back") return buildDualCompositeThumbnail();

        const mainImg = document.getElementById("mainImg");
        if (!mainImg || !mainImg.src) return mainImg ? mainImg.src : "";

        const overlay = document.getElementById("designOverlay");
        const designImg = overlay ? overlay.querySelector("img") : null;
        // Nothing uploaded for the side currently shown — just use the
        // plain colored garment photo, same as before.
        if (!designImg || !designImg.complete) return mainImg.src;

        try {
            const size = 500;
            const canvas = document.createElement("canvas");
            canvas.width = size;
            canvas.height = size;
            const ctx = canvas.getContext("2d");

            // Draw the garment photo "contain"-fit, matching how it's
            // displayed inside .pd-img-box on the page.
            const iw = mainImg.naturalWidth || size;
            const ih = mainImg.naturalHeight || size;
            const fit = Math.min(size / iw, size / ih);
            const dw = iw * fit, dh = ih * fit;
            const dx = (size - dw) / 2, dy = (size - dh) / 2;
            ctx.drawImage(mainImg, dx, dy, dw, dh);

            // Place the design using the same percentage box the on-page
            // overlay uses for this print location, scaled by the
            // customer's design-size slider. On the page, the overlay's
            // top/left/width/height percentages are relative to the FULL
            // square photo box (.pd-img-box), not to the garment photo's
            // own contain-fit dimensions (dw/dh) — those can be smaller
            // than the box whenever the garment photo isn't perfectly
            // square, so the reference here has to be the box (size),
            // matching the CSS, or the composited design ends up the
            // wrong size relative to what the customer actually saw.
            const loc = overlay.dataset.location || "front";
            const p = DESIGN_LOCATION_MAP[loc] || DESIGN_LOCATION_MAP.front;
            const mul = (typeof designScale === "number" && designScale > 0) ? designScale : 1;

            const designW = size * (p.w / 100) * mul;
            const designH = size * (p.h / 100) * mul;
            const cx = size * (p.left / 100);
            const cy = size * (p.top / 100);

            // Clip the design to the garment's real silhouette, mirroring
            // what the live-preview CSS mask does (mask-mode: alpha off
            // the same photo). "source-atop" only paints the new draw
            // (the design) over pixels the canvas already has — i.e. the
            // garment photo just drawn above — so the design can never
            // show up past the garment's own opaque pixels, even near an
            // edge like a collar or sleeve seam.
            ctx.globalCompositeOperation = "source-atop";
            ctx.drawImage(designImg, cx - designW / 2, cy - designH / 2, designW, designH);
            ctx.globalCompositeOperation = "source-over";

            // canvas above still has real transparency (e.g. letterboxing
            // if the garment photo isn't perfectly square, or a cutout
            // photo's own transparent background). toDataURL("image/jpeg")
            // can't keep that transparency and would flatten it to solid
            // black, so composite it one more time onto a canvas
            // pre-filled with the same light background color the on-page
            // preview uses (.pd-img-main's --pd-primary-bg) — that way the
            // cart thumbnail is framed the same way the customize page
            // shows it instead of with harsh black bars.
            const finalCanvas = document.createElement("canvas");
            finalCanvas.width = size;
            finalCanvas.height = size;
            const finalCtx = finalCanvas.getContext("2d");
            finalCtx.fillStyle = "#f0f1ff";
            finalCtx.fillRect(0, 0, size, size);
            finalCtx.drawImage(canvas, 0, 0);

            return finalCanvas.toDataURL("image/jpeg", 0.85);
        } catch (e) {
            return mainImg.src; // e.g. tainted canvas — fall back to the plain garment photo
        }
    }

    // Same idea as buildCompositeThumbnail() above, but for the Front/Back
    // print location: composites BOTH dual panes (garment photo + whichever
    // design belongs to that side) side by side into one square image, so
    // the cart thumbnail shows the same two-panel front+back preview the
    // customer saw on this page instead of just one side.
    function buildDualCompositeThumbnail() {
        const frontImg = document.getElementById("dualFrontImg");
        const backImg = document.getElementById("dualBackImg");
        if (!frontImg || !frontImg.src) return frontImg ? frontImg.src : "";

        try {
            const size = 500;
            const halfW = size / 2;
            const canvas = document.createElement("canvas");
            canvas.width = size;
            canvas.height = size;
            const ctx = canvas.getContext("2d");

            function drawPane(img, overlay, offsetX, paneW) {
                if (!img || !img.src) return;
                const iw = img.naturalWidth || paneW;
                const ih = img.naturalHeight || size;
                const fit = Math.min(paneW / iw, size / ih);
                const dw = iw * fit, dh = ih * fit;
                const dx = offsetX + (paneW - dw) / 2, dy = (size - dh) / 2;
                ctx.drawImage(img, dx, dy, dw, dh);

                const designImg = overlay ? overlay.querySelector("img") : null;
                if (!designImg || !designImg.complete) return;

                const loc = (overlay.dataset.location || "front");
                const p = DESIGN_LOCATION_MAP[loc] || DESIGN_LOCATION_MAP.front;
                const mul = (typeof designScale === "number" && designScale > 0) ? designScale : 1;
                const designW = paneW * (p.w / 100) * mul;
                const designH = size * (p.h / 100) * mul;
                const cx = offsetX + paneW * (p.left / 100);
                const cy = size * (p.top / 100);

                // Clip to this pane only (so a design near a pane's edge
                // can't spill into the OTHER pane), then use the same
                // source-atop silhouette-masking trick as the single-view
                // composite above.
                ctx.save();
                ctx.beginPath();
                ctx.rect(offsetX, 0, paneW, size);
                ctx.clip();
                ctx.globalCompositeOperation = "source-atop";
                ctx.drawImage(designImg, cx - designW / 2, cy - designH / 2, designW, designH);
                ctx.globalCompositeOperation = "source-over";
                ctx.restore();
            }

            drawPane(frontImg, document.getElementById("dualFrontOverlay"), 0, halfW);
            drawPane(backImg, document.getElementById("dualBackOverlay"), halfW, halfW);

            // Thin divider between the two panes, matching the on-page gap.
            ctx.strokeStyle = "rgba(255,255,255,.7)";
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.moveTo(halfW, 0);
            ctx.lineTo(halfW, size);
            ctx.stroke();

            const finalCanvas = document.createElement("canvas");
            finalCanvas.width = size;
            finalCanvas.height = size;
            const finalCtx = finalCanvas.getContext("2d");
            finalCtx.fillStyle = "#f0f1ff";
            finalCtx.fillRect(0, 0, size, size);
            finalCtx.drawImage(canvas, 0, 0);

            return finalCanvas.toDataURL("image/jpeg", 0.85);
        } catch (e) {
            return frontImg.src; // e.g. tainted canvas — fall back to the plain front photo
        }
    }

    // Hook called by buildCartItem() in product-details.js: folds in
    // services chosen, print location, freeform location note, and a
    // flattened thumbnail with the design sitting on the garment itself.
    window.getCustomizeCartExtras = function () {
        const extras = { isCustomOrder: true };

        const designOverlay = document.getElementById("designOverlay");
        if (!designOverlay) return extras;

        const selectedServices = Array.from(document.querySelectorAll(".pd-svc.active"));
        extras.service = selectedServices.map(s => s.dataset.name).join(", ");
        extras.servicePrice = selectedServices.reduce((sum, s) => sum + (parseFloat(s.dataset.price) || 0), 0);

        extras.printLocation = designOverlay.dataset.location || "";

        const note = document.querySelector(".pd-loc-text")?.value.trim();
        if (note) extras.locationNote = note;

        // Composite the design directly onto the colored garment photo,
        // so the cart (and any other place that reads item.image) shows
        // exactly what the customer saw on this page — same color, same
        // design, same spot — as one image instead of a separate square.
        extras.image = buildCompositeThumbnail();

        // Kept for informational text only (e.g. the "Design: Front +
        // Back" note in the cart's item details) — no longer rendered
        // as its own square image. In Front/Back with only one upload,
        // that design mirrors onto the back too, so it counts as "has a
        // back design" here as well.
        const isFrontBack = designOverlay.dataset.location === "front-back";
        extras.hasFrontDesign = !!uploadedFiles[0];
        extras.hasBackDesign = !!uploadedFiles[1] || (isFrontBack && !!uploadedFiles[0]);
        extras.designCount = uploadedFiles.length;

        return extras;
    };

    document.addEventListener("DOMContentLoaded", function () {
        // Sets correct initial state (placeholder text + visibility) on
        // page load, and picks up window.CF_PRODUCT's first-color default.
        currentColorName = (window.CF_CUSTOMIZE_DEFAULT_COLOR || null);
        renderDesignOverlay();
    });

    // Expose handlers referenced by inline onclick=/onchange=/oninput=
    // attributes in the view.
    window.requestExitCustomize = requestExitCustomize;
    window.closeExitConfirm = closeExitConfirm;
    window.confirmExitCustomize = confirmExitCustomize;
    window.setPrintLocation = setPrintLocation;
    window.setDesignScale = setDesignScale;
    window.handleFiles = handleFiles;
    window.selectSvc = selectSvc;
    window.selectCustomizeColor = selectCustomizeColor;

})();
