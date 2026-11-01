using UnityEngine.SceneManagement;

namespace Game.Core
{
	public sealed class SceneLoader : ISceneLoader
	{
		public void ReloadActiveScene()
		{
			Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
			// Works for scenes that aren't in the build settings yet.
			UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
				new LoadSceneParameters(LoadSceneMode.Single));
#else
			SceneManager.LoadScene(scene.buildIndex);
#endif
		}
	}
}
