// Signing in through playnesc.com (see Game.Online.BrowserAuth). The site's session cookie is httpOnly, so only a
// credentialed fetch from the page can use it, and the login itself happens on the site, in a popup: navigating this
// tab away would reload the game.
mergeInto(LibraryManager.library, {
	// GET url with the site's cookies; calls back (requestId, status, body). Status 0 means the request failed.
	NescAuth_FetchGameToken: function (urlPtr, requestId, callback) {
		var url = UTF8ToString(urlPtr);
		fetch(url, { credentials: "include", cache: "no-store" })
			.then(function (response) {
				return response.text().then(function (text) {
					return { status: response.status, text: text };
				});
			})
			.catch(function () {
				return { status: 0, text: "" };
			})
			.then(function (result) {
				var size = lengthBytesUTF8(result.text) + 1;
				var body = _malloc(size);
				stringToUTF8(result.text, body, size);
				{{{ makeDynCall("viii", "callback") }}}(requestId, result.status, body);
				_free(body);
			});
	},

	// Opens the login popup; returns 0 if the browser blocked it. Calls back once when the popup closes (it closes
	// itself after signing in) or the game window gets focus back, whichever comes first.
	NescAuth_OpenLogin: function (urlPtr, callback) {
		var url = UTF8ToString(urlPtr);
		var popup = window.open(url, "nesc-login", "popup,width=480,height=720");
		if (!popup) return 0;

		var done = false;
		var timer = 0;
		function finish() {
			if (done) return;
			done = true;
			clearInterval(timer);
			window.removeEventListener("focus", onFocus);
			{{{ makeDynCall("v", "callback") }}}();
		}
		function onFocus() {
			finish();
		}
		timer = setInterval(function () {
			if (popup.closed) finish();
		}, 500);
		// Opening the popup takes focus away; only a later return counts.
		setTimeout(function () {
			if (!done) window.addEventListener("focus", onFocus);
		}, 1000);
		return 1;
	},
});
