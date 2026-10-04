using NUnit.Framework;
using UnityEngine;
using PetShop.Core;
using PetShop.Shop;

namespace PetShop.Tests
{
    /// <summary>
    /// EditMode tests for the build view's input gate: it raises <see cref="GameManager.IsBuildViewActive"/>
    /// rather than the modal flag, so the day clock and the save/end-day keys keep running.
    /// </summary>
    public class BuildCameraTests
    {
        private const string MainCameraTag = "MainCamera";

        private GameObject  _cameraObject;
        private GameObject  _gameObject;
        private GameManager _game;
        private BuildCamera _view;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("main-camera", typeof(Camera)) { tag = MainCameraTag };
            _gameObject   = new GameObject("build-camera-test");
            _game = _gameObject.AddComponent<GameManager>();
            _view = _gameObject.AddComponent<BuildCamera>();
            _view.Init(_game, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void Enter_SetsBuildViewActive_AndLeavesModalClosed()
        {
            Assert.IsNotNull(Camera.main, "the test camera is the main camera");

            _view.Enter();

            Assert.IsTrue(_view.IsActive);
            Assert.IsTrue(_game.IsBuildViewActive);
            Assert.IsFalse(_game.IsModalOpen, "the build view must not freeze the day");
        }

        [Test]
        public void Exit_ClearsBuildViewActive()
        {
            _view.Enter();

            _view.Exit();

            Assert.IsFalse(_view.IsActive);
            Assert.IsFalse(_game.IsBuildViewActive);
            Assert.IsFalse(_game.IsModalOpen);
        }
    }
}
