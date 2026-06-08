/**
 * Theme interop for Blazor — applies CSS custom properties from theme JSON
 * and manages custom theme storage in localStorage.
 */

const STORAGE_KEY_ACTIVE = 'vyshyvanka-active-theme';
const STORAGE_KEY_CUSTOM = 'vyshyvanka-custom-themes';

/**
 * Apply a dictionary of CSS custom properties to the document root.
 * @param {Object<string, string>} colors
 * @param {string} baseMode - "light" or "dark"
 */
window.vyshyvankaTheme = {

    applyColors: function (colors, baseMode) {
        const root = document.documentElement;
        root.setAttribute('data-theme', baseMode);
        root.style.setProperty('color-scheme', baseMode);
        for (const [key, value] of Object.entries(colors)) {
            root.style.setProperty('--' + key, value);
        }
    },

    clearInlineColors: function () {
        const root = document.documentElement;
        const style = root.style;
        const toRemove = [];
        for (let i = 0; i < style.length; i++) {
            const prop = style[i];
            if (prop.startsWith('--')) {
                toRemove.push(prop);
            }
        }
        toRemove.forEach(p => style.removeProperty(p));
    },

    /**
     * Fetch a JSON file relative to the app origin using the browser's native fetch.
     * Returns the raw JSON string, or null if the fetch fails.
     * @param {string} path - Relative path (e.g. "themes/vyshyvanka-light.json")
     * @returns {Promise<string|null>}
     */
    fetchThemeJson: async function (path) {
        try {
            const response = await fetch(path);
            if (!response.ok) return null;
            const data = await response.json();
            return JSON.stringify(data);
        } catch {
            return null;
        }
    },

    getActiveThemeId: function () {
        return localStorage.getItem(STORAGE_KEY_ACTIVE);
    },

    setActiveThemeId: function (themeId) {
        localStorage.setItem(STORAGE_KEY_ACTIVE, themeId);
    },

    getCustomThemes: function () {
        const raw = localStorage.getItem(STORAGE_KEY_CUSTOM);
        if (!raw) return [];
        try { return JSON.parse(raw); }
        catch { return []; }
    },

    saveCustomTheme: function (themeJson) {
        const themes = this.getCustomThemes();
        const parsed = JSON.parse(themeJson);
        const idx = themes.findIndex(t => t.id === parsed.id);
        if (idx >= 0) {
            themes[idx] = parsed;
        } else {
            themes.push(parsed);
        }
        localStorage.setItem(STORAGE_KEY_CUSTOM, JSON.stringify(themes));
    },

    removeCustomTheme: function (themeId) {
        const themes = this.getCustomThemes();
        const filtered = themes.filter(t => t.id !== themeId);
        localStorage.setItem(STORAGE_KEY_CUSTOM, JSON.stringify(filtered));
    }
};
