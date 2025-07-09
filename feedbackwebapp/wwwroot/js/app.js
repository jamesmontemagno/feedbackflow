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
        console.log('fetchAuthMe: Starting token refresh and auth check');
        
        // First, attempt to refresh tokens to ensure we have valid access tokens
        try {
            const refreshResponse = await fetch('/.auth/refresh', {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Cache-Control': 'no-cache'
                }
            });
            console.log('fetchAuthMe: Token refresh response status:', refreshResponse.status);
        } catch (refreshError) {
            console.warn('fetchAuthMe: Token refresh failed, continuing with auth check:', refreshError);
        }
        
        // Now fetch the auth information
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
        
        // First, attempt to refresh tokens
        let refreshInfo = null;
        try {
            const refreshStartTime = performance.now();
            const refreshResponse = await fetch('/.auth/refresh', {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Cache-Control': 'no-cache'
                }
            });
            const refreshEndTime = performance.now();
            
            refreshInfo = {
                status: refreshResponse.status,
                statusText: refreshResponse.statusText,
                timing: refreshEndTime - refreshStartTime,
                headers: Object.fromEntries(refreshResponse.headers.entries())
            };
        } catch (refreshError) {
            refreshInfo = {
                error: refreshError.message,
                stack: refreshError.stack
            };
        }
        
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
            url: response.url,
            refreshInfo: refreshInfo
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

// Authentication token refresh management
class AuthTokenManager {
    constructor() {
        this.refreshIntervalId = null;
        this.isRefreshing = false;
        this.lastRefreshTime = null;
        this.refreshInterval = 15 * 60 * 1000; // 15 minutes (configurable)
        this.minRefreshInterval = 5 * 60 * 1000; // 5 minutes (configurable)
        this.dotNetHelper = null; // Reference to .NET object for callbacks
    }

    // Start automatic token refresh
    startAutoRefresh() {
        console.log('AuthTokenManager: Starting automatic token refresh');
        this.stopAutoRefresh(); // Clear any existing interval
        
        // Initial refresh check
        this.refreshTokenIfNeeded();
        
        // Set up periodic refresh
        this.refreshIntervalId = setInterval(() => {
            this.refreshTokenIfNeeded();
        }, this.refreshInterval);
    }

    // Stop automatic token refresh
    stopAutoRefresh() {
        if (this.refreshIntervalId) {
            console.log('AuthTokenManager: Stopping automatic token refresh');
            clearInterval(this.refreshIntervalId);
            this.refreshIntervalId = null;
        }
    }

    // Set .NET helper for callbacks
    setDotNetHelper(dotNetHelper) {
        this.dotNetHelper = dotNetHelper;
        console.log('AuthTokenManager: .NET helper set for callbacks');
    }

    // Configure refresh interval
    setRefreshInterval(intervalMs) {
        this.refreshInterval = intervalMs;
        console.log('AuthTokenManager: Refresh interval set to', intervalMs, 'ms');
        
        // Restart auto refresh if it's already running
        if (this.refreshIntervalId) {
            this.stopAutoRefresh();
            this.startAutoRefresh();
        }
    }

    // Configure minimum refresh interval
    setMinRefreshInterval(intervalMs) {
        this.minRefreshInterval = intervalMs;
        console.log('AuthTokenManager: Min refresh interval set to', intervalMs, 'ms');
    }

    // Check if token refresh is needed and perform it
    async refreshTokenIfNeeded() {
        if (this.isRefreshing) {
            console.log('AuthTokenManager: Refresh already in progress, skipping');
            return;
        }

        const now = Date.now();
        
        // Don't refresh too frequently (use configured minimum interval)
        if (this.lastRefreshTime && (now - this.lastRefreshTime) < this.minRefreshInterval) {
            console.log('AuthTokenManager: Too soon for another refresh, skipping. Min interval:', this.minRefreshInterval, 'ms');
            return;
        }

        try {
            console.log('AuthTokenManager: Attempting token refresh');
            this.isRefreshing = true;
            
            const refreshResult = await this.performTokenRefresh();
            
            if (refreshResult.success) {
                console.log('AuthTokenManager: Token refresh successful');
                this.lastRefreshTime = now;
                
                // Notify .NET about successful refresh
                if (this.dotNetHelper) {
                    try {
                        await this.dotNetHelper.invokeMethodAsync('OnAuthStateChangedFromJS', true);
                    } catch (callbackError) {
                        console.warn('AuthTokenManager: Error calling .NET callback:', callbackError);
                    }
                }
            } else {
                console.warn('AuthTokenManager: Token refresh failed', refreshResult);
                
                // If refresh failed, try to check current auth status
                const authStatus = await this.checkAuthStatus();
                if (!authStatus.isAuthenticated) {
                    console.warn('AuthTokenManager: User appears to be logged out');
                    
                    // Notify .NET about auth failure
                    if (this.dotNetHelper) {
                        try {
                            await this.dotNetHelper.invokeMethodAsync('OnAuthStateChangedFromJS', false);
                        } catch (callbackError) {
                            console.warn('AuthTokenManager: Error calling .NET callback:', callbackError);
                        }
                    }
                }
            }
        } catch (error) {
            console.error('AuthTokenManager: Error during token refresh:', error);
        } finally {
            this.isRefreshing = false;
        }
    }

    // Perform the actual token refresh
    async performTokenRefresh() {
        try {
            const response = await fetch('/.auth/refresh', {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Cache-Control': 'no-cache'
                }
            });

            return {
                success: response.ok,
                status: response.status,
                statusText: response.statusText
            };
        } catch (error) {
            return {
                success: false,
                error: error.message
            };
        }
    }

    // Check current authentication status
    async checkAuthStatus() {
        try {
            const response = await fetch('/.auth/me', {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Accept': 'application/json',
                    'Cache-Control': 'no-cache'
                }
            });

            if (!response.ok) {
                return { isAuthenticated: false, status: response.status };
            }

            const text = await response.text();
            const isAuthenticated = text && text.trim() !== '[]' && text.trim() !== '';
            
            return { isAuthenticated, response: text };
        } catch (error) {
            console.error('AuthTokenManager: Error checking auth status:', error);
            return { isAuthenticated: false, error: error.message };
        }
    }

    // Manual refresh trigger (can be called from components)
    async manualRefresh() {
        console.log('AuthTokenManager: Manual refresh requested');
        this.lastRefreshTime = null; // Reset to allow immediate refresh
        await this.refreshTokenIfNeeded();
    }
}

// Create global auth token manager
const authTokenManager = new AuthTokenManager();
window.authTokenManager = authTokenManager;

// Expose enhanced fetch auth functions
window.fetchAuthMeWithRefresh = fetchAuthMeWithRefresh;

// Helper function to ensure authentication before API calls
async function ensureAuthenticated() {
    try {
        console.log('ensureAuthenticated: Checking and refreshing authentication');
        
        // Force a manual refresh
        await authTokenManager.manualRefresh();
        
        // Check the current auth status
        const authStatus = await authTokenManager.checkAuthStatus();
        
        console.log('ensureAuthenticated: Authentication status:', authStatus.isAuthenticated);
        return authStatus.isAuthenticated;
    } catch (error) {
        console.error('ensureAuthenticated: Error ensuring authentication:', error);
        return false;
    }
}

window.ensureAuthenticated = ensureAuthenticated;

// Enhanced fetch auth me with automatic refresh
async function fetchAuthMeWithRefresh() {
    try {
        console.log('fetchAuthMeWithRefresh: Starting auth check with refresh');
        
        // First, attempt to refresh tokens
        try {
            const refreshResponse = await fetch('/.auth/refresh', {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Cache-Control': 'no-cache'
                }
            });
            console.log('fetchAuthMeWithRefresh: Token refresh response status:', refreshResponse.status);
        } catch (refreshError) {
            console.warn('fetchAuthMeWithRefresh: Token refresh failed, continuing with auth check:', refreshError);
        }
        
        // Now fetch the auth information
        const response = await fetch('/.auth/me', {
            method: 'GET',
            credentials: 'include',
            headers: {
                'Accept': 'application/json',
                'Cache-Control': 'no-cache'
            }
        });
        
        console.log('fetchAuthMeWithRefresh: Response status:', response.status);
        
        if (!response.ok) {
            console.warn('fetchAuthMeWithRefresh: Auth check failed with status:', response.status);
            return null;
        }
        
        const text = await response.text();
        console.log('fetchAuthMeWithRefresh: Response text:', text);
        return text;
    } catch (error) {
        console.error('fetchAuthMeWithRefresh: Error fetching auth info:', error);
        return null;
    }
}

window.fetchAuthMeWithRefresh = fetchAuthMeWithRefresh;

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

    // Start automatic token refresh after page load
    console.log('Starting automatic authentication token refresh');
    authTokenManager.startAutoRefresh();
});
