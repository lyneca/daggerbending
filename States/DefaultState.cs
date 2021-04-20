using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace DaggerBending.States {
    public class DefaultState : DaggerState {
        public override bool ShouldIgnorePlayer() => true;
        public override void Enter(DaggerBehaviour dagger, DaggerController controller) {
            base.Enter(dagger, controller);
            gatherable = true;
        }
        public override bool CanImbue(RagdollHand hand) => true;
    }
}
