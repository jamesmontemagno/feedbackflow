// Theme management
function initTheme() {
    const theme = localStorage.getItem('theme') || 'light';
    setTheme(theme);
}

function isDarkMode() {
    return document.documentElement.getAttribute('data-theme') === 'dark';
}

function setTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('theme', theme);
}

window.initTheme = initTheme;
window.setTheme = setTheme;
window.isDarkMode = isDarkMode;

// Clipboard utility for cross-browser compatibility, especially Safari
async function copyToClipboard(text) {
    try {
        // First try the modern clipboard API
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(text);
            return true;
        }
        
        // Fallback for older browsers or when clipboard API fails (e.g., Safari)
        return copyToClipboardFallback(text);
    } catch (error) {
        console.warn('Clipboard API failed, using fallback:', error);
        return copyToClipboardFallback(text);
    }
}

function copyToClipboardFallback(text) {
    try {
        // Create a temporary textarea element
        const textArea = document.createElement('textarea');
        textArea.value = text;
        
        // Make it invisible and position it off-screen
        textArea.style.position = 'fixed';
        textArea.style.left = '-999999px';
        textArea.style.top = '-999999px';
        textArea.setAttribute('readonly', '');
        textArea.setAttribute('aria-hidden', 'true');
        
        document.body.appendChild(textArea);
        
        // Select and copy the text
        textArea.focus();
        textArea.select();
        textArea.setSelectionRange(0, text.length);
        
        const successful = document.execCommand('copy');
        document.body.removeChild(textArea);
        
        return successful;
    } catch (error) {
        console.error('Fallback clipboard copy failed:', error);
        return false;
    }
}

// Make clipboard function available globally
window.copyToClipboard = copyToClipboard;

document.addEventListener('DOMContentLoaded', () => {
    // Add the toast container to the DOM if it doesn't exist
    if (!document.getElementById('toast-container')) {
        const toastContainer = document.createElement('div');
        toastContainer.id = 'toast-container';
        toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        document.body.appendChild(toastContainer);
    }
});
