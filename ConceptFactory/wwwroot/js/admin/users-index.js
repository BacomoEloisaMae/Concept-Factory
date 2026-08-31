// ConceptFactory — Admin: User Management (uIndex.cshtml)
// Handles the soft-delete confirmation modal for Staff rows.

function confirmDelete(id, name) {
    document.getElementById('deleteProductName').textContent = name;
    document.getElementById('deleteForm').action = '/Users/uSoftDelete/' + id;
    document.getElementById('deleteModal').style.display = 'flex';
}

function closeModal() {
    document.getElementById('deleteModal').style.display = 'none';
}
