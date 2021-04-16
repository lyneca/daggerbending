using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace DaggerBending {
    public class SpellDagger : SpellCastCharge {
        public static int maxDaggerCount = 36;
        public static bool allowPunchDagger = false;
        public bool isCasting = false;
        bool hasSpawnedDagger = false;
        bool isSpawningHandle = false;
        DaggerController controller;
        EffectData grabPointEffectData;
        EffectInstance grabPointFX;
        ItemData handleData;
        public Item handle;
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
            isCasting = false;
        }
        public DaggerBehaviour GetHeld() => GetDaggers().FirstOrDefault(dagger => dagger.GetState() == DaggerState.Hand && dagger.hand == spellCaster.ragdollHand);
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
            } else {
                if (!GetHeld())
                    return;
                if (handle != null)
                    DespawnHandle();
            }
            GetHeld()?.IntoOrbit();
        }
        public bool IsGripping() => spellCaster.ragdollHand.IsGripping();
        public void DetectNoGrip() {
            // Get hand velocity relative to head
            var velocity = spellCaster.ragdollHand.transform.InverseTransformVector(spellCaster.ragdollHand.rb.velocity)
                - Player.currentCreature.ragdoll.headPart.rb.velocity;

            // Get angular hand velocity
            Vector3 handAngularVelocity = spellCaster.ragdollHand.LocalAngularVelocity();

            // Spawn dagger on flick back of hand
            if (isCasting && !GetHeld() && velocity.z > 3) {
                hasSpawnedDagger = true;
                controller.SpawnDagger(dagger => {
                    dagger.IntoHand(spellCaster.ragdollHand);
                    dagger.transform.position = spellCaster.ragdollHand.PosAboveBackOfHand();
                    dagger.item.PointItemFlyRefAtTarget(spellCaster.ragdollHand.PointDir(), 1, -spellCaster.ragdollHand.PalmDir());
                    dagger.PlaySpawnEffect();
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
                Catalog.GetData<EffectData>("DaggerSelectFX").Spawn(dagger.transform).Play();
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
                if (!IsGripping() && !isCasting)
                    DespawnHandle();
            } else if (isCasting && IsGripping() && !isSpawningHandle) {
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

            if (IsGripping() && GetHeld()) {
                var dagger = GetHeld();
                dagger.Release();
                Fire(false);
                currentCharge = 0;
                spellCaster.isFiring = false;
                spellCaster.ragdollHand.Grab(dagger.item.GetMainHandle(spellCaster.ragdollHand.side));
            }

            if (hasSpawnedDagger)
                return;

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
            }
        }
    }
}
