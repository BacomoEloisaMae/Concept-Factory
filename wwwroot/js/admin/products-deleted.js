// ConceptFactory — Admin: Products Deleted
// Handles the restore, soft-delete, and permanent-delete confirmation modals.

function confirmRestore(id, name) {
    document.getElementById('restoreProductName').textContent = name;
    document.getElementById('restoreForm').action = '/Products/pRestore/' + id;
    document.getElementById('restoreModal').style.display = 'flex';
}

function confirmHardDelete(id, name) {
    document.getElementById('hardDeleteName').textContent = name;
    document.getElementById('hardDeleteForm').action = '/Products/pHardDelete/' + id;
    document.getElementById('hardDeleteModal').style.display = 'flex';
}

function closeAllModals() {
    document.getElementById('restoreModal').style.display = 'none';
    document.getElementById('hardDeleteModal').style.display = 'none';
}
