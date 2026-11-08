using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene entry point for PlayMode tests. RPGBattle is not in Build Settings, so the
/// editor assembly installs <see cref="LoadInPlayMode"/> before the runner starts.
/// A player build falls back to SceneManager, which can load the scene only after
/// it is added to Build Settings.
/// </summary>
public static class PlayModeSceneLoader
{
    public static Func<string, LoadSceneParameters, AsyncOperation> LoadInPlayMode;

    public static AsyncOperation Load(string scenePath, LoadSceneParameters parameters)
    {
        if (LoadInPlayMode != null)
            return LoadInPlayMode(scenePath, parameters);

        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
        return SceneManager.LoadSceneAsync(sceneName, parameters);
    }
}
