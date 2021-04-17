using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using ExtensionMethods;

namespace DaggerBending.States {
    class TrapState : DaggerState {
        Vector3 position;
        bool armed = false;
        public EffectInstance trapEffect;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            trapEffect = Catalog.GetData<EffectData>("DaggerFloatFX").Spawn(dagger.transform.position, dagger.transform.rotation);
            dagger.SetPhysics(0);
        }
        public override bool Grabbable() => true;
        public override void Update() {
            base.Update();
            if (!armed) {
                dagger.transform.position = Vector3.Lerp(dagger.transform.position, position, Time.deltaTime * 10);
                dagger.item.PointItemFlyRefAtTarget(Vector3.down, 1);
                return;
            }
            dagger.pidController.UpdateVelocity(position, 1, 2);
            dagger.item.PointItemFlyRefAtTarget(Vector3.down, 10);
            trapEffect.effects.ForEach(effect => {
                effect.transform.position = dagger.transform.position;
                effect.transform.rotation = dagger.transform.rotation;
            });
            var nearbyCreatures = Creature.list.Where(creature => Vector3.Distance(creature.transform.position, dagger.transform.position) < 3 && creature != Player.currentCreature && creature.state != Creature.State.Dead);
            if (nearbyCreatures.Any()) {
                dagger.TrackCreature(nearbyCreatures.First());
            }
        }
        public void UpdateTrap(Vector3 position, bool armed) {
            if (this.armed)
                return;
            this.position = position;
            if (!this.armed && armed) {
                trapEffect.SetIntensity(1);
                trapEffect.Play();
            }
            this.armed = armed;
        }
        public override void Exit() {
            base.Exit();
            trapEffect.SetIntensity(0);
            trapEffect.Stop();
            dagger.ResetPhysics();
        }

        public void Trap() {

        }
    }
}
