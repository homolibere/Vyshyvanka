// Code editor interop using CodeMirror 5
window.codeEditorInterop = {
    _editors: new Map(),

    /**
     * Initialize a CodeMirror editor instance on the given element.
     * @param {HTMLElement} container - The container element for the editor.
     * @param {string} editorId - Unique identifier for this editor instance.
     * @param {string} initialValue - Initial code content.
     * @param {object} dotNetRef - .NET object reference for callbacks.
     * @param {string} language - Programming language ('csharp' or 'javascript').
     * @returns {boolean} Whether initialization succeeded.
     */
    initialize: function (container, editorId, initialValue, dotNetRef, language) {
        if (!container || !window.CodeMirror) return false;

        // Detect current theme
        var isDark = document.documentElement.getAttribute('data-theme') === 'dark';
        var mode = this._getMode(language);

        var editor = CodeMirror(container, {
            value: initialValue || '',
            mode: mode,
            theme: isDark ? 'material-darker' : 'default',
            lineNumbers: true,
            matchBrackets: true,
            autoCloseBrackets: true,
            indentUnit: 4,
            tabSize: 4,
            indentWithTabs: false,
            lineWrapping: true,
            viewportMargin: Infinity,
            extraKeys: {
                'Tab': function (cm) {
                    if (cm.somethingSelected()) {
                        cm.indentSelection('add');
                    } else {
                        cm.replaceSelection('    ', 'end');
                    }
                },
                'Shift-Tab': function (cm) {
                    cm.indentSelection('subtract');
                }
            }
        });

        // Notify .NET on content change (debounced)
        var debounceTimer = null;
        editor.on('change', function () {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(function () {
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnCodeChanged', editor.getValue());
                }
            }, 300);
        });

        // Store reference
        this._editors.set(editorId, { editor: editor, dotNetRef: dotNetRef });

        return true;
    },

    /**
     * Update the editor content without triggering the change callback.
     */
    setValue: function (editorId, value) {
        var entry = this._editors.get(editorId);
        if (entry) {
            var cursor = entry.editor.getCursor();
            entry.editor.setValue(value || '');
            entry.editor.setCursor(cursor);
        }
    },

    /**
     * Get the current editor content.
     */
    getValue: function (editorId) {
        var entry = this._editors.get(editorId);
        return entry ? entry.editor.getValue() : '';
    },

    /**
     * Update the editor theme based on the current app theme.
     */
    updateTheme: function (editorId) {
        var entry = this._editors.get(editorId);
        if (entry) {
            var isDark = document.documentElement.getAttribute('data-theme') === 'dark';
            entry.editor.setOption('theme', isDark ? 'material-darker' : 'default');
        }
    },

    /**
     * Refresh the editor layout (call after visibility changes).
     */
    refresh: function (editorId) {
        var entry = this._editors.get(editorId);
        if (entry) {
            entry.editor.refresh();
        }
    },

    /**
     * Change the editor's syntax highlighting language.
     */
    setLanguage: function (editorId, language) {
        var entry = this._editors.get(editorId);
        if (entry) {
            entry.editor.setOption('mode', this._getMode(language));
        }
    },

    /**
     * Map language identifier to CodeMirror mode.
     */
    _getMode: function (language) {
        switch ((language || '').toLowerCase()) {
            case 'javascript':
            case 'js':
                return 'text/javascript';
            case 'csharp':
            case 'c#':
            default:
                return 'text/x-csharp';
        }
    },

    /**
     * Auto-format (re-indent) all lines in the editor using the mode's indentation rules.
     */
    autoFormat: function (editorId) {
        var entry = this._editors.get(editorId);
        if (!entry) return;

        var editor = entry.editor;
        var lineCount = editor.lineCount();

        // Select all and auto-indent each line
        editor.operation(function () {
            for (var i = 0; i < lineCount; i++) {
                editor.indentLine(i, 'smart');
            }
        });
    },

    /**
     * Dispose of the editor instance and clean up resources.
     */
    dispose: function (editorId) {
        var entry = this._editors.get(editorId);
        if (entry) {
            var wrapper = entry.editor.getWrapperElement();
            if (wrapper && wrapper.parentNode) {
                wrapper.parentNode.removeChild(wrapper);
            }
            this._editors.delete(editorId);
        }
    }
};
