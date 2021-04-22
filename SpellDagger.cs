using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace DaggerBending {
    using States;
    public class SpellDagger : SpellCastCharge {
        public static int maxDaggerCount = 36;
        public string itemId;
        public static bool allowPunchDagger = false;
        public bool daggersOrbitWhenIdle = true;
        public bool isCasting = false;
        public bool debugEnabled = false;
        bool hasSpawnedDagger = false;
        bool isSpawningHandle = false;
        DaggerController controller;
        EffectData grabPointEffectData;
        EffectInstance grabPointFX;
        ItemData handleData;
        public Item handle;
        bool hasExplosionStarted = false;
        public override void OnCatalogRefresh() {
            base.OnCatalogRefresh();
            grabPointEffectData = Catalog.GetData<EffectData>("SlingshotGrabPoint");
            handleData = Catalog.GetData<ItemData>("InvisibleHandle");
        }
        public override void Load(SpellCaster spellCaster) {
            base.Load(spellCaster);
            hasSpawnedDagger = false;
            isSpawningHandle = false;
            controller = spellCaster.mana.gameObject.GetOrAddComponent<DaggerController>();
            controller.itemId = itemId;
            controller.daggersOrbitWhenIdle = daggersOrbitWhenIdle;
            isCasting = false;
        }
        public override void Load(Imbue imbue) {
            if (imbue.colliderGroup.collisionHandler.item is Item item && item.itemId == itemId) {
                item.gameObject.GetOrAddComponent<DaggerBehaviour>();
                base.Load(imbue);
            }
        }
        public override void OnImbueCollisionStart(CollisionInstance collisionInstance) {
            if (imbue == null)
                return;
            if (imbue.colliderGroup?.collisionHandler?.item is Item item && item.gameObject?.GetOrAddComponent<DaggerBehaviour>() is var dagger && !hasExplosionStarted) {
                if (dagger.state == null)
                    return;
                if (!dagger.state.AllowExplosion())
                    return;
                base.OnImbueCollisionStart(collisionInstance);
                hasExplosionStarted = true;
                dagger.StartCoroutine(dagger.Explosion());
            }
        }
        public override void UpdateImbue() {
            if (imbue == null)
                return;
            base.UpdateImbue();
        }
        public override void SlowUpdateImbue() {
            if (imbue == null)
                return;
            base.SlowUpdateImbue();
        }
        public DaggerBehaviour GetHeld() => GetDaggers().FirstOrDefault(dagger => dagger.IsAtHand(spellCaster.ragdollHand));
        List<DaggerBehaviour> GetDaggers() => controller.daggers;
        public override void Unload() {
            base.Unload();
            isCasting = false;
        }
        public override void Fire(bool active) {
            base.Fire(active);
            isCasting = active;
            if (active) {
                hasSpawnedDagger = false;
                imbueEnabled = true;
            } else {
                if (!GetHeld())
                    return;
                if (handle != null)
                    DespawnHandle();
                GetHeld()?.IntoState<OrbitState>();
            }
        }
        public bool IsGripping() => spellCaster?.ragdollHand?.IsGripping() ?? false;
        public void DetectNoGrip() {
            // Get hand velocity relative to head
            var velocity = spellCaster.ragdollHand.transform.InverseTransformVector(spellCaster.ragdollHand.Velocity())
                - Player.currentCreature.ragdoll.headPart.rb.velocity;

            // Get angular hand velocity
            Vector3 handAngularVelocity = spellCaster.ragdollHand.LocalAngularVelocity();

            // Spawn dagger on flick back of hand
            if (isCasting && !GetHeld() && velocity.z > 3) {
                hasSpawnedDagger = true;
                imbueEnabled = false;
                controller.SpawnDagger(dagger => {
                    dagger.SpawnSizeIncrease(spellCaster.ragdollHand);
                    dagger.transform.position = spellCaster.ragdollHand.PosAboveBackOfHand();
                    dagger.item.PointItemFlyRefAtTarget(spellCaster.ragdollHand.PointDir(), 1, -spellCaster.ragdollHand.PalmDir());
                    dagger.PlaySpawnEffect();
                    dagger.IntoHand(spellCaster.ragdollHand);
                });
                return;
            }

            // Pick closest dagger from the orbiting ones
            if (isCasting && !GetHeld()
                          && (spellCaster.ragdollHand.side == Side.Right ? handAngularVelocity.z < -7 : handAngularVelocity.z > 7)
                          && handAngularVelocity.MostlyZ()
                          && controller.DaggerAvailable(5)) {
                var dagger = controller.GetFreeDaggerClosestTo(spellCaster.ragdollHand.transform.position, 5);
                hasSpawnedDagger = true;
                imbueEnabled = false;
                Catalog.GetData<EffectData>("DaggerSelectFX").Spawn(dagger.transform).Play();
                spellCaster.ragdollHand.PlayHapticClipOver(new AnimationCurve(new Keyframe(0f, 0), new Keyframe(1f, 1)), 0.3f);
                dagger.IntoHand(spellCaster.ragdollHand);
                return;
            }
        }


        public Vector3 GetHandlePosition() => spellCaster.ragdollHand.transform.position
            + GetHandleOffset();

        public Vector3 GetHandleOffset() => spellCaster.ragdollHand.PalmDir() * 0.1f
            - spellCaster.ragdollHand.PointDir() * 0.05f;

        void DespawnHandle() {
            handle.GetMainHandle(spellCaster.ragdollHand.otherHand.side).Release();
            grabPointFX.Despawn();
            handle?.Despawn();
        }

        public override void UpdateCaster() {
            base.UpdateCaster();
            if (handle != null) {
                handle.rb.useGravity = false;
                if (handle.mainHandler == null) {
                    handle.transform.position = GetHandlePosition();
                }
                if (!IsGripping() || !isCasting || spellCaster.ragdollHand.grabbedHandle != null)
                    DespawnHandle();
            } else if (isCasting && IsGripping() && !isSpawningHandle && spellCaster.ragdollHand.grabbedHandle == null) {
                isSpawningHandle = true;
                handleData.SpawnAsync(item => {
                    handle = item;
                    grabPointFX = grabPointEffectData.Spawn(handle.transform);
                    grabPointFX.Play();
                    handle.GetMainHandle(spellCaster.ragdollHand.side)
                        .orientations
                        .Remove(handle.GetMainHandle(spellCaster.ragdollHand.side)
                            .GetDefaultOrientation(spellCaster.ragdollHand.side));
                    isSpawningHandle = false;
                    //Catalog.GetData<EffectData>("SlingshotHandleFX").Spawn(handle.transform);
                });
            }

            if (IsGripping() && GetHeld() && GetHeld().isFullySpawned) {
                var dagger = GetHeld();
                dagger.IntoState<DefaultState>();
                Fire(false);
                currentCharge = 0;
                spellCaster.isFiring = false;
                spellCaster.ragdollHand.Grab(dagger.item.GetMainHandle(spellCaster.ragdollHand.side));
                //controller.RunAfter(() => imbueEnabled = true, 0.5f);
            }

            if (hasSpawnedDagger)
                return;

            if (!GetHeld()) {
                imbueEnabled = true;
            }
            if (IsGripping() && isCasting && !spellCaster.ragdollHand.grabbedHandle) {
                imbueEnabled = false;
            }
            if (spellCaster.imbueObjects.Any()) {
                var imbuingObjects = spellCaster.imbueObjects
                    .Where(obj => obj.colliderGroup.imbue is Imbue imbue
                               && imbue.spellCastBase is SpellDagger
                               && obj.item.GetComponent<DaggerBehaviour>().state.CanImbue(spellCaster.ragdollHand))
                    .Select(obj => obj.colliderGroup.imbue);
                if (imbuingObjects.Any()) {
                    var intensity = imbuingObjects.Average(imbue => imbue.energy / imbue.maxEnergy
                                    * Mathf.InverseLerp(0.3f, 0.05f, Vector3.Distance(spellCaster.magic.position, imbue.colliderGroup.transform.position)));
                    spellCaster.ragdollHand.HapticTick(intensity);
                }
            }

            if (!IsGripping()) {
                DetectNoGrip();
            }
        }

        public override void Throw(Vector3 velocity) {
            base.Throw(velocity);
            var dagger = GetHeld();
            if (dagger) {
                dagger.ThrowForce(velocity * 2, true);
                dagger.SpawnThrowFX(velocity);
                controller.PlayThrowClip(spellCaster.ragdollHand);
            }
        }
    }
}
