// ConceptFactory — Admin: Services Index
// Handles the soft-delete confirmation modal.

function confirmDelete(id, name) {
    document.getElementById('deleteServiceName').textContent = name;
    document.getElementById('deleteForm').action = '/Services/sSoftDelete/' + id;
    document.getElementById('deleteModal').style.display = 'flex';
}

function closeModal() {
    document.getElementById('deleteModal').style.display = 'none';
}
