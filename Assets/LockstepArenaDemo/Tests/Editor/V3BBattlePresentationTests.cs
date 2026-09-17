#nullable enable

using System;
using System.Collections.Generic;
using LockstepArena.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class V3BBattlePresentationTests
    {
        [Test]
        public void TeamColorsAreCanonicalAndIndependentOfLocalSlot()
        {
            Assert.That(BattlePresentationReadModel.GetTeamColor(new PlayerSlot(0)),
                Is.EqualTo(new Color(0.08f, 0.72f, 1f, 1f)));
            Assert.That(BattlePresentationReadModel.GetTeamColor(new PlayerSlot(1)),
                Is.EqualTo(new Color(1f, 0.18f, 0.12f, 1f)));
        }

        [Test]
        public void HudReadModelUsesDeterministicHpRoundTimerAndWins()
        {
            BattleState state = CreateState(
                42, BattlePhase.Playing, 0, 1781, RoundResult.None, null,
                new PlayerState(-3000, 0, 0, 75, 1, 0),
                new PlayerState(3000, 0, 32768, 50, 0, 0));

            BattlePresentationSnapshot snapshot = BattlePresentationReadModel.Create(state);

            Assert.That(snapshot.Player1HitPoints, Is.EqualTo(75));
            Assert.That(snapshot.Player2HitPoints, Is.EqualTo(50));
            Assert.That(snapshot.Player1RoundWins, Is.EqualTo(1));
            Assert.That(snapshot.Player2RoundWins, Is.Zero);
            Assert.That(snapshot.RoundNumber, Is.EqualTo(2));
            Assert.That(snapshot.SecondsRemaining, Is.EqualTo(60));
            Assert.That(snapshot.Announcement, Is.EqualTo(BattleAnnouncementKind.Fight));
        }

        [TestCase(90U, BattleAnnouncementKind.Countdown3)]
        [TestCase(60U, BattleAnnouncementKind.Countdown2)]
        [TestCase(30U, BattleAnnouncementKind.Countdown1)]
        public void CountdownMapsToExactlyOneAnnouncement(uint ticks, BattleAnnouncementKind expected)
        {
            BattleState state = CreateState(1, BattlePhase.RoundCountdown, ticks, 1800,
                RoundResult.None, null, DefaultPlayer(0), DefaultPlayer(1));

            BattlePresentationSnapshot snapshot = BattlePresentationReadModel.Create(state);

            Assert.That(snapshot.Announcement, Is.EqualTo(expected));
            Assert.That(snapshot.AnnouncementText, Is.Not.Empty);
        }

        [Test]
        public void FinalVictorySupersedesRoundWin()
        {
            BattleState state = CreateState(99, BattlePhase.MatchEnded, 0, 1200,
                RoundResult.PlayerWin(new PlayerSlot(0)), new PlayerSlot(0),
                new PlayerState(-3000, 0, 0, 0, 2, 0), DefaultPlayer(1));

            BattlePresentationSnapshot snapshot = BattlePresentationReadModel.Create(state);

            Assert.That(snapshot.Announcement, Is.EqualTo(BattleAnnouncementKind.MatchVictory));
            Assert.That(snapshot.WinnerSlot, Is.EqualTo(0));
            Assert.That(snapshot.AnnouncementText, Is.EqualTo("MATCH\nVICTORY\nP1"));
        }

        [Test]
        public void DrawAndRoundWinAreMutuallyExclusive()
        {
            BattlePresentationSnapshot draw = BattlePresentationReadModel.Create(CreateState(
                80, BattlePhase.RoundEnded, 20, 0, RoundResult.Draw, null,
                DefaultPlayer(0), DefaultPlayer(1)));
            BattlePresentationSnapshot win = BattlePresentationReadModel.Create(CreateState(
                81, BattlePhase.RoundEnded, 20, 0, RoundResult.PlayerWin(new PlayerSlot(1)), null,
                DefaultPlayer(0), DefaultPlayer(1)));

            Assert.That(draw.Announcement, Is.EqualTo(BattleAnnouncementKind.Draw));
            Assert.That(draw.WinnerSlot, Is.EqualTo(-1));
            Assert.That(win.Announcement, Is.EqualTo(BattleAnnouncementKind.RoundWin));
            Assert.That(win.WinnerSlot, Is.EqualTo(1));
        }

        [Test]
        public void ResetPresentationDataReturnsPlayersToAliveCountdownState()
        {
            BattleState initial = BattleState.CreateGameplayInitial(CreateRoster(), BattleDefinition.CreateDefault());

            BattlePresentationSnapshot snapshot = BattlePresentationReadModel.Create(initial);

            Assert.That(snapshot.Player1HitPoints, Is.EqualTo(100));
            Assert.That(snapshot.Player2HitPoints, Is.EqualTo(100));
            Assert.That(snapshot.RoundNumber, Is.EqualTo(1));
            Assert.That(snapshot.Announcement, Is.EqualTo(BattleAnnouncementKind.Countdown3));
        }

        [Test]
        public void ProjectileLifecycleAndDamageAreObservedOnceAcrossRepeatedState()
        {
            var tracker = new BattlePresentationTracker();
            ProjectileState projectile = new ProjectileState(7, new PlayerId(11), -1000, 0, 1000, 0, 20);
            BattleState fired = CreateState(10, BattlePhase.Playing, 0, 1700, RoundResult.None, null,
                DefaultPlayer(0), DefaultPlayer(1), projectile);
            BattleState hit = CreateState(11, BattlePhase.Playing, 0, 1699, RoundResult.None, null,
                DefaultPlayer(0), new PlayerState(3000, 0, 32768, 75, 0, 0));

            BattlePresentationEvents first = tracker.Observe(fired);
            BattlePresentationEvents repeated = tracker.Observe(fired);
            BattlePresentationEvents impact = tracker.Observe(hit);
            BattlePresentationEvents rollbackRepeated = tracker.Observe(fired);
            BattlePresentationEvents replayedImpact = tracker.Observe(hit);

            Assert.That(first.SpawnedProjectileIds, Is.EqualTo(new ulong[] { 7 }));
            Assert.That(repeated.HasAny, Is.False);
            Assert.That(impact.RemovedProjectileIds, Is.EqualTo(new ulong[] { 7 }));
            Assert.That(impact.DamagedSlots, Is.EqualTo(new int[] { 1 }));
            Assert.That(rollbackRepeated.HasAny, Is.False);
            Assert.That(replayedImpact.HasAny, Is.False);
        }

        [Test]
        public void CatalogContainsTheApprovedMinimalAssetSet()
        {
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();

            Assert.That(AssetDatabase.GetAssetPath(catalog.RobotPrefab), Is.EqualTo(
                "Assets/Art/Characters/StylooRobot/robot.fbx"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.WeaponPrefab), Is.EqualTo(
                "Assets/Art/Weapons/SciFiGun/LongPistol_small.fbx"));
            Assert.That(catalog.IdleClip?.name, Does.Contain("iddle"));
            Assert.That(catalog.RunClip?.name, Does.Contain("walking"));
            Assert.That(catalog.ShootClip?.name, Does.Contain("attackminiguns"));
            Assert.That(catalog.ProjectileMaterial, Is.Not.Null);
            Assert.That(catalog.ShootSound, Is.Not.Null);
            Assert.That(catalog.PlayerHitSound, Is.Not.Null);
            Assert.That(catalog.EnvironmentHitSound, Is.Not.Null);
            Assert.That(catalog.AmbientSound, Is.Not.Null);
        }

        [Test]
        public void ImportedPlayerAndWeaponContainNoGameplayPhysicsAuthority()
        {
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();
            Assert.That(catalog.RobotPrefab!.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(catalog.RobotPrefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(catalog.WeaponPrefab!.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(catalog.WeaponPrefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void FixedCameraProjectsRightSideMouseAimOntoPositiveWorldX()
        {
            var root = new GameObject("Camera Projection Test");
            try
            {
                Camera camera = root.AddComponent<Camera>();
                BattlePresenter.ApplyGameplayCamera(camera);
                Vector3 screen = camera.WorldToScreenPoint(new Vector3(3f, 0f, 0f));
                Ray ray = camera.ScreenPointToRay(screen);
                var plane = new Plane(Vector3.up, Vector3.zero);

                Assert.That(plane.Raycast(ray, out float distance), Is.True);
                Assert.That(ray.GetPoint(distance).x, Is.GreaterThan(2.9f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static BattleState CreateState(
            uint tick,
            BattlePhase phase,
            uint phaseTicks,
            uint roundTicks,
            RoundResult result,
            PlayerSlot? matchWinner,
            PlayerState first,
            PlayerState second,
            params ProjectileState[] projectiles)
        {
            return new BattleState(tick, CreateRoster(), new[] { first, second },
                BattleDefinition.CreateDefault(), phase, phaseTicks, roundTicks,
                result, matchWinner, projectiles, 100);
        }

        private static ActiveRoster CreateRoster() =>
            new ActiveRoster(new[] { new PlayerId(11), new PlayerId(22) });

        private static PlayerState DefaultPlayer(int slot) => slot == 0
            ? new PlayerState(-3500, 0, 0, 100, 0, 0)
            : new PlayerState(3500, 0, 32768, 100, 0, 0);
    }
}
