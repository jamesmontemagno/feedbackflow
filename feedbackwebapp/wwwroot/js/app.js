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
        // Check if clipboard API is available and we're in a secure context
        if (navigator.clipboard && window.isSecureContext) {
            // Safari-specific: Check clipboard permissions first
            if (navigator.permissions) {
                try {
                    const permission = await navigator.permissions.query({name: 'clipboard-write'});
                    if (permission.state === 'denied') {
                        console.warn('Clipboard permission denied, using fallback');
                        return copyToClipboardFallback(text);
                    }
                } catch (permError) {
                    // Permission API not supported or failed, continue with clipboard attempt
                    console.warn('Permission check failed, continuing with clipboard attempt:', permError);
                }
            }
            
            // Attempt to write to clipboard
            await navigator.clipboard.writeText(text);
            return true;
        }
        
        // Fallback for older browsers or when clipboard API not available
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
        textArea.style.opacity = '0';
        textArea.style.pointerEvents = 'none';
        textArea.setAttribute('readonly', '');
        textArea.setAttribute('aria-hidden', 'true');
        
        document.body.appendChild(textArea);
        
        // Select and copy the text - Safari requires focus first
        textArea.focus();
        textArea.select();
        
        // For Safari iOS, we need to set the selection range explicitly
        if (textArea.setSelectionRange) {
            textArea.setSelectionRange(0, text.length);
        }
        
        // Use the deprecated execCommand as last resort
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

// Function to select all text in a textarea/input element
function selectAllText(element) {
    try {
        element.focus();
        element.select();
        if (element.setSelectionRange) {
            element.setSelectionRange(0, element.value.length);
        }
    } catch (error) {
        console.error('Error selecting text:', error);
    }
}

window.selectAllText = selectAllText;

// File download utility
function downloadFile(dataUrl, fileName) {
    try {
        const link = document.createElement('a');
        link.href = dataUrl;
        link.download = fileName;
        link.style.display = 'none';
        
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        return true;
    } catch (error) {
        console.error('Error downloading file:', error);
        return false;
    }
}

window.downloadFile = downloadFile;

// Easy Auth utility function - simplified for server-side authentication
async function fetchAuthMe() {
    try {
        console.log('fetchAuthMe: Starting request to /.auth/me');
        
        const response = await fetch('/.auth/me', {
            method: 'GET',
            credentials: 'include', // Include cookies for authentication
            headers: {
                'Accept': 'application/json',
                'Cache-Control': 'no-cache'
            }
        });
        
        console.log('fetchAuthMe: Response status:', response.status);
        
        if (!response.ok) {
            console.warn('fetchAuthMe: Auth check failed with status:', response.status);
            return null;
        }
        
        const text = await response.text();
        console.log('fetchAuthMe: Response text:', text);
        return text;
    } catch (error) {
        console.error('fetchAuthMe: Error fetching auth info:', error);
        return null;
    }
}

window.fetchAuthMe = fetchAuthMe;

// Enhanced debug function for detailed auth info
async function fetchAuthMeDetailed() {
    try {
        const startTime = performance.now();
        
        const response = await fetch('/.auth/me', {
            method: 'GET',
            credentials: 'include',
            headers: {
                'Accept': 'application/json',
                'Cache-Control': 'no-cache'
            }
        });
        
        const endTime = performance.now();
        const text = await response.text();
        
        return {
            success: true,
            status: response.status,
            statusText: response.statusText,
            headers: Object.fromEntries(response.headers.entries()),
            body: text,
            timing: endTime - startTime,
            cookies: document.cookie,
            url: response.url
        };
    } catch (error) {
        return {
            success: false,
            error: error.message,
            stack: error.stack,
            cookies: document.cookie
        };
    }
}

window.fetchAuthMeDetailed = fetchAuthMeDetailed;

document.addEventListener('DOMContentLoaded', async () => {
    // Add the toast container to the DOM if it doesn't exist
    if (!document.getElementById('toast-container')) {
        const toastContainer = document.createElement('div');
        toastContainer.id = 'toast-container';
        toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        document.body.appendChild(toastContainer);
    }

    // Load and expose IndexedDB module
    try {
        const indexedDbModule = await import('./indexedDb.js');
        window.indexedDbModule = indexedDbModule;
        console.log('IndexedDB module loaded successfully');
    } catch (error) {
        console.error('Failed to load IndexedDB module:', error);
    }
});
