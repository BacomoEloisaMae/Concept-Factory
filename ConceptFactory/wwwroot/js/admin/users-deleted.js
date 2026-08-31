// ConceptFactory — Admin: Users → Deleted Staff
// Handles the restore and permanent-delete confirmation modals.

function confirmRestore(id, name) {
    document.getElementById('restoreProductName').textContent = name;
    document.getElementById('restoreForm').action = '/Users/uRestore/' + id;
    document.getElementById('restoreModal').style.display = 'flex';
}

function confirmHardDelete(id, name) {
    document.getElementById('hardDeleteName').textContent = name;
    document.getElementById('hardDeleteForm').action = '/Users/uHardDelete/' + id;
    document.getElementById('hardDeleteModal').style.display = 'flex';
}

function closeAllModals() {
    document.getElementById('restoreModal').style.display = 'none';
    document.getElementById('hardDeleteModal').style.display = 'none';
}
