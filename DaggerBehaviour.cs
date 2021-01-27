using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;
using Random = UnityEngine.Random;

namespace DaggerBending {
    public class DaggerBehaviour : MonoBehaviour {
        const float ORBIT_RADIUS = 0.3f;
        const float ORBIT_VERTICAL_RANGE = 1;
        const float TARGET_DISTANCE_AHEAD = 0.8f;
        float lastNoOrbitTime;
        float orbitCooldown = 1.5f;
        float aimLockAngle = 30f;
        Item itemToBlock;
        Vector3 targetPos;
        public List<CollisionHandler.PhysicModifier> orgPhysicModifiers;
        public float slingshotIntensity;
        public int slingshotIndex;
        public int slingshotTotal;
        public Creature targetCreature;
        DaggerState state = DaggerState.None;
        DaggerController controller;
        public RagdollHand hand;
        public EffectData trailData;
        public EffectInstance trailEffect;
        PIDRigidbodyHelper pidController;
        public Item item;

        bool orgUseGravity = false;
        public Rigidbody rb;
        public void Start() {
            item = GetComponent<Item>();
            rb = item.rb;
            trailData = Catalog.GetData<EffectData>("DaggerTrailFX");
            controller = Player.currentCreature.mana.gameObject.GetComponent<DaggerController>();
            item.OnUngrabEvent += (handle, hand, throwing) => {
                var velocity = Player.local.transform.rotation * PlayerControl.GetHand(hand.side).GetHandVelocity();
                Debug.Log(velocity);
                if (throwing && velocity.magnitude > 3) {
                    lastNoOrbitTime = Time.time;
                    SetState(DaggerState.None);
                }
            };
            orgUseGravity = rb.useGravity;
            pidController = new PIDRigidbodyHelper(rb, 5, 1);
            //sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //sphere.transform.localScale = Vector3.one * 0.05f;
            //sphere.GetComponent<SphereCollider>().enabled = false;

        }

        public void Release() {
            SetState(DaggerState.None);

            if (item)
                item.ResetRagdollCollision();
            if (rb)
                rb.useGravity = orgUseGravity;
        }

        public void Throw() {
            Release();
            lastNoOrbitTime = Time.time;
            item.Throw(1, Item.FlyDetection.Forced);
        }

        public void Track(Creature creature) {
            Throw();
            targetCreature = creature;
            float modifier = 1;
            if (rb.mass < 1) {
                modifier *= rb.mass;
            } else {
                modifier *= rb.mass * Mathf.Clamp(rb.drag, 1, 2);
            }
            rb.AddForce(Vector3.up * 30f * modifier, ForceMode.Acceleration);
            SetState(DaggerState.Tracking);
        }

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
                if (group.data.imbueType != ColliderGroupData.ImbueType.None) {
                    group.imbue.Transfer(spell, group.imbue.maxEnergy);
                }
            });
        }

        public Imbue GetImbue() => item.imbues.FirstOrDefault(imbue => imbue.energy > 0);

        public bool CanSummon() {
            return GetState() == DaggerState.Orbit
                && item.holder == null
                && !item.isGripped
                && !item.isTelekinesisGrabbed
                && item.handlers.Count() == 0;
        }

        GameObject GetItemMeshObject() {
            var colliderGroups = item.colliderGroups.Where(
                group => group?
                    .imbueEmissionRenderer?
                    .GetComponent<MeshFilter>() != null);
            if (colliderGroups.Any() && colliderGroups.First().imbueEmissionRenderer is var emissionRenderer && emissionRenderer && emissionRenderer.GetComponent<MeshFilter>()) {
                return emissionRenderer.gameObject;
            } else if (item.GetComponentInChildren<MeshFilter>() is MeshFilter filter && filter != null) {
                return filter.gameObject;
            } else {
                return null;
                //vfx.vfx.SetTexture("PositionMap", null);
            }
        }
        Mesh GetItemMesh() {
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
            if (trailEffect != null)
                trailEffect.Despawn();
            item.Despawn();
        }

        public void Update() {
            if (!item)
                return;
            if (trailEffect == null) {
                trailEffect = trailData.Spawn(GetItemMeshObject().transform.position, GetItemMeshObject().transform.rotation);
                trailEffect.Play();
                trailEffect.SetIntensity(0);
                trailEffect.SetMesh(GetItemMesh());
            }
            trailEffect.effects.ForEach(effect => {
                effect.transform.position = GetItemMeshObject().transform.position;
                effect.transform.rotation = GetItemMeshObject().transform.rotation;
            });
            if (GetState() == DaggerState.None) {
                item.collisionHandlers.ForEach(handler => {
                    handler.RemovePhysicModifier(this);
                });
            } else {
                item.collisionHandlers.ForEach(handler => {
                    handler.SetPhysicModifier(this, 2, 1, 0.999f);
                });
            }
            switch (state) {
                case DaggerState.None:
                    trailEffect.SetIntensity(0);
                    break;
                case DaggerState.Orbit:
                    trailEffect.SetIntensity(1);
                    Orbit();
                    //Repel(Item.list.Where(i => i.itemId == "DaggerCommon"
                    //                        && i.GetComponent<DaggerBehaviour>() is DaggerBehaviour behavior
                    //                        && behavior != null
                    //                        && behavior.GetState() == DaggerState.Orbit)
                    //               .Select(dagger => dagger.GetComponent<DaggerBehaviour>())
                    //               .ToList());
                    break;
                case DaggerState.Tracking:
                    if (targetCreature != null) {
                        TrackCreature();
                    }
                    trailEffect.SetIntensity(0);
                    break;
                case DaggerState.Hand:
                    trailEffect.SetIntensity(1);
                    GoToHand();
                    break;
                case DaggerState.Slingshot:
                    OrbitHand();
                    trailEffect.SetIntensity(0);
                    break;
                case DaggerState.Block:
                    Block();
                    break;
                case DaggerState.Flying:
                    FlyToLoop();
                    break;
            }
        }

        public void SetState(DaggerState state) {
            this.state = state;
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
            Track(npcs.ElementAtOrDefault(Random.Range(0, npcs.Count())));
        }

        public DaggerState GetState() => state;

        public bool ShouldOrbit() => (Time.time - lastNoOrbitTime > orbitCooldown)
            && (GetState() == DaggerState.None);

        public bool CanOrbit() => 
            !item.isTelekinesisGrabbed
            && !item.isGripped
            && !item.handlers.Any()
            && item.holder == null;

        Transform GetPlayerChest() {
            return Player.currentCreature.ragdoll.GetPart(RagdollPart.Type.Torso).transform;
        }

        public Vector3 HomingThrow(Vector3 velocity) {
            var hits = Physics.SphereCastAll(item.transform.position, 10, velocity, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits) {
                Creature creature = hit.collider?.attachedRigidbody?.GetComponentInParent<Creature>();
                if (creature && creature != Player.currentCreature && creature.state != Creature.State.Dead) {
                    Vector3 vectorToCreatureTarget = creature.ragdoll.GetPart(RagdollPart.Type.Neck).transform.position - item.transform.position;
                    if (Vector3.Angle(velocity, vectorToCreatureTarget) < aimLockAngle + 3 * Vector3.Distance(item.transform.position, Player.currentCreature.transform.position)) {
                        item.rb.velocity = Vector3.zero;
                        velocity = vectorToCreatureTarget.normalized * velocity.magnitude;
                        break;
                    }
                }
            }
            return velocity;
        }

        public void GoToHand() {
            rb.useGravity = false;
            item.transform.position = Vector3.Lerp(item.transform.position, hand.PosAboveBackOfHand(), Time.deltaTime * 10);
            item.PointItemFlyRefAtTarget(hand.PointDir(), Time.deltaTime * 10, -hand.PalmDir());
        }

        public void OrbitHand() {
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);

            Vector3 offset = Quaternion.AngleAxis(360 / slingshotTotal * slingshotIndex, -Vector3.right) * (Vector3.up * (0.3f + 0.2f * (1 - slingshotIntensity)));
            item.rb.useGravity = false;
            pidController.UpdateVelocity(hand.transform.TransformPoint(offset), 10, 2);
            item.PointItemFlyRefAtTarget(
                controller.SlingshotDir(hand),
                Time.deltaTime * 20);
                hand.transform.TransformDirection(offset);
        }

        public void IntoOrbit() {
            SetState(DaggerState.Orbit);
        }

        public void BlockItem(Item toBlock) {
            itemToBlock = toBlock;
            state = DaggerState.Block;
            Throw();
        }

        public void Block() {
            if (!itemToBlock) {
                rb.velocity = Vector3.zero;
                item.transform.position = Vector3.Lerp(item.transform.position, itemToBlock.transform.position, Time.deltaTime * 20);
                if (item.mainCollisionHandler.isColliding) {
                    state = DaggerState.Orbit;
                    if (controller.itemsBeingBlocked.Contains(itemToBlock))
                        controller.itemsBeingBlocked.Remove(itemToBlock);
                }
                return;
            }
            state = DaggerState.Orbit;
            if (controller.itemsBeingBlocked.Contains(itemToBlock))
                controller.itemsBeingBlocked.Remove(itemToBlock);
        }

        public void FlyTo(Vector3 position) {
            targetPos = position;
            state = DaggerState.Flying;
        }

        public void FlyToLoop() {
            pidController.UpdateVelocity(targetPos, 3);
            rb.useGravity = false;
            item.Throw();
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

        public void TrackCreature() {
            rb.useGravity = false;
            item.Throw(1, Item.FlyDetection.Forced);
            if (targetCreature.state == Creature.State.Dead) {
                Release();
            }
            if (item.isPenetrating) {
                if (item.collisionHandlers
                        .Where(handler => handler.collisions
                            .Where(collision => collision.damageStruct.hitRagdollPart?.ragdoll.creature == targetCreature)
                            .Any())
                        .Any()) {
                    SetState(DaggerState.Orbit);
                    return;
                } else {
                    Depenetrate();
                }
            }
            if (Time.time - lastNoOrbitTime > 0.5f) {
                rb.AddForce(targetCreature.GetHead().transform.position - transform.position, ForceMode.Impulse);
                rb.velocity = Vector3.Project(rb.velocity, targetCreature.GetHead().transform.position - transform.position);
            }
            foreach (Collider collider in Physics.OverlapSphere(item.transform.position, 2, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
                if (collider.attachedRigidbody?.gameObject.GetComponent<Creature>() is Creature creature
                    && creature != null
                    && creature == targetCreature)
                    continue;
                var distance = collider.ClosestPoint(item.transform.position);
                rb.AddForce(distance.normalized * (1 / Mathf.Pow(distance.magnitude * 2, 3).SafetyClamp()).SafetyClamp());
            }
        }

        public void Deorbit() {
            SetState(DaggerState.None);
        }

        public void Orbit() {
            if (!item)
                return;
            if (item.handlers.Any() || item.isTelekinesisGrabbed || item.isGripped) {
                Deorbit();
                return;
            }
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll, new RagdollPart.Type[] {
                RagdollPart.Type.LeftHand,
                RagdollPart.Type.RightHand,
            });
            rb.useGravity = false;
            item.mainCollisionHandler.damagers.ForEach(damager => damager.UnPenetrateAll());
            var bodyAndHeight = new Vector3(
                Player.currentCreature.transform.position.x,
                Mathf.Clamp(
                    item.transform.position.y + UnityEngine.Random.Range(-ORBIT_VERTICAL_RANGE / 4, ORBIT_VERTICAL_RANGE / 4),
                    GetPlayerChest().transform.position.y - ORBIT_VERTICAL_RANGE / 2,
                    GetPlayerChest().transform.position.y + ORBIT_VERTICAL_RANGE / 2),
                Player.currentCreature.transform.position.z);
            var positionOnBody = bodyAndHeight + (item.transform.position - bodyAndHeight).normalized * ORBIT_RADIUS;
            var targetPosition = positionOnBody
                + Vector3.Project(rb.velocity, Vector3.Cross(Vector3.up, positionOnBody - GetPlayerChest().transform.position)).normalized
                * TARGET_DISTANCE_AHEAD;
            pidController.UpdateVelocity(targetPosition);
            rb.AddForce((positionOnBody - item.transform.position).normalized * 3);
            item.Throw();
        }

        public void ThrowForce(Vector3 velocity, bool homing = false) {
            if (homing)
                velocity = HomingThrow(velocity);
            float modifier = 1;
            if (rb.mass < 1) {
                modifier *= rb.mass;
            } else {
                modifier *= rb.mass * Mathf.Clamp(rb.drag, 1, 2);
            }
            rb.AddForce(velocity * 7 * modifier, ForceMode.Impulse);
            Throw();
        }

        public void Repel(List<DaggerBehaviour> daggers) {
            foreach (DaggerBehaviour dagger in daggers) {
                if (dagger != this) {
                    var distance = (transform.position - dagger.transform.position);
                    rb.AddForce(distance.normalized * (1 / Mathf.Pow(distance.magnitude * 2, 3).SafetyClamp()).SafetyClamp());
                }
            }
        }
    }

    public enum DaggerState {
        None,
        Orbit,
        Block,
        Tracking,
        Hand,
        Flying,
        Slingshot
    }

    public enum ImbueType {
        Fire, Gravity, Lightning
    }
}
