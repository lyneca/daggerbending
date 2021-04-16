using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using ExtensionMethods;

namespace DaggerBending.States {
    class SlingshotState : DaggerState {
        RagdollHand hand;
        int index;
        int count;
        float intensity;
        public override bool ShouldIgnorePlayer() => true;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            dagger.CreateJoint();
            dagger.IgnoreDaggerCollisions();
            dagger.SetPhysics(0);
            Catalog.GetData<EffectData>("DaggerSnickFX").Spawn(dagger.transform).Play();
        }
        public void Init(RagdollHand hand) => this.hand = hand;
        public void UpdateParams(int index, int count, float intensity) {
            this.index = index;
            this.count = count;
            this.intensity = intensity;
        }
        public override void Update() {
            base.Update();
            if (count == 0)
                return;
            Vector3 offset = Quaternion.AngleAxis(360 / count * index, -Vector3.right) * (Vector3.up * (0.3f + 0.2f * (1 - intensity)));
            dagger.UpdateJoint(hand.transform.TransformPoint(offset), Quaternion.LookRotation(controller.SlingshotDir(hand), dagger.transform.position - hand.transform.position));
            hand.transform.TransformDirection(offset);
        }
        public override void Exit() {
            dagger.DeleteJoint();
            dagger.ResetDaggerCollisions();
            dagger.ResetPhysics();
            base.Exit();
        }
    }
}
