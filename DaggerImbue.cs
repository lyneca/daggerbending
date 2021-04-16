using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace DaggerBending {
    class DaggerImbueModule : ItemModule {
        public override void OnItemLoaded(Item item) {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<DaggerImbueBehaviour>();
        }
    }

    class DaggerImbueBehaviour : MonoBehaviour {
        public Item item;
        public void Start() {
            item = GetComponent<Item>();
        }

        public void Update() {
            if (item == null)
                return;
            if (item.mainHandler && (item.mainHandler?.playerHand?.controlHand?.usePressed ?? false)) {
                if (Player.currentCreature.mana.GetCaster(item.mainHandler.side).spellInstance is SpellCastCharge spell && spell != null && spell.imbueEnabled) {
                    foreach (var group in item.colliderGroups.Where(group =>
                        group.data.modifiers.Where(mod => mod.imbueType != ColliderGroupData.ImbueType.None
                                                && spell.imbueAllowMetal || mod.imbueType != ColliderGroupData.ImbueType.Metal).Any())) {
                        group.imbue.Transfer(spell, 3);
                    }
                }
            }
        }
    }
}
