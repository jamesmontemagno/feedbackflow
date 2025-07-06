// Toast notification service for FeedbackFlow
export function showToast(message, type = 'success', duration = 3000, position = 'bottom-end') {
    // Check if bootstrap is available
    if (typeof bootstrap === 'undefined' || !bootstrap.Toast) {
        console.error('Bootstrap Toast functionality is not available');
        return;
    }

    // Map position to CSS classes
    const positionClasses = {
        'top-start': 'position-fixed top-0 start-0',
        'top-center': 'position-fixed top-0 start-50 translate-middle-x',
        'top-end': 'position-fixed top-0 end-0',
        'middle-start': 'position-fixed top-50 start-0 translate-middle-y',
        'middle-center': 'position-fixed top-50 start-50 translate-middle',
        'middle-end': 'position-fixed top-50 end-0 translate-middle-y',
        'bottom-start': 'position-fixed bottom-0 start-0',
        'bottom-center': 'position-fixed bottom-0 start-50 translate-middle-x',
        'bottom-end': 'position-fixed bottom-0 end-0'
    };
    
    const positionClass = positionClasses[position] || positionClasses['bottom-end'];
    
    // Get or create toast container for this position
    const containerId = `toast-container-${position}`;
    let toastContainer = document.getElementById(containerId);
    if (!toastContainer) {
        toastContainer = document.createElement('div');
        toastContainer.id = containerId;
        toastContainer.className = `toast-container ${positionClass} p-3`;
        document.body.appendChild(toastContainer);
    }    // Create toast element
    const toastElement = document.createElement('div');
    toastElement.className = 'toast';
    toastElement.setAttribute('role', 'alert');
    toastElement.setAttribute('aria-live', 'assertive');
    toastElement.setAttribute('aria-atomic', 'true');

    // Determine background color based on type
    const bgClass = type === 'success' ? 'bg-success' : 
                    type === 'warning' ? 'bg-warning' : 
                    type === 'danger' ? 'bg-danger' : 
                    type === 'info' ? 'bg-info' : 'bg-primary';

    // Set toast content with icon based on type
    const iconMap = {
        success: 'bi-check-circle',
        warning: 'bi-exclamation-triangle',
        danger: 'bi-x-circle',
        info: 'bi-info-circle',
        primary: 'bi-bell'
    };
    
    const icon = iconMap[type] || iconMap.primary;
    
    toastElement.innerHTML = `
        <div class="toast-header ${bgClass}">
            <i class="bi ${icon} me-2"></i>
            <strong class="me-auto">FeedbackFlow</strong>
            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
        <div class="toast-body ${bgClass}">
            <span>${message}</span>
        </div>
    `;

    // Add toast to container
    toastContainer.appendChild(toastElement);

    // Initialize Bootstrap toast
    const toast = new bootstrap.Toast(toastElement, {
        autohide: true,
        delay: duration
    });

    // Show toast
    toast.show();    // Remove toast element after it's hidden
    toastElement.addEventListener('hidden.bs.toast', () => {
        toastElement.remove();
        
        // Remove container if empty
        if (toastContainer.children.length === 0) {
            toastContainer.remove();
        }
    });

    return toast;
}
