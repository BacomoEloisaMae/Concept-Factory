// ConceptFactory — Admin: Products Index
// Renders the garment + design-placement preview onto each thumbnail canvas,
// and handles the soft-delete confirmation modal.

document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.thumb-canvas').forEach(function (canvas) {
        const garmentSrc = canvas.dataset.garment;
        const designSrc = canvas.dataset.design;
        const fullW = 400, fullH = 440;
        const scaleX = canvas.width / fullW;
        const scaleY = canvas.height / fullH;
        const px = parseFloat(canvas.dataset.px) * scaleX;
        const py = parseFloat(canvas.dataset.py) * scaleY;
        const pw = parseFloat(canvas.dataset.pw) * scaleX;
        const ph = parseFloat(canvas.dataset.ph) * scaleY;
        const ctx = canvas.getContext('2d');
        const garment = new Image();
        garment.onload = function () {
            ctx.drawImage(garment, 0, 0, canvas.width, canvas.height);
            if (designSrc) {
                const design = new Image();
                design.crossOrigin = 'anonymous';
                design.onload = function () {
                    ctx.save();
                    ctx.beginPath();
                    ctx.rect(px, py, pw, ph);
                    ctx.clip();
                    ctx.globalAlpha = 0.92;
                    ctx.drawImage(design, px, py, pw, ph);
                    ctx.globalAlpha = 1.0;
                    ctx.restore();
                    ctx.globalAlpha = 0.07;
                    ctx.fillStyle = '#000';
                    ctx.fillRect(px, py, pw, ph);
                    ctx.globalAlpha = 1.0;
                };
                design.src = designSrc;
            }
        };
        garment.src = garmentSrc;
    });
});

function confirmDelete(id, name) {
    document.getElementById('deleteProductName').textContent = name;
    document.getElementById('deleteForm').action = '/Products/pSoftDelete/' + id;
    document.getElementById('deleteModal').style.display = 'flex';
}

function closeModal() {
    document.getElementById('deleteModal').style.display = 'none';
}
