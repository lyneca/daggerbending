using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;

namespace DaggerBending {
    public abstract class DaggerState {
        protected DaggerController controller;
        protected DaggerBehaviour dagger;
        protected float enterTime;
        public bool gatherable = false;
        public virtual bool ShouldIgnorePlayer() => true;
        public virtual bool AllowExplosion() => true;
        public virtual bool Grabbable() => false;
        public virtual bool CanImbue(RagdollHand hand) => false;
        public virtual void Enter(DaggerBehaviour dagger, DaggerController controller) {
            this.controller = controller;
            this.dagger = dagger;
            enterTime = Time.time;
        }
        public virtual void Update() { }
        public virtual void Exit() { }
    }
    public enum GatherResistance {
        Weak,
        Strong,
        Never
    }
}
