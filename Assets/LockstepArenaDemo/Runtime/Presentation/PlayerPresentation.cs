#nullable enable

using System.Collections.Generic;
using LockstepArena.Simulation;
using UnityEngine;

namespace LockstepArena.Demo
{
    public sealed class PlayerPresentation : MonoBehaviour
    {
        private readonly List<Renderer> _robotRenderers = new List<Renderer>();
        private Animation? _animation;
        private BattlePresentationCatalog? _catalog;
        private Transform? _visualRoot;
        private Transform? _modelRoot;
        private Transform? _muzzle;
        private Vector3 _modelLocalPosition;
        private Quaternion _modelLocalRotation;
        private Vector3 _modelLocalScale;
        private Vector3 _lastPosition;
        private bool _hasPosition;
        private bool _dead;
        private float _flashUntil;

        public PlayerSlot Slot { get; private set; }
        public Transform Muzzle => _muzzle ?? transform;

        public void Configure(PlayerSlot slot, BattlePresentationCatalog catalog)
        {
            Slot = slot;
            _catalog = catalog;
            gameObject.name = $"P{slot.Value + 1} Robot View";

            var visual = new GameObject("Robot Visual");
            visual.transform.SetParent(transform, false);
            _visualRoot = visual.transform;
            if (catalog.RobotPrefab is not null)
            {
                GameObject model = Instantiate(catalog.RobotPrefab, visual.transform, false);
                model.name = "Styloo Robot";
                RemovePhysics(model);
                NormalizeHeight(model.transform, 1.38f);
                _modelRoot = model.transform;
                _modelLocalPosition = model.transform.localPosition;
                _modelLocalRotation = model.transform.localRotation;
                _modelLocalScale = model.transform.localScale;
                _animation = model.GetComponentInChildren<Animation>();
                if (_animation is null) _animation = model.AddComponent<Animation>();
                AddClip(catalog.IdleClip, "Idle");
                AddClip(catalog.RunClip, "Run");
                AddClip(catalog.ShootClip, "Shoot");
                if (_animation.GetClip("Idle") is not null)
                {
                    _animation.clip = _animation.GetClip("Idle");
                    _animation.Play("Idle");
                }
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if (catalog.RobotMaterial is not null) renderer.sharedMaterial = catalog.RobotMaterial;
                    _robotRenderers.Add(renderer);
                }
                AttachWeapon(model.transform, catalog);
            }

            CreateTeamMarker(slot, catalog);
        }

        public void Present(PlayerState state)
        {
            Vector3 position = BattlePresenter.ToWorld(state.PositionX, 0f, state.PositionZ);
            bool moving = _hasPosition && (position - _lastPosition).sqrMagnitude > 0.000001f;
            transform.position = position;
            BattleSimulation.GetAimDirection(state.Aim, out int directionX, out int directionZ);
            transform.rotation = Quaternion.LookRotation(new Vector3(directionX, 0f, directionZ));

            bool isDead = state.HitPoints <= 0;
            if (_visualRoot is not null)
            {
                _visualRoot.localRotation = isDead ? Quaternion.Euler(0f, 0f, Slot.Value == 0 ? 72f : -72f) : Quaternion.identity;
                _visualRoot.localPosition = isDead ? new Vector3(0f, 0.22f, 0f) : Vector3.zero;
            }

            if (isDead)
            {
                if (!_dead) _animation?.Stop();
            }
            else if (_animation is not null && !_animation.IsPlaying("Shoot"))
            {
                string clip = moving ? "Run" : "Idle";
                if (_animation.GetClip(clip) is not null && !_animation.IsPlaying(clip)) _animation.CrossFade(clip, 0.08f);
            }

            _dead = isDead;
            _lastPosition = position;
            _hasPosition = true;
        }

        public void TriggerShoot()
        {
            if (_dead || _animation is null || _animation.GetClip("Shoot") is null) return;
            _animation.CrossFade("Shoot", 0.03f);
        }

        public void TriggerHitFlash()
        {
            _flashUntil = Time.unscaledTime + 0.1f;
        }

        private void Update()
        {
            if (_robotRenderers.Count == 0) return;
            bool flash = Time.unscaledTime < _flashUntil;
            var properties = new MaterialPropertyBlock();
            if (flash) properties.SetColor("_BaseColor", Color.white);
            foreach (Renderer renderer in _robotRenderers) renderer.SetPropertyBlock(properties);
        }

        private void LateUpdate()
        {
            if (_modelRoot is null) return;
            _modelRoot.localPosition = _modelLocalPosition;
            _modelRoot.localRotation = _modelLocalRotation;
            _modelRoot.localScale = _modelLocalScale;
        }

        private void AttachWeapon(Transform model, BattlePresentationCatalog catalog)
        {
            if (catalog.WeaponPrefab is null) return;
            Transform parent = FindBone(model, "handdown") ?? FindBone(model, "minigun") ?? model;
            GameObject weapon = Instantiate(catalog.WeaponPrefab, parent, false);
            weapon.name = "View Blaster";
            RemovePhysics(weapon);
            NormalizeLength(weapon.transform, 0.48f);
            weapon.transform.localPosition = new Vector3(0.03f, 0f, 0.08f);
            weapon.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>())
                if (catalog.WeaponMaterial is not null) renderer.sharedMaterial = catalog.WeaponMaterial;
            var muzzle = new GameObject("Visual Muzzle");
            muzzle.transform.SetParent(weapon.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.52f);
            _muzzle = muzzle.transform;
        }

        private void CreateTeamMarker(PlayerSlot slot, BattlePresentationCatalog catalog)
        {
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Team Core";
            core.transform.SetParent(transform, false);
            core.transform.localPosition = new Vector3(0f, 0.7f, 0.28f);
            core.transform.localScale = Vector3.one * 0.16f;
            DestroyView(core.GetComponent<Collider>());
            core.GetComponent<Renderer>().sharedMaterial = slot.Value == 0 ? catalog.CyanMaterial : catalog.RedMaterial;

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Team Ground Ring";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            ring.transform.localScale = new Vector3(0.38f, 0.008f, 0.38f);
            DestroyView(ring.GetComponent<Collider>());
            ring.GetComponent<Renderer>().sharedMaterial = slot.Value == 0 ? catalog.CyanMaterial : catalog.RedMaterial;
        }

        private void AddClip(AnimationClip? clip, string name)
        {
            if (_animation is null || clip is null) return;
            _animation.AddClip(clip, name);
            if (name == "Idle" || name == "Run") clip.wrapMode = WrapMode.Loop;
        }

        private static void RemovePhysics(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) DestroyView(collider);
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true)) DestroyView(body);
        }

        private static Transform? FindBone(Transform root, string fragment)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.ToLowerInvariant().Contains(fragment)) return child;
            return null;
        }

        private static void NormalizeHeight(Transform root, float targetHeight)
        {
            Bounds bounds = CalculateBounds(root);
            if (bounds.size.y > 0.001f) root.localScale *= targetHeight / bounds.size.y;
            bounds = CalculateBounds(root);
            root.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static void NormalizeLength(Transform root, float targetLength)
        {
            Bounds bounds = CalculateBounds(root);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (size > 0.001f) root.localScale *= targetLength / size;
        }

        private static Bounds CalculateBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void DestroyView(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
