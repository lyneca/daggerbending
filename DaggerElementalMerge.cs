using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace DaggerBending {
    class DaggerElementalMerge : SpellMergeData {
        public DaggerController controller;
        public float lastImbueTime = 0;
        public float imbueDelay = 0.5f;
        public bool active;
        public SpellCaster daggerCaster;
        public SpellCaster otherCaster;

        public override void Load(Mana mana) {
            base.Load(mana);
            controller = Player.currentCreature.mana.gameObject.GetComponent<DaggerController>();
        }

        public override void Merge(bool active) {
            base.Merge(active);
            this.active = active;
            if (active) {
                daggerCaster = (mana.casterLeft.spellInstance is SpellDagger) ? mana.casterLeft : mana.casterRight;
                otherCaster = daggerCaster.ragdollHand.otherHand.caster;
            }
        }

        public override void Update() {
            base.Update();
            if (Time.time - lastImbueTime > imbueDelay) {
                if (otherCaster && otherCaster.spellInstance is SpellCastCharge spell) {
                    controller.ImbueRandomDagger(spell, mana.mergePoint);
                    lastImbueTime = Time.time;
                }
            }
        }
    }
}
