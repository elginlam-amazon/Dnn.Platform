// Named JS interop helpers for the DotNetNuke.WebControls Blazor components.
//
// These replace the `IJSRuntime.InvokeVoidAsync("eval", ...)` calls the first pass used when
// porting the legacy controls. Every entry point a component needs is a named export, so the
// host can serve this module under a Content-Security-Policy that forbids `eval`/inline script.

/**
 * Opens a URL in a named browsing context.
 *
 * Replaces the legacy `window.open('<url>', '<target>')` script string, so neither the URL nor
 * the target is ever concatenated into executable source.
 *
 * @param {string} url The URL to open.
 * @param {string} target The name of the target browsing context.
 */
export function openWindow(url, target) {
    if (!url) {
        return;
    }

    window.open(url, target || "_blank");
}

/**
 * Runs a client script that the control's own configuration supplied.
 *
 * The legacy controls emitted the `JSFunction` / start-up statement text straight into a
 * `<script>` block, so executing the source is required for behavioural parity. Keeping it in
 * this single audited helper means there is exactly one place to lock down, and `new Function`
 * is used instead of `eval` so the script cannot capture or mutate local scope.
 *
 * @param {string} script The script source configured on the control.
 */
export function runControlScript(script) {
    if (!script) {
        return;
    }

    // eslint-disable-next-line no-new-func
    const run = new Function(script);
    run.call(window);
}
