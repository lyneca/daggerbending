using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using ExtensionMethods;

namespace DaggerBending.States {
    class HandState : DaggerState {
        public RagdollHand hand;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            dagger.item.rb.velocity = Vector3.zero;
            dagger.SetPhysics(0);
            dagger.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            dagger.rb.isKinematic = true;
        }
        public override bool ShouldIgnorePlayer() => true;
        public override bool CanImbue(RagdollHand hand) => hand != this.hand;
        public void Init(RagdollHand hand) {
            this.hand = hand;
        }
        public override void Update() {
            base.Update();
            if (hand == null)
                return;
            if (dagger.item.mainHandler != null)
                dagger.IntoState<DefaultState>();
            dagger.item.transform.position = Vector3.Lerp(dagger.item.transform.position, hand.PosAboveBackOfHand(), Time.deltaTime * 10);
            dagger.item.PointItemFlyRefAtTarget(hand.PointDir(), Time.deltaTime * 10, -hand.PalmDir());
        }
        public override void Exit() {
            base.Exit();
            dagger.ResetPhysics();
            dagger.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            dagger.rb.isKinematic = false;
        }
    }
}
