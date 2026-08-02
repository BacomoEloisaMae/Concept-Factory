// ConceptFactory — Admin: Services Deleted
// Handles the restore and permanent-delete confirmation modals.

function confirmRestore(id, name) {
    document.getElementById('restoreServiceName').textContent = name;
    document.getElementById('restoreForm').action = '/Services/sRestore/' + id;
    document.getElementById('restoreModal').style.display = 'flex';
}

function confirmHardDelete(id, name) {
    document.getElementById('hardDeleteName').textContent = name;
    document.getElementById('hardDeleteForm').action = '/Services/sHardDelete/' + id;
    document.getElementById('hardDeleteModal').style.display = 'flex';
}

function closeAllModals() {
    document.getElementById('restoreModal').style.display = 'none';
    document.getElementById('hardDeleteModal').style.display = 'none';
}

// Kept for backward compatibility with any markup still calling closeModal()
function closeModal() {
    closeAllModals();
}
