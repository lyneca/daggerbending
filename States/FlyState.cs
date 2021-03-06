﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using ExtensionMethods;

namespace DaggerBending.States {
    class FlyState : DaggerState {
        Quaternion targetRot;
        Vector3 targetPos;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            dagger.IgnoreDaggerCollisions();
            dagger.SetPhysics(0, 0.999f);
            dagger.CreateJoint();
        }
        public override bool ShouldIgnorePlayer() => true;
        public override bool CanImbue(RagdollHand hand) => false;
        public void UpdateTarget(Vector3 pos, Quaternion rot) {
            targetPos = pos;
            targetRot = rot;
        }
        public override void Update() {
            base.Update();
            dagger.UpdateJoint(targetPos, targetRot, 10, Time.deltaTime * 10);
            dagger.item.Throw();
        }
        public override void Exit() {
            base.Exit();
            dagger.ResetDaggerCollisions();
            dagger.ResetPhysics();
            dagger.DeleteJoint();
        }
    }
}
