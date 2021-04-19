﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace DaggerBending.States {
    class PouchState : DaggerState {
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            dagger.item.GetMainHandle(Side.Left).SetTouch(false);
            dagger.item.GetMainHandle(Side.Right).SetTouch(false);
            dagger.SetPhysics(0);
            dagger.IgnoreDaggerCollisions();
            dagger.CreateJoint();
        }
        public override bool ShouldIgnorePlayer() => true;
        public override bool CanImbue(RagdollHand hand) => false;
        public override void Update() {
            base.Update();
            var pouch = controller.GetNonFullPouches().MinBy(quiver => Vector3.Distance(quiver.transform.position, dagger.transform.position));
            if (dagger.item.handlers.Any() || dagger.item.isTelekinesisGrabbed || dagger.item.isGripped) {
                dagger.IntoState<DefaultState>();
                return;
            }
            if (!pouch) {
                dagger.IntoState<OrbitState>();
                return;
            }
            var distance = Vector3.Distance(dagger.transform.position, pouch.transform.position);
            dagger.UpdateJoint(
                pouch.transform.position + pouch.transform.up * Mathf.Clamp(distance / 2, 0, 1),
                Quaternion.LookRotation(pouch.transform.position - dagger.transform.position, pouch.transform.right));
            dagger.Depenetrate();
            if (Vector3.Distance(dagger.transform.position, pouch.transform.position) < 0.2f) {
                dagger.item.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                //pouch.RefreshChildAndParentHolder();
                pouch.SetTouch(false);
                pouch.Snap(dagger.item);
                pouch.SetTouch(true);
                Player.currentCreature.handLeft.ClearTouch();
                Player.currentCreature.handRight.ClearTouch();
                dagger.IntoState<DefaultState>();
            }
        }
        public override void Exit() {
            base.Exit();
            dagger.DeleteJoint();
            dagger.item.GetMainHandle(Side.Left).SetTouch(true);
            dagger.item.GetMainHandle(Side.Right).SetTouch(true);
            dagger.ResetDaggerCollisions();
            dagger.ResetPhysics();
        }
    }
}
