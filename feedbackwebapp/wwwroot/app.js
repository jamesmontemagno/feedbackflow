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