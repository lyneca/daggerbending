using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using ExtensionMethods;

namespace DaggerBending.States {
    class ShieldState : DaggerState {
        public RagdollHand hand;
        public bool isBigShield;
        int total;
        int index;
        Vector3 position;
        Quaternion rotation;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            dagger.Depenetrate();
            dagger.IgnoreDaggerCollisions();
            dagger.SetPhysics(0);
            dagger.CreateJoint();
            dagger.item.mainCollisionHandler.OnCollisionStartEvent += CollisionEvent;
        }
        public void CollisionEvent(CollisionInstance collision) {
            if (isBigShield) {
                controller.GetHand(Side.Left).playerHand.controlHand.HapticShort(1);
                controller.GetHand(Side.Right).playerHand.controlHand.HapticShort(1);
            } else {
                hand?.playerHand.controlHand.HapticShort(1);
            }
        }
        public override bool ShouldIgnorePlayer() => true;
        public override bool CanImbue(RagdollHand hand) => hand != this.hand;
        public void Init(int index, RagdollHand hand = null, bool isBigShield = false) {
            this.index = index;
            this.hand = hand;
            this.isBigShield = isBigShield;
        }
        public void UpdateShield(Vector3 position, Quaternion rotation, int total) {
            this.position = position;
            this.rotation = rotation;
            this.total = total;
        }
        public Vector3 GetOffset() => GetShieldOffset(total, index) * 0.2f;
        public override void Update() {
            base.Update();
            var offset = GetOffset();
            dagger.UpdateJoint(position + rotation * new Vector3(offset.x * 0.5f, offset.y, 0),
                Quaternion.LookRotation(rotation * Vector3.down, rotation * Vector3.right), 10);
            if (!dagger.item.isFlying) {
                dagger.item.Throw(1, Item.FlyDetection.Forced);
            }
        }
        public override void Exit() {
            base.Exit();
            dagger.item.mainCollisionHandler.OnCollisionStartEvent -= CollisionEvent;
            dagger.ResetPhysics();
            dagger.ResetDaggerCollisions();
            dagger.DeleteJoint();
        }

        // ---

        public static int[] ShieldPoint(int total) {
            switch (total) {
                case 1:
                    return new int[] { 1 };
                case 2:
                    return new int[] { 2 };
                case 3:
                    return new int[] { 1, 2 };
                case 4:
                    return new int[] { 1, 2, 1 };
                case 5:
                    return new int[] { 3, 2 };
                case 6:
                    return new int[] { 3, 2, 1 };
                case 7:
                    return new int[] { 3, 4 };
                case 8:
                    return new int[] { 1, 4, 3 };
                case 9:
                    return new int[] { 4, 3, 2 };
                case 10:
                    return new int[] { 3, 4, 3 };
                case 11:
                    return new int[] { 4, 3, 4 };
                case 12:
                    return new int[] { 5, 4, 3 };
                case 13:
                    return new int[] { 4, 5, 4 };
                case 14:
                    return new int[] { 5, 4, 5 };
                case 15:
                    return new int[] { 1, 4, 5, 4, 1 };
                case 16:
                    return new int[] { 5, 6, 5 };
                default:
                    return new int[] { 6, 5, 6 };
            }
        }
        public static Vector2 GetShieldOffset(int total, int index) {
            float totalSoFar = 0;
            var rows = ShieldPoint(total);
            float y = -(rows.Count() - 1) / 2;
            foreach (int row in rows) {
                if (index > totalSoFar + row - 1) {
                    totalSoFar += row;
                    y++;
                    continue;
                }
                return new Vector2(index - totalSoFar - ((float)row - 1) / 2, y);
            }
            Debug.Log("shield offset func returned null, this should not happen");
            return default;
        }
    }
}
