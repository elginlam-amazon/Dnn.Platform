// ASP.NET AJAX page methods ([ScriptService]/[WebMethod], called as "<page>.aspx/<method>") do not
// exist on ASP.NET Core, so the methods this file called are now real endpoints which live next to
// the page they belong to (see Browser/SetFileEndpoint.cs). The jQuery.PageMethod signature is kept
// so that the calling code the browser emits does not have to change: the method name becomes the
// last segment of the endpoint URL, resolved relative to the folder of the calling page, and the
// named argument pairs are posted as the same JSON body as before.
jQuery.PageMethod = function (page, method, success, error) {
    var args = "", argCount = arguments.length, i, folder;

    if (argCount > 4) {
        for (i = 4; i < argCount - 1; i += 2) {
            if (args.length != 0) {
                args += ",";
            }

            args += '"' + arguments[i] + '":"' + arguments[i + 1] + '"';
        }
    }

    args = "{" + args + "}";

    // The endpoint sits in the same folder as the page that calls it, so the extension keeps working
    // whatever virtual path the DNN website is installed under.
    folder = window.location.pathname.substring(0, window.location.pathname.lastIndexOf("/") + 1);

    return jQuery.PageMethodToPage(folder + method, success, error, args);
}, jQuery.PageMethodToPage = function (url, success, error, args) {
    jQuery.ajax({
        type: "POST",
        url: url,
        contentType: "application/json; charset=utf-8",
        data: args,
        dataType: "json",
        success: success,
        error: error
    })
}
