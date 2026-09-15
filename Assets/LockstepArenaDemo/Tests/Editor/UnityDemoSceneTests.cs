using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class UnityDemoSceneTests
    {
        [Test]
        public void LobbyAndBattleScenesAreBuildEnabledAndUseOnePersistentRoot()
        {
            const string lobbyPath = "Assets/LockstepArenaDemo/Scenes/LobbyScene.unity";
            const string battlePath = "Assets/LockstepArenaDemo/Scenes/BattleScene.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(lobbyPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(battlePath), Is.Not.Null);
            string[] enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            Assert.That(enabledScenes, Is.EqualTo(new[] { lobbyPath, battlePath }));

            var scene = EditorSceneManager.OpenScene(lobbyPath, OpenSceneMode.Single);
            MonoBehaviour[] controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component.GetType().FullName == "LockstepArena.Demo.LockstepArenaDemoController")
                .ToArray();
            Assert.That(controllers, Has.Length.EqualTo(1));
        }
    }
}
