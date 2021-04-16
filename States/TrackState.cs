using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using ExtensionMethods;

namespace DaggerBending.States {
    class TrackState : DaggerState {
        Creature target;
        bool startedTracking;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            startedTracking = false;
            dagger.SetPhysics(0);
            float modifier = 1;
            if (dagger.rb.mass < 1) {
                modifier *= dagger.rb.mass;
            } else {
                modifier *= dagger.rb.mass * Mathf.Clamp(dagger.rb.drag, 1, 2);
            }
            dagger.rb.AddForce(Vector3.up * 30f * modifier, ForceMode.Acceleration);
        }
        public void Init(Creature creature) {
            target = creature;
        }
        public override void Update() {
            base.Update();
            if (!target)
                return;
            dagger.item.Throw(1, Item.FlyDetection.Forced);
            if (target.state == Creature.State.Dead) {
                dagger.IntoState<DefaultState>();
            }
            if (dagger.item.isPenetrating) {
                if (dagger.item.collisionHandlers
                        .Where(handler => handler.collisions
                            .Where(collision => collision.damageStruct.hitRagdollPart?.ragdoll.creature == target)
                            .Any())
                        .Any()) {
                    dagger.IntoState<OrbitState>();
                    return;
                } else {
                    dagger.Depenetrate();
                }
            }
            if (Time.time - enterTime > 0.5f) {
                var targetHead = target.GetHead().transform;
                if (!startedTracking) {
                    dagger.SpawnThrowFX(targetHead.position - dagger.transform.position);
                    startedTracking = true;
                }
                dagger.item.PointItemFlyRefAtTarget(targetHead.position - dagger.transform.position, 1);
                dagger.rb.AddForce((targetHead.position - dagger.item.transform.position).normalized * 2f, ForceMode.Impulse);
                // Reorient force vector to target head
                if (Vector3.Distance(targetHead.position, dagger.transform.position) > 0.2f)
                    dagger.rb.velocity = Vector3.Project(dagger.rb.velocity, targetHead.position - dagger.transform.position);
            }
        }
        public override void Exit() {
            base.Exit();
            dagger.item.mainCollisionHandler.RemovePhysicModifier(this);
            dagger.ResetPhysics();
        }
    }
}
