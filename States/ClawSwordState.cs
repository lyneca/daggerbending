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
        public int index;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            dagger.SetPhysics(0);
            dagger.CreateJoint();
            dagger.IgnoreDaggerCollisions();
        }
        public override bool ShouldIgnorePlayer() => true;
        public void Init(RagdollHand hand, int index) {
            this.hand = hand;
            this.index = index;
        }
        public override void Update() {
            base.Update();
            Vector3 position;
            Quaternion rotation;
            if (hand.playerHand.controlHand.usePressed) {
                var fistIndex = (hand.side == Side.Right) ? index : (2 - index);
                position = hand.Palm() + hand.ThumbDir() * 0.15f + hand.ThumbDir() * (0.4f * (fistIndex + 0.3f * hand.rb.velocity.magnitude));
                rotation = Quaternion.LookRotation(hand.ThumbDir(), -hand.PalmDir());
            } else {
                var angleTarget = Mathf.Clamp(60 - hand.rb.velocity.magnitude * 15, 15, 60);
                var angle = index * angleTarget - angleTarget;
                Vector3 offset = Quaternion.AngleAxis(angle, -Vector3.right)
                    * (Vector3.forward * (0.2f + 0.3f * hand.rb.velocity.magnitude / 2))
                    + (-Vector3.right - Vector3.forward) * 0.3f * hand.rb.velocity.magnitude / 2;
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
            dagger.DeleteJoint();
            dagger.ResetDaggerCollisions();
        }
    }
}
