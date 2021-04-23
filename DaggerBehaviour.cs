using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;
using Random = UnityEngine.Random;

namespace DaggerBending {
    using States;

    public class DaggerBehaviour : MonoBehaviour {
        public float lastNoOrbitTime;
        readonly float orbitCooldown = 1.5f;
        bool justSpawned = true;
        public float decorationIndex = -1;
        public List<CollisionHandler.PhysicModifier> orgPhysicModifiers;
        public Rigidbody jointObj;
        public ConfigurableJoint joint;
        public DaggerState state = null;
        DaggerController controller;
        public PIDRigidbodyHelper pidController;
        public Item item;
        public List<DaggerBehaviour> ignoredDaggers = new List<DaggerBehaviour>();
        public EffectInstance trailEffect;
        public bool isFullySpawned = true;

        public Rigidbody rb;
        public void Start() {
            item = GetComponent<Item>();
            rb = item.rb;
            //item.mainCollisionHandler.damagers.ForEach(damager => damager.penetrationExitOnMaxDepth = true);
            controller = Player.currentCreature.mana.gameObject.GetComponent<DaggerController>();
            Catalog.GetData<EffectData>("SlingshotGrabPoint").Spawn(transform).Play();
            trailEffect = Catalog.GetData<EffectData>("ShiftTrail").Spawn(transform);
            trailEffect.SetIntensity(0);
            trailEffect.Play();
            item.OnSnapEvent += holder => {
                item.lastHandler?.ClearTouch();
                item.lastHandler = null;
                IntoState<DefaultState>();
                if (item.transform.parent.GetComponentsInChildren<Item>() is Item[] items && items.Count() > 1) {
                    var slotToUse = holder.slots.FirstOrDefault(slot => slot.GetComponentsInChildren<Item>().Count() == 0);
                    if (slotToUse == null)
                        return;
                    var holderPoint = item.GetHolderPoint(holder.data.targetAnchor);
                    item.transform.MoveAlign(holderPoint.anchor, slotToUse.transform, slotToUse.transform);
                }
                if (holder.GetComponentInParent<Item>() is Item holderItem) {
                    item.IgnoreObjectCollision(holderItem);
                }
            };
            item.OnUnSnapEvent += holder => {
                if (holder.GetComponentInParent<Item>() is Item holderItem) {
                    item.RunNextFrame(() => item.IgnoreObjectCollision(holderItem));
                    item.RunAfter(() => item.ResetObjectCollision(), 0.1f);
                }
            };
            item.OnTelekinesisGrabEvent += (handle, grabber) => {
                if (!state.Grabbable())
                    IntoState<DefaultState>();
            };
            item.OnGrabEvent += (handle, hand) => {
                IntoState<DefaultState>();
                foreach (var collider in hand.colliderGroup.colliders) {
                    foreach (var group in item.colliderGroups) {
                        foreach (var otherCollider in group.colliders) {
                            Physics.IgnoreCollision(collider, otherCollider);
                        }
                    }
                }
                controller.RunAfter(() => {
                    if (hand.caster?.spellInstance is SpellCastCharge spell) {
                        spell.imbueEnabled = true;
                    }
                }, 0.5f);
            };
            item.OnUngrabEvent += (handle, hand, throwing) => {
                var velocity = Player.local.transform.rotation * PlayerControl.GetHand(hand.side).GetHandVelocity();
                if (throwing && velocity.magnitude > 3) {
                    lastNoOrbitTime = Time.time;
                    IntoState<DefaultState>();
                }
            };
            pidController = new PIDRigidbodyHelper(rb, 5, 1);
            if (state == null)
                IntoState<DefaultState>();
        }

        public void SpawnSizeIncrease(RagdollHand hand = null) {
            isFullySpawned = false;
            StartCoroutine(ScaleOverTime(hand));
        }
        public IEnumerator ScaleOverTime(RagdollHand hand = null) {
            float time = Time.time;
            while (Time.time - time < 0.2f) {
                float amount = Mathf.Clamp((Time.time - time) / 0.2f, 0, 1).MapOverCurve(
                    Tuple.Create(0f, 0f, 0f, 0f),
                    Tuple.Create(1f, 1f, 0f, 0f));
                item.transform.localScale = Vector3.one * amount;
                hand?.HapticTick(amount);
                yield return 0;
            }
            isFullySpawned = true;
        }
        public void PlaySpawnEffect() {
            var spawnFX = Catalog.GetData<EffectData>("DaggerSpawnFX").Spawn(item.transform);
            spawnFX.SetMesh(GetItemMesh());
            spawnFX.SetSource(GetItemMeshObject().transform);
            spawnFX.Play();
        }

        public void DisableCollisions() {
            foreach (var cg in item.colliderGroups) {
                foreach (var collider in cg.colliders) {
                    collider.enabled = false;
                }
            }
        }
        public void EnableCollisions() {
            foreach (var cg in item.colliderGroups) {
                foreach (var collider in cg.colliders) {
                    collider.enabled = true;
                }
            }
        }

        public bool Held() => item.mainHandler != null && !item.isTelekinesisGrabbed;


        SpellCastCharge GetImbueSpellFromType(ImbueType type) {
            string id = "";
            switch (type) {
                case ImbueType.Fire:
                    id = "Fire";
                    break;
                case ImbueType.Lightning:
                    id = "Lightning";
                    break;
                case ImbueType.Gravity:
                    id = "Gravity";
                    break;
            }
            return Player.currentCreature.mana.spells.Find(spell => spell.id == id) as SpellCastCharge;
        }

        public void Imbue(ImbueType type) {
            Imbue(GetImbueSpellFromType(type));
        }

        public void Imbue(SpellCastCharge spell) {
            item.colliderGroups.ForEach(group => {
                if (group.data.modifiers.Where(mod => mod.imbueType != ColliderGroupData.ImbueType.None
                                                   && spell.imbueAllowMetal || mod.imbueType != ColliderGroupData.ImbueType.Metal)
                                        .Any()) {
                    group.imbue.Transfer(spell, group.imbue.maxEnergy);
                }
            });
        }

        public Imbue GetImbue() => item.imbues.FirstOrDefault(imbue => imbue.energy > 0);

        public GameObject GetItemMeshObject() {
            var bounds = item.renderers.First().bounds;
            var colliderGroups = item.colliderGroups.Where(
                group => group?
                    .imbueEmissionRenderer?
                    .GetComponent<MeshFilter>() != null);
            if (colliderGroups.Any()
                && colliderGroups.First().imbueEmissionRenderer is var emissionRenderer
                && emissionRenderer
                && emissionRenderer.GetComponent<MeshFilter>()) {
                return emissionRenderer.gameObject;
            } else if (item.GetComponentInChildren<MeshFilter>() is MeshFilter filter && filter != null) {
                return filter.gameObject;
            } else {
                return null;
                //vfx.vfx.SetTexture("PositionMap", null);
            }
        }
        public Mesh GetItemMesh() {
            Mesh currentMesh = GetItemMeshObject()?.GetComponent<MeshFilter>()?.mesh;
            Mesh mesh = new Mesh {
                vertices = currentMesh.vertices,
                uv = currentMesh.uv,
                triangles = currentMesh.triangles
            };
            return mesh;
        }

        public void Despawn() {
            if (!item)
                return;
            controller.daggers.Remove(this);
            IntoState<DefaultState>();
            trailEffect.Despawn();
            item.Despawn(0.1f);
        }

        public void SpawnThrowFX(Vector3 velocity) {
            var data = Catalog.GetData<EffectData>("DaggerThrowFX");
            var holder = new GameObject();
            holder.transform.position = item.transform.position + velocity * 0.2f;
            holder.transform.rotation = Quaternion.LookRotation(velocity);
            var instance = data.Spawn(holder.transform);
            instance.Play();
        }

        public void Update() {
            if (!item)
                return;
            if (!Held()) {
                item.lastHandler = null;
            }
            trailEffect.SetIntensity(Mathf.Clamp(rb.velocity.magnitude * 0.1f, 0, 1));
            if (state.ShouldIgnorePlayer()) {
                IgnorePlayerCollisions();
            } else {
                ResetPlayerCollisions();
            }
            state?.Update();
            justSpawned = false;
        }

        public void IntoState<T>() where T : DaggerState, new() {
            if (new T() is PouchState && !controller.PouchSlotAvailable()) {
                if (controller.daggersOrbitWhenIdle) {
                    IntoState<OrbitState>();
                } else {
                    IntoState<DefaultState>();
                }
                return;
            }
            if (new T() is OrbitState && !controller.daggersOrbitWhenIdle) {
                IntoState<PouchState>();
                return;
            }
            // Don't re-enter a state we're already in
            if (state is T)
                return;
            state?.Exit();
            bool oldCollide = state?.ShouldIgnorePlayer() ?? false;
            System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(1);
            var method = frame.GetMethod();
            if (controller?.debug ?? false)
                Debug.Log($"{method.DeclaringType}.{method.Name} is setting state from {state} to {typeof(T).FullName}.");

            state = new T();
            bool newCollide = state?.ShouldIgnorePlayer() ?? false;
            if (controller?.debug ?? false)
                Debug.Log($"Old state {(oldCollide ? "ignores" : "resets")} collisions, new state {(newCollide ? "ignores" : "resets")} them.");
            if (oldCollide && !newCollide)
                ResetPlayerCollisions();
            else if (!oldCollide && newCollide) {
                IgnorePlayerCollisions();
            }
            state.Enter(this, controller);
        }

        public bool CheckState<T>() where T : DaggerState => state is T;

        public void TrackCreature(Creature creature) {
            IntoState<TrackState>();
            if (state is TrackState trackState) {
                trackState.Init(creature);
            }
        }
        public void TrackRandomTarget(IEnumerable<Creature> creatures = null) {
            var npcs = creatures ?? Utils.GetAliveNPCs();
            float modifier = 1;
            if (rb.mass < 1) {
                modifier *= rb.mass;
            } else {
                modifier *= rb.mass * Mathf.Clamp(rb.drag, 1, 2);
            }
            rb.AddForce((transform.position - Player.currentCreature.transform.position).normalized * 3f * modifier, ForceMode.Impulse);
            if (!npcs.Any())
                return;
            TrackCreature(npcs.ElementAtOrDefault(Random.Range(0, npcs.Count())));
        }

        public DaggerState GetState() => state;

        public bool ShouldOrbit() => (Time.time - lastNoOrbitTime > orbitCooldown);

        public bool CanDespawn() => !justSpawned && CanOrbit() && (CheckState<DefaultState>() || CheckState<OrbitState>());
        public bool CanOrbit() => item != null
             && !item.isTelekinesisGrabbed
             && state.gatherable
             && !item.isGripped
             && !justSpawned
             && (!item?.handlers?.Any() ?? false)
             && item?.holder == null;

        public Vector3 HomingThrow(Vector3 velocity, float homingAngle) {
            var hits = Physics.SphereCastAll(item.transform.position, 10, velocity, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var targets = hits.SelectNotNull(hit => hit.collider?.attachedRigidbody?.GetComponentInParent<Creature>())
                .Where(creature => creature != Player.currentCreature && creature.state != Creature.State.Dead)
                .Where(creature => Vector3.Angle(velocity, creature.GetHead().transform.position - item.transform.position)
                     < homingAngle + 3 * Vector3.Distance(item.transform.position, Player.currentCreature.transform.position))
                .OrderBy(creature => Vector3.Angle(velocity, creature.GetHead().transform.position - item.transform.position));
            var closeToAngle = targets.Where(creature => Vector3.Angle(velocity, creature.GetHead().transform.position - item.transform.position) < 5);
            if (closeToAngle.Any()) {
                targets = closeToAngle.OrderBy(creature => Vector3.Distance(item.transform.position, creature.GetHead().transform.position));
            }
            var target = targets.FirstOrDefault();
            if (!target)
                return velocity;
            var extendedPoint = item.transform.position + velocity.normalized * Vector3.Distance(item.transform.position, target.GetTorso().transform.position);
            if (controller.debug) {
                controller.debugObj = controller.debugObj ?? new GameObject();
                controller.debugObj.transform.position = extendedPoint;
                controller.debugObj.GetComponentInChildren<EffectInstance>()?.Despawn();
                Catalog.GetData<EffectData>("SlingshotGrabPoint").Spawn(controller.debugObj.transform).Play();
            }
            var targetPart = target.ragdoll.parts.MinBy(part => Vector3.Distance(part.transform.position, extendedPoint));
            var vectorToTarget = targetPart.transform.position - item.transform.position;
            item.rb.velocity = Vector3.zero;
            velocity = vectorToTarget.normalized * velocity.magnitude;
            return velocity;
        }

        public void IntoHand(RagdollHand hand) {
            IntoState<HandState>();
            if (state is HandState handState) {
                handState.Init(hand);
            }
        }

        public void IntoSlingshot(RagdollHand hand) {
            IntoState<SlingshotState>();
            if (state is SlingshotState slingshotState) {
                slingshotState.Init(hand);
            }
        }

        public void UpdateSlingshot(int index, int count, float intensity) {
            if (state is SlingshotState slingshotState) {
                slingshotState.UpdateParams(index, count, intensity);
            }
        }
        public void IntoOneHandShield(int index, RagdollHand hand) {
            IntoState<ShieldState>();
            if (state is ShieldState shieldState) {
                shieldState.Init(index, hand);
            }
        }
        public void IntoLargeShield(int index) {
            IntoState<ShieldState>();
            if (state is ShieldState shieldState) {
                shieldState.Init(index, null, true);
            }
        }
        public bool IsLargeShield() => state is ShieldState shieldState && shieldState.isBigShield;
        public bool IsShieldOnHand(RagdollHand hand) => state is ShieldState shieldState && shieldState.hand == hand;
        public bool IsClawSwordOn(RagdollHand hand) => state is ClawSwordState clawSwordState && clawSwordState.hand == hand;
        public bool IsAtHand(RagdollHand hand) => state is HandState handState && handState.hand == hand;
        public int ClawSwordIndex() => state is ClawSwordState clawSwordState ? clawSwordState.index : -1;
        public void UpdateShield(Vector3 position, Quaternion rotation, int total) {
            if (state is ShieldState shieldState) {
                shieldState.UpdateShield(position, rotation, total);
            }
        }

        public void Decoration() {
            if (item.handlers.Any() || item.isTelekinesisGrabbed || item.isGripped) {
                IntoState<DefaultState>();
                return;
            }
            var head = Player.currentCreature.GetHead();
            var headPos = head.transform.position;
            var index = decorationIndex - 0.5f;
            transform.position = Vector3.Lerp(transform.position, headPos
                + (index * 0.2f * head.transform.up)
                + Math.Abs(index) * 0.2f * head.transform.right
                + head.transform.right * -0.5f, Time.deltaTime * 10);
            item.PointItemFlyRefAtTarget(transform.position - head.transform.position + head.transform.right * 0.2f, Time.deltaTime * 20, head.transform.up);
            return;
        }
        public void IntoDecoration(int index) {
            //SetState(DaggerState.Decoration);
            decorationIndex = index;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;
        }

        public void TrapAt(Vector3 position, bool activate) {
            IntoState<TrapState>();
            if (state is TrapState trapState) {
                trapState.UpdateTrap(position, activate);
            }
        }
        public void CreateJoint() {
            DeleteJoint();
            jointObj = jointObj ?? new GameObject().AddComponent<Rigidbody>();
            jointObj.isKinematic = true;
            jointObj.transform.position = item.GetMainHandle(Side.Left).transform.position;
            jointObj.transform.rotation = item.flyDirRef.rotation;
            joint = Utils.CreateTKJoint(jointObj, item.GetMainHandle(Side.Left), Side.Left);
        }

        public void UpdateJoint(Vector3 position, Quaternion rotation, float strengthMult = 1, float lerpFactor = 1) {
            if (jointObj == null)
                return;
            Utils.UpdateDriveStrengths(jointObj.GetComponent<ConfigurableJoint>(), strengthMult);
            jointObj.transform.position = Vector3.Lerp(jointObj.transform.position, position, lerpFactor);
            jointObj.transform.rotation = rotation;
        }

        public void DeleteJoint() {
            Destroy(joint);
        }
        public void SetPhysics(float gravity = 1, float mass = 1, float drag = -1, float angularDrag = -1)
            => item.mainCollisionHandler.SetPhysicModifier(this, 4, gravity, mass, drag, angularDrag);
        public void ResetPhysics() => item.mainCollisionHandler.RemovePhysicModifier(this);
        public IEnumerator Explosion() {
            yield return 0;
            if (!item.isPenetrating) {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;
            }
            var effectHolder = new GameObject();
            trailEffect.SetParent(null);
            effectHolder.transform.SetPositionAndRotation(transform.position, transform.rotation);
            Catalog.GetData<EffectData>("ExplosionFX").Spawn(effectHolder.transform).Play();
            yield return new WaitForSeconds(0.623f);
            Utils.Explosion(transform.position, 30, 2, true, true);
            Despawn();
        }
        public void IgnorePlayerCollisions() {
            if (item == null)
                return;
            if (item.ignoredRagdoll)
                return;
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
        }
        public void ResetPlayerCollisions() {
            if (item == null)
                return;
            if (!item.ignoredRagdoll)
                return;
            item.ResetRagdollCollision();
        }
        public void IgnoreDaggerCollisions() {
            foreach (var dagger in controller.daggers.Where(dagger => dagger.state.GetType() == state.GetType())) {
                try {
                    if (dagger == this || dagger == null)
                        continue;
                    if (ignoredDaggers.Contains(dagger))
                        continue;
                    ignoredDaggers.Add(dagger);
                    if ((dagger?.gameObject?.activeSelf ?? false) && (gameObject?.activeSelf ?? false))
                        foreach (Collider thisCollider in gameObject.GetComponentsInChildren<Collider>()) {
                            foreach (Collider otherCollider in dagger?.gameObject.GetComponentsInChildren<Collider>()) {
                                Physics.IgnoreCollision(thisCollider, otherCollider, true);
                            }
                        }
                } catch (NullReferenceException) {
                    Debug.LogWarning("Caught NRE when ignoring dagger collisions. This is a bug but shouldn't break anything.");
                }
            }
        }

        public void ResetDaggerCollisions() {
            if (controller?.daggers == null || gameObject == null)
                return;
            foreach (var dagger in controller.daggers) {
                try {
                    if (dagger == this || dagger == null)
                        continue;
                    if (!ignoredDaggers.Contains(dagger))
                        continue;
                    ignoredDaggers.Remove(dagger);
                    if ((dagger?.gameObject?.activeSelf ?? false) && (gameObject?.activeSelf ?? false))
                        foreach (Collider thisCollider in gameObject.GetComponentsInChildren<Collider>()) {
                            if (dagger?.gameObject?.GetComponentsInChildren<Collider>().Any() ?? false)
                                foreach (Collider otherCollider in dagger.gameObject.GetComponentsInChildren<Collider>()) {
                                    Physics.IgnoreCollision(thisCollider, otherCollider, false);
                                }
                        }
                } catch (NullReferenceException) {
                    Debug.LogWarning("Caught NRE when resetting dagger collisions. This is a bug but shouldn't break anything.");
                }
            }
        }

        public void IntoClawSword(RagdollHand hand, int index) {
            IntoState<ClawSwordState>();
            if (state is ClawSwordState clawSwordState) {
                clawSwordState.Init(hand, index);
            }
        }

        public void FlyTo(Vector3 position, Quaternion rotation) {
            IntoState<FlyState>();
            if (state is FlyState flyState) {
                flyState.UpdateTarget(position, rotation);
            }
        }

        public void Depenetrate() {
            if (item.isPenetrating) {
                item.collisionHandlers.ForEach(handler => handler.damagers.ForEach(damager => {
                    if (damager.data.penetrationAllowed) {
                        damager.UnPenetrateAll();
                        rb.AddForce(-damager.transform.forward, ForceMode.Impulse);
                        rb.AddForce(Vector3.up * 0.5f, ForceMode.Impulse);
                    }
                }));
            }
        }

        public void ThrowForce(Vector3 velocity, bool homing = false, float homingAngle = 40) {
            IntoState<DefaultState>();
            if (homing)
                velocity = HomingThrow(velocity, homingAngle);
            float modifier = 1;
            if (rb.mass < 1) {
                modifier *= rb.mass;
            } else {
                modifier *= rb.mass * Mathf.Clamp(rb.drag, 1, 2);
            }
            rb.AddForce(velocity * 7 * modifier, ForceMode.Impulse);
            lastNoOrbitTime = Time.time;
            item.Throw(1, Item.FlyDetection.Forced);
            IgnorePlayerCollisions();
            //StartCoroutine(ResetPlayerCollisionsAfter(2));
        }
        public IEnumerator ResetPlayerCollisionsAfter(float seconds) {
            yield return new WaitForSeconds(seconds);
            ResetPlayerCollisions();
        }
        public void Repel() {
            foreach (DaggerBehaviour dagger in controller.GetDaggersInState<OrbitState>()) {
                if (dagger != this) {
                    var distance = (transform.position - dagger.transform.position);
                    rb.AddForce(distance.normalized * (1 / Mathf.Pow(distance.magnitude * 2, 3).SafetyClamp()).SafetyClamp());
                }
            }
        }
    }

    public enum ImbueType {
        Fire, Gravity, Lightning
    }
}
