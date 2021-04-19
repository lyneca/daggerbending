using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;

namespace DaggerBending.States {
    public class OrbitState : DaggerState {
        const float ORBIT_RADIUS = 0.3f;
        const float ORBIT_VERTICAL_RANGE = 1;
        const float TARGET_DISTANCE_AHEAD = 0.8f;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            dagger.IgnoreDaggerCollisions();
            dagger.SetPhysics(0, 0.999f);
        }
        public override bool ShouldIgnorePlayer() => true;
        public override bool CanImbue(RagdollHand hand) => true;
        public override void Update() {
            base.Update();
            dagger.Depenetrate();
            var bodyAndHeight = new Vector3(
                Player.currentCreature.transform.position.x,
                Mathf.Clamp(
                    dagger.item.transform.position.y + UnityEngine.Random.Range(-ORBIT_VERTICAL_RANGE / 4, ORBIT_VERTICAL_RANGE / 4),
                    Utils.GetPlayerChest().transform.position.y - ORBIT_VERTICAL_RANGE / 2,
                    Utils.GetPlayerChest().transform.position.y + ORBIT_VERTICAL_RANGE / 2),
                Player.currentCreature.transform.position.z);
            var positionOnBody = bodyAndHeight + (dagger.item.transform.position - bodyAndHeight).normalized * ORBIT_RADIUS;
            var targetPosition = positionOnBody
                + Vector3.Project(dagger.rb.velocity, Vector3.Cross(Vector3.up, positionOnBody - Utils.GetPlayerChest().transform.position)).normalized
                * TARGET_DISTANCE_AHEAD;
            dagger.pidController.UpdateVelocity(targetPosition);
            dagger.rb.AddForce((positionOnBody - dagger.item.transform.position).normalized * 3);
            dagger.item.Throw();
        }
        public override void Exit() {
            base.Exit();
            dagger.ResetDaggerCollisions();
            dagger.ResetPhysics();
        }
    }
}
