using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using ExtensionMethods;

namespace DaggerBending.States {
    class ClawSwordState : DaggerState {
        public RagdollHand hand;
        EffectInstance whooshEffect;
        public int index;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            whooshEffect = Catalog.GetData<EffectData>("ClawsWhoosh").Spawn(dagger.transform);
            whooshEffect.Play();
            dagger.SetPhysics(0);
            dagger.CreateJoint();
            dagger.IgnoreDaggerCollisions();
            dagger.item.mainCollisionHandler.OnCollisionStartEvent += CollisionEvent;
        }
        public void CollisionEvent(CollisionInstance collision) {
            hand?.playerHand.controlHand.HapticShort(1);
        }
        public override bool ShouldIgnorePlayer() => true;
        public void Init(RagdollHand hand, int index) {
            this.hand = hand;
            this.index = index;
        }
        public override bool CanImbue(RagdollHand hand) => hand != this.hand;
        public override void Update() {
            base.Update();
            if (!hand)
                return;
            whooshEffect.SetSpeed(Mathf.InverseLerp(3, 12, dagger.rb.velocity.magnitude));
            Vector3 position;
            Quaternion rotation;
            if (hand.playerHand.controlHand.usePressed) {
                var fistIndex = (hand.side == Side.Right) ? index : (2 - index);
                position = hand.Palm() + hand.ThumbDir() * 0.15f + hand.ThumbDir() * (0.4f * (fistIndex + 0.3f * hand.Velocity().magnitude));
                rotation = Quaternion.LookRotation(hand.ThumbDir(), -hand.PalmDir());
            } else {
                var angleTarget = Mathf.Clamp(60 - hand.Velocity().magnitude * 15, 15, 60);
                var angle = index * angleTarget - angleTarget;
                Vector3 offset = Quaternion.AngleAxis(angle, -Vector3.right)
                    * (Vector3.forward * (0.2f + 0.3f * hand.Velocity().magnitude / 2))
                    + (-Vector3.right - Vector3.forward) * 0.3f * hand.Velocity().magnitude / 2;
                position = hand.transform.TransformPoint(offset);
                rotation = Quaternion.LookRotation(hand.PointDir(), -hand.PalmDir());
            }
            dagger.UpdateJoint(position, rotation, 10);
            dagger.item.Throw(1, Item.FlyDetection.Forced);
            dagger.item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
        }
        public override void Exit() {
            base.Exit();
            dagger.ResetPhysics();
            dagger.item.mainCollisionHandler.OnCollisionStartEvent -= CollisionEvent;
            whooshEffect.End();
            dagger.DeleteJoint();
            dagger.ResetDaggerCollisions();
        }
    }
}
