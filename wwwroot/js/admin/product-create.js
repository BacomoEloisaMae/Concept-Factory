// ConceptFactory — Admin: Products Create
// Color swatch picker, primary image upload/drag-drop, size multi-select,
// and the showcase (additional) images widget.

// ── Color Swatch Picker (each color can carry its own photo) ─
// `variants` holds one entry per swatch: { hex, id, existingImagePath, file, previewUrl }.
// `id` / `existingImagePath` stay 0/null on Create (there's nothing to
// keep yet); product-edit.js seeds them from the saved product instead.
(function () {
    const hidden = document.getElementById('colorHidden');
    const wrap = document.getElementById('colorPickerWrap');
    const picker = document.getElementById('nativeColorPicker');
    const inputsHost = document.getElementById('colorImageInputs');

    let variants = (hidden.value ? hidden.value.split(',').map(c => c.trim()).filter(Boolean) : [])
        .map(hex => ({ hex, id: 0, existingImagePath: null, file: null, previewUrl: null }));

    function sync() { hidden.value = variants.map(v => v.hex).join(', '); }

    function render() {
        wrap.innerHTML = '';
        inputsHost.innerHTML = '';

        variants.forEach((v, i) => {
            const sw = document.createElement('div');
            sw.className = 'color-swatch';
            sw.style.background = v.hex;
            sw.title = v.hex;

            const preview = v.previewUrl || v.existingImagePath;
            if (preview) {
                sw.style.backgroundImage = `url('${preview}')`;
                sw.style.backgroundSize = 'cover';
                sw.style.backgroundPosition = 'center';
            }

            const cam = document.createElement('span');
            cam.className = 'color-swatch-cam';
            cam.innerHTML = '<i class="fa-solid fa-camera"></i>';
            cam.title = 'Attach a photo for this color';
            cam.onclick = e => { e.stopPropagation(); pickColorImage(i); };
            sw.appendChild(cam);

            const rm = document.createElement('span');
            rm.className = 'rm';
            rm.textContent = '×';
            rm.onclick = e => { e.stopPropagation(); variants.splice(i, 1); sync(); render(); };
            sw.appendChild(rm);

            wrap.appendChild(sw);

            // Hidden inputs so ASP.NET Core can model-bind List<ProductColorImageInput>
            inputsHost.insertAdjacentHTML('beforeend', `
                <input type="hidden" name="ColorImages[${i}].ProductColorImageID" value="${v.id || 0}" />
                <input type="hidden" name="ColorImages[${i}].ColorHex" value="${v.hex}" />
                <input type="hidden" name="ColorImages[${i}].ExistingImagePath" value="${v.existingImagePath || ''}" />
            `);

            // Real file input carrying this swatch's photo (if any). Kept in
            // memory as a plain File object on the variant and re-attached
            // via DataTransfer on every render, so it survives reordering.
            const fileInput = document.createElement('input');
            fileInput.type = 'file';
            fileInput.style.display = 'none';
            fileInput.name = `ColorImages[${i}].NewImage`;
            if (v.file) {
                const dt = new DataTransfer();
                dt.items.add(v.file);
                fileInput.files = dt.files;
            }
            inputsHost.appendChild(fileInput);
        });
    }

    // Opens a one-off file picker for a single swatch's photo
    window.pickColorImage = function (i) {
        const filePicker = document.createElement('input');
        filePicker.type = 'file';
        filePicker.accept = 'image/*';
        filePicker.style.display = 'none';
        filePicker.onchange = function () {
            if (this.files?.[0]) {
                variants[i].file = this.files[0];
                variants[i].previewUrl = URL.createObjectURL(this.files[0]);
                render();
            }
            filePicker.remove();
        };
        document.body.appendChild(filePicker);
        filePicker.click();
    };

    // Exposed globally so the button can call it
    window.openColorPicker = function () { picker.click(); };

    picker.addEventListener('change', function () {
        const hex = this.value;
        if (hex && !variants.some(v => v.hex === hex)) {
            variants.push({ hex, id: 0, existingImagePath: null, file: null, previewUrl: null });
            sync();
            render();
        }
    });

    render();
})();

// ── Image Upload ─────────────────────────────────────────────
function handleImageUpload(input) {
    if (!input.files?.[0]) return;
    const reader = new FileReader();
    reader.onload = e => {
        document.getElementById('uploadPlaceholder').style.display = 'none';
        document.getElementById('imagePreviewContainer').style.display = 'block';
        document.getElementById('primaryImagePreview').src = e.target.result;
        document.getElementById('uploadedFileName').textContent = '✓ ' + input.files[0].name;
        document.getElementById('uploadArea').classList.add('upload-loaded');
    };
    reader.readAsDataURL(input.files[0]);
}

function handleDragOver(e) { e.preventDefault(); document.getElementById('uploadArea').classList.add('drag-over'); }
function handleDragLeave(e) { e.preventDefault(); document.getElementById('uploadArea').classList.remove('drag-over'); }
function handleDrop(e) {
    e.preventDefault();
    document.getElementById('uploadArea').classList.remove('drag-over');
    const file = e.dataTransfer.files[0];
    if (file?.type.startsWith('image/')) {
        const dt = new DataTransfer();
        dt.items.add(file);
        const input = document.getElementById('imageFile');
        input.files = dt.files;
        handleImageUpload(input);
    }
}

// ── Size Multi-Select Dropdown ───────────────────────────────
// Sizes are always arranged smallest → biggest, then "One Size" last,
// no matter what order the admin clicked them in.
const SIZE_ORDER = ['XS', 'S', 'M', 'L', 'XL', 'XXL', '2XL', 'XXXL', '3XL', '4XL', '5XL'];
function sortSizes(arr) {
    return arr.slice().sort((a, b) => {
        const na = a.trim().toUpperCase();
        const nb = b.trim().toUpperCase();
        const ia = SIZE_ORDER.indexOf(na);
        const ib = SIZE_ORDER.indexOf(nb);
        const ra = ia === -1 ? SIZE_ORDER.length : ia; // unknown sizes (incl. "One Size") sort after known ones
        const rb = ib === -1 ? SIZE_ORDER.length : ib;
        return ra - rb;
    });
}

(function () {
    const hidden = document.getElementById('sizeHidden');
    const items = document.querySelectorAll('#sizeList .size-list-item');
    const triggerText = document.getElementById('sizeTriggerText');
    let selected = hidden.value ? hidden.value.split(',').map(s => s.trim()).filter(Boolean) : [];

    function sync() {
        selected = sortSizes(selected);
        hidden.value = selected.join(', ');
        triggerText.textContent = selected.length ? selected.join(', ') : 'Select Size';
    }

    items.forEach(item => {
        if (selected.includes(item.dataset.size)) item.classList.add('selected');
        item.addEventListener('click', () => {
            const s = item.dataset.size;
            if (selected.includes(s)) {
                selected = selected.filter(x => x !== s);
                item.classList.remove('selected');
            } else {
                selected.push(s);
                item.classList.add('selected');
            }
            sync();
        });
    });

    document.addEventListener('click', e => {
        if (!document.getElementById('sizeDropdown').contains(e.target))
            closeSizeDropdown();
    });

    sync();
})();

// ── Size Dropdown Toggle ─────────────────────────────────────
function toggleSizeDropdown() {
    const open = document.getElementById('sizePanel').classList.contains('open');
    document.getElementById('sizePanel').classList.toggle('open', !open);
    document.getElementById('sizeChevron').classList.toggle('rotated', !open);
    document.getElementById('sizeTrigger').classList.toggle('active', !open);
}
function closeSizeDropdown() {
    document.getElementById('sizePanel').classList.remove('open');
    document.getElementById('sizeChevron').classList.remove('rotated');
    document.getElementById('sizeTrigger').classList.remove('active');
}

// ── Showcase Images — dynamic big-box / small-grid ──────────
const MAX_SHOWCASE = 6;
let showcaseItems = []; // [{ file, previewUrl }]

function pickShowcaseFiles() {
    const input = document.getElementById('showcaseFiles');
    input.setAttribute('multiple', 'multiple');
    input.onchange = function () {
        const files = Array.from(this.files);
        input.value = ''; // reset BEFORE syncShowcaseInput rebuilds input.files, so it isn't wiped out
        addShowcaseFiles(files);
    };
    input.click();
}

function replaceShowcaseItem(idx) {
    const input = document.getElementById('showcaseFiles');
    input.removeAttribute('multiple');
    input.onchange = function () {
        const file = this.files[0];
        input.value = ''; // reset BEFORE syncShowcaseInput rebuilds input.files, so it isn't wiped out
        if (file && file.type.startsWith('image/')) {
            showcaseItems[idx] = { file, previewUrl: URL.createObjectURL(file) };
            renderShowcase();
        }
    };
    input.click();
}

function addShowcaseFiles(files) {
    const room = MAX_SHOWCASE - showcaseItems.length;
    files.filter(f => f.type.startsWith('image/')).slice(0, room).forEach(file => {
        showcaseItems.push({ file, previewUrl: URL.createObjectURL(file) });
    });
    renderShowcase();
}

function removeShowcaseItem(idx) {
    showcaseItems.splice(idx, 1);
    renderShowcase();
}

function renderShowcase() {
    const bigBox = document.getElementById('scBigBox');
    const grid = document.getElementById('showcaseGrid');

    if (showcaseItems.length === 0) {
        bigBox.style.display = 'flex';
        grid.style.display = 'none';
        grid.innerHTML = '';
    } else {
        bigBox.style.display = 'none';
        grid.style.display = 'flex';

        let html = showcaseItems.map((item, i) => `
            <div class="sc-slot filled">
                <img src="${item.previewUrl}" alt="" />
                <button type="button" class="sc-slot-add-btn"
                        onclick="event.stopPropagation();replaceShowcaseItem(${i})" title="Change">
                    <i class="fa-solid fa-plus"></i>
                </button>
                <button type="button" class="sc-slot-rm"
                        onclick="event.stopPropagation();removeShowcaseItem(${i})" title="Remove">✕</button>
            </div>`).join('');

        if (showcaseItems.length < MAX_SHOWCASE) {
            html += `
            <div class="sc-slot empty" onclick="pickShowcaseFiles()">
                <i class="fa-solid fa-plus sc-slot-icon"></i>
            </div>`;
        }
        grid.innerHTML = html;
    }
    syncShowcaseInput();
}

function handleShowcaseDragOver(e) { e.preventDefault(); document.getElementById('scBigBox').classList.add('drag-over'); }
function handleShowcaseDragLeave(e) { e.preventDefault(); document.getElementById('scBigBox').classList.remove('drag-over'); }
function handleShowcaseDrop(e) {
    e.preventDefault();
    document.getElementById('scBigBox').classList.remove('drag-over');
    addShowcaseFiles(Array.from(e.dataTransfer.files));
}

function syncShowcaseInput() {
    const dt = new DataTransfer();
    showcaseItems.forEach(item => { if (item.file) dt.items.add(item.file); });
    document.getElementById('showcaseFiles').files = dt.files;
}
