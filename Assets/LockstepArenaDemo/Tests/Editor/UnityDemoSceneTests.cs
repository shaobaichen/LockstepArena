using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class UnityDemoSceneTests
    {
        [Test]
        public void DemoSceneContainsSingleDebugController()
        {
            const string path = "Assets/LockstepArenaDemo/Scenes/LockstepArenaDemo.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            MonoBehaviour[] controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component.GetType().FullName == "LockstepArena.Demo.LockstepArenaDemoController")
                .ToArray();
            Assert.That(controllers, Has.Length.EqualTo(1));
        }
    }
}
