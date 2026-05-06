// CodeMirror 5 mode for JSONata expression language
// Based on JSONata syntax: https://docs.jsonata.org/
(function (mod) {
    if (typeof exports === "object" && typeof module === "object")
        mod(require("codemirror"));
    else if (typeof define === "function" && define.amd)
        define(["codemirror"], mod);
    else
        mod(CodeMirror);
})(function (CodeMirror) {
    "use strict";

    CodeMirror.defineMode("jsonata", function () {
        // Built-in function names (prefixed with $ in usage)
        var builtins = new Set([
            "sum", "count", "max", "min", "average",
            "string", "length", "substring", "substringBefore", "substringAfter",
            "uppercase", "lowercase", "trim", "pad", "contains", "split", "join",
            "match", "replace", "eval", "base64encode", "base64decode",
            "encodeUrlComponent", "encodeUrl", "decodeUrlComponent", "decodeUrl",
            "number", "abs", "floor", "ceil", "round", "power", "sqrt",
            "random", "formatNumber", "formatBase", "formatInteger", "parseInteger",
            "boolean", "not", "exists", "type",
            "append", "sort", "reverse", "shuffle", "distinct", "zip",
            "keys", "values", "spread", "merge", "each", "error",
            "assert", "single", "filter", "reduce", "sift",
            "map", "lookup",
            "now", "millis", "toMillis", "fromMillis",
            "clone", "object", "array"
        ]);

        var keywords = new Set(["true", "false", "null", "function", "lambda", "in"]);

        function tokenBase(stream, state) {
            var ch = stream.peek();

            // Block comments /* ... */
            if (ch === "/" && stream.match("/*")) {
                state.tokenize = tokenComment;
                return tokenComment(stream, state);
            }

            // Strings (double-quoted)
            if (ch === '"') {
                stream.next();
                state.tokenize = tokenString('"');
                return state.tokenize(stream, state);
            }

            // Strings (single-quoted)
            if (ch === "'") {
                stream.next();
                state.tokenize = tokenString("'");
                return state.tokenize(stream, state);
            }

            // Backtick-quoted field names
            if (ch === "`") {
                stream.next();
                state.tokenize = tokenString("`");
                return state.tokenize(stream, state);
            }

            // Numbers
            if (/\d/.test(ch) || (ch === "." && stream.match(/^\.\d/, false))) {
                stream.match(/^\d*\.?\d+([eE][+-]?\d+)?/);
                return "number";
            }

            // Variables and built-in functions ($name)
            if (ch === "$") {
                stream.next();
                if (stream.match(/^[a-zA-Z_][a-zA-Z0-9_]*/)) {
                    var word = stream.current().substring(1);
                    if (builtins.has(word)) {
                        return "builtin";
                    }
                    return "variable-2";
                }
                // Standalone $ (context reference)
                return "variable-2";
            }

            // Operators
            if (stream.match("~>") || stream.match(":=") || stream.match("!=") ||
                stream.match("<=") || stream.match(">=") || stream.match("..")) {
                return "operator";
            }

            if (/[+\-*\/%&=<>!?:^~]/.test(ch)) {
                stream.next();
                return "operator";
            }

            // Brackets and punctuation
            if (/[[\]{}(),;.]/.test(ch)) {
                stream.next();
                return "bracket";
            }

            // Wildcards
            if (ch === "*" && stream.match("**")) {
                return "keyword";
            }

            // Identifiers and keywords
            if (/[a-zA-Z_]/.test(ch)) {
                stream.match(/^[a-zA-Z_][a-zA-Z0-9_]*/);
                var word = stream.current();
                if (keywords.has(word)) {
                    return "keyword";
                }
                if (word === "and" || word === "or") {
                    return "operator";
                }
                return "property";
            }

            // Skip unknown characters
            stream.next();
            return null;
        }

        function tokenComment(stream, state) {
            var maybeEnd = false;
            var ch;
            while ((ch = stream.next()) != null) {
                if (ch === "/" && maybeEnd) {
                    state.tokenize = tokenBase;
                    break;
                }
                maybeEnd = (ch === "*");
            }
            return "comment";
        }

        function tokenString(quote) {
            return function (stream, state) {
                var escaped = false, ch;
                while ((ch = stream.next()) != null) {
                    if (ch === quote && !escaped) {
                        state.tokenize = tokenBase;
                        break;
                    }
                    escaped = !escaped && ch === "\\";
                }
                return quote === "`" ? "property" : "string";
            };
        }

        return {
            startState: function () {
                return { tokenize: tokenBase };
            },
            token: function (stream, state) {
                if (stream.eatSpace()) return null;
                return state.tokenize(stream, state);
            },
            lineComment: null,
            blockCommentStart: "/*",
            blockCommentEnd: "*/"
        };
    });

    CodeMirror.defineMIME("text/x-jsonata", "jsonata");
});
