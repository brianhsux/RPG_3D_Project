using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs the in-editor scene load before the PlayMode runner starts.
/// The test assembly itself stays free of UnityEditor references.
/// </summary>
[InitializeOnLoad]
static class PlayModeSceneLoaderSetup
{
    static PlayModeSceneLoaderSetup()
    {
        Install();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Install()
    {
        PlayModeSceneLoader.LoadInPlayMode = (path, parameters) =>
            EditorSceneManager.LoadSceneAsyncInPlayMode(path, parameters);
    }
}
