using System;
using Cysharp.Threading.Tasks;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
#endif

namespace Game.Online
{
	/// <summary>
	/// The browser half of signing in (Plugins/WebGL/NescAuth.jslib). Only the page can send playnesc.com's
	/// session cookie, so fetching a game token and opening the login popup go through JavaScript; the API calls
	/// themselves are plain web requests. Outside a WebGL build there is no site session, so nothing here is available.
	/// </summary>
	internal static class BrowserAuth
	{
#if UNITY_WEBGL && !UNITY_EDITOR
		private static Action _loginFinished;

		private static readonly Dictionary<int, UniTaskCompletionSource<(int, string)>> Requests =
			new Dictionary<int, UniTaskCompletionSource<(int, string)>>();

		private static int _nextRequestId;

		private delegate void TokenCallback(int requestId, int status, string body);

		private delegate void LoginCallback();

		[DllImport("__Internal")]
		private static extern void NescAuth_FetchGameToken(string url, int requestId, TokenCallback callback);

		[DllImport("__Internal")]
		private static extern int NescAuth_OpenLogin(string url, LoginCallback callback);

		public static bool IsAvailable => true;
#else
		public static bool IsAvailable => false;
#endif

		/// <summary>GETs <paramref name="url"/> with the site's cookies. Status 0 means the request failed.</summary>
		public static UniTask<(int Status, string Body)> FetchGameTokenAsync(string url)
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			int requestId = ++_nextRequestId;
			var completion = new UniTaskCompletionSource<(int, string)>();
			Requests.Add(requestId, completion);
			NescAuth_FetchGameToken(url, requestId, OnToken);
			return completion.Task;
#else
			return UniTask.FromResult((0, string.Empty));
#endif
		}

		/// <summary>
		/// Opens the login popup; <paramref name="onFinished"/> runs when it closes or the game window gets focus back.
		/// False if the browser blocked it (or this isn't a browser).
		/// </summary>
		public static bool OpenLogin(string url, Action onFinished)
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			_loginFinished = onFinished;
			return NescAuth_OpenLogin(url, OnLoginFinished) != 0;
#else
			return false;
#endif
		}

#if UNITY_WEBGL && !UNITY_EDITOR
		[MonoPInvokeCallback(typeof(TokenCallback))]
		private static void OnToken(int requestId, int status, string body)
		{
			if (Requests.Remove(requestId, out UniTaskCompletionSource<(int, string)> completion))
			{
				completion.TrySetResult((status, body));
			}
		}

		[MonoPInvokeCallback(typeof(LoginCallback))]
		private static void OnLoginFinished()
		{
			_loginFinished?.Invoke();
		}
#endif
	}
}
