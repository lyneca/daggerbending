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
    public class DaggerBehaviour : MonoBehaviour {
        const float ORBIT_RADIUS = 0.3f;
        const float ORBIT_VERTICAL_RANGE = 1;
        const float TARGET_DISTANCE_AHEAD = 0.8f;
        float lastNoOrbitTime;
        float orbitCooldown = 1.5f;
        bool justSpawned = true;
        bool singleSlingshot;
        bool startedTracking;
        public float decorationIndex = -1;
        Item itemToBlock;
        public Vector3 trapPoint;
        public Vector3 targetPos;
        public Quaternion? targetRot;
        public List<CollisionHandler.PhysicModifier> orgPhysicModifiers;
        public float slingshotIntensity;
        public int index;
        public int total;
        public Rigidbody jointObj;
        public ConfigurableJoint joint;
        public Creature targetCreature;
        DaggerState state = DaggerState.None;
        DaggerState lastState = DaggerState.None;
        DaggerController controller;
        public RagdollHand hand;
        public EffectData trailData;
        public EffectInstance trailEffect;
        public EffectData trapData;
        public EffectInstance trapEffect;
        PIDRigidbodyHelper pidController;
        public Item item;

        bool orgUseGravity = false;
        public Rigidbody rb;
        public void Start() {
            item = GetComponent<Item>();
            rb = item.rb;
            //item.mainCollisionHandler.damagers.ForEach(damager => damager.penetrationExitOnMaxDepth = true);
            trailData = Catalog.GetData<EffectData>("DaggerTrailFX");
            trapData = Catalog.GetData<EffectData>("DaggerFloatFX");
            controller = Player.currentCreature.mana.gameObject.GetComponent<DaggerController>();
            hand = hand ?? controller.GetHand(Side.Right);
            Catalog.GetData<EffectData>("SlingshotGrabPoint").Spawn(transform).Play();
            item.OnSnapEvent += holder => {
                item.lastHandler = null;
                hand.ClearTouch();
                if (item.transform.parent.GetComponentsInChildren<Item>() is Item[] items && items.Count() > 1) {
                    var slotToUse = holder.slots.FirstOrDefault(slot => slot.GetComponentsInChildren<Item>().Count() == 0);
                    if (slotToUse == null)
                        return;
                    var holderPoint = item.GetHolderPoint(holder.data.targetAnchor);
                    item.transform.MoveAlign(holderPoint.anchor, slotToUse.transform, slotToUse.transform);
                }
            };
            item.OnUnSnapEvent += holder => {
                if (holder.GetComponentInParent<Item>() is Item holderItem) {
                    item.IgnoreObjectCollision(holderItem);
                    Invoke("ResetObjectCollision", 2f);
                }
            };
            item.OnUngrabEvent += (handle, hand, throwing) => {
            var velocity = Player.local.transform.rotation * PlayerControl.GetHand(hand.side).GetHandVelocity();
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

        public void PlaySpawnEffect() {
            var spawnFX = Catalog.GetData<EffectData>("DaggerSpawnFX").Spawn(item.transform);
            spawnFX.SetMesh(GetItemMesh());
            spawnFX.SetSource(GetItemMeshObject().transform);
            spawnFX.Play();
        }

        public void Release() {
            SetState(DaggerState.None);
            rb.isKinematic = false;
            DeleteJoint();

            if (item) {
                item.ResetRagdollCollision();
                item.ResetObjectCollision();
            }
            if (rb)
                rb.useGravity = orgUseGravity;
        }

        public bool Held() => item.mainHandler != null;

        public void Throw() {
            rb.isKinematic = false;
            Release();
            lastNoOrbitTime = Time.time;
            item.Throw(1, Item.FlyDetection.Forced);
        }

        public void Track(Creature creature) {
            Throw();
            targetCreature = creature;
            startedTracking = false;
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
                if (group.data.modifiers.Where(mod => mod.imbueType != ColliderGroupData.ImbueType.None
                                                   && spell.imbueAllowMetal || mod.imbueType != ColliderGroupData.ImbueType.Metal)
                                        .Any()) {
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
            var bounds = item.renderers.First().bounds;
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
            if (lastState != GetState()) {
                if (state != DaggerState.Orbit && state != DaggerState.Shield && state != DaggerState.Flying && state != DaggerState.Fist && state != DaggerState.Pouch && state != DaggerState.Slingshot) {
                    ResetDaggerCollisions();
                } else if (lastState == DaggerState.Decoration || lastState == DaggerState.Hand) {
                    rb.isKinematic = false;
                }
                if (state == DaggerState.Pouch) {
                    item.GetMainHandle(Side.Left).SetTouch(false);
                    item.GetMainHandle(Side.Right).SetTouch(false);
                } else if (lastState == DaggerState.Pouch) {
                    item.GetMainHandle(Side.Left).SetTouch(true);
                    item.GetMainHandle(Side.Right).SetTouch(true);
                }
            }
            lastState = GetState();
            if (GetState() == DaggerState.None) {
                item.mainCollisionHandler.RemovePhysicModifier(this);
            } else if (GetState() != DaggerState.Fist) {
                item.mainCollisionHandler.SetPhysicModifier(this, 2, 1, 0.999f);
            }
            if (trapEffect == null) {
                // This is needed (instead of parenting) so that the trap effect doesn't spin with the
                //  dagger when it's launched.
                trapEffect = trapData.Spawn(transform.position, transform.rotation);
            }
            switch (state) {
                case DaggerState.None:
                    trailEffect.SetIntensity(0);
                    singleSlingshot = false;
                    break;
                case DaggerState.Orbit:
                    trailEffect.SetIntensity(1);
                    singleSlingshot = false;
                    Orbit();
                    break;
                case DaggerState.Decoration:
                    Decoration();
                    break;
                case DaggerState.Tracking:
                    if (targetCreature != null) {
                        TrackCreature();
                    }
                    trailEffect.SetIntensity(0);
                    break;
                case DaggerState.Pouch:
                    trailEffect.SetIntensity(0);
                    Pouch();
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
                case DaggerState.Trap:
                    Trap();
                    break;
                case DaggerState.TrapPlan:
                    TrapPlan();
                    break;
                case DaggerState.Shield:
                    Shield();
                    break;
                case DaggerState.Fist:
                    Fist();
                    break;
            }
            justSpawned = false;
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

        public bool CanDespawn() => !justSpawned && CanOrbit() && (GetState() == DaggerState.None || GetState() == DaggerState.Orbit);
        public bool CanOrbit() => item != null
             && !item.isTelekinesisGrabbed
             && state != DaggerState.Trap
             && !item.isGripped
             && (!item?.handlers?.Any() ?? false)
             && item?.holder == null;

        Transform GetPlayerChest() {
            return Player.currentCreature.ragdoll.GetPart(RagdollPart.Type.Torso).transform;
        }

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

        public void Pouch() {
            rb.useGravity = false;
            var pouch = controller.GetNonFullPouches().MinBy(quiver => Vector3.Distance(quiver.transform.position, transform.position));
            if (item.handlers.Any() || item.isTelekinesisGrabbed || item.isGripped) {
                SetState(DaggerState.None);
                return;
            }
            if (!pouch) {
                IntoOrbit();
                return;
            }
            var distance = Vector3.Distance(transform.position, pouch.transform.position);
            UpdateJoint(
                pouch.transform.position + pouch.transform.up * Mathf.Clamp(distance / 2, 0, 1),
                Quaternion.LookRotation(pouch.transform.position - transform.position, pouch.transform.right));
            //pidController.UpdateVelocity(, 3, 2);
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
            Depenetrate();
            //if (!item.isFlying) {
            //    item.Throw(1, Item.FlyDetection.Forced);
            //}
            if (Vector3.Distance(transform.position, pouch.transform.position) < 0.2f) {
                Release();
                item.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                pouch.RefreshChildAndParentHolder();
                pouch.SetTouch(false);
                pouch.Snap(item);
                pouch.SetTouch(true);
                var containingSlot = pouch.slots.FirstOrDefault(slot => slot.GetComponentsInChildren<Item>().Contains(item));
            }
        }

        public void GoToPouch() {
            CreateJoint();
            SetState(DaggerState.Pouch);
        }

        public void GoToHand() {
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;
            if (item.mainHandler != null) {
                rb.isKinematic = false;
                singleSlingshot = true;
            } else {
                item.transform.position = Vector3.Lerp(item.transform.position, hand.PosAboveBackOfHand(), Time.deltaTime * 10);
                item.PointItemFlyRefAtTarget(hand.PointDir(), Time.deltaTime * 10, -hand.PalmDir());
            }
            if (singleSlingshot && item.mainHandler == null) {
                singleSlingshot = false;
                rb.isKinematic = false;
                ThrowForce(hand.PosAboveBackOfHand() - item.transform.position * 5, true);
            }
        }

        public void IntoHand(RagdollHand hand) {
            DeleteJoint();
            item.IgnoreRagdollCollision(hand.ragdoll, hand.side == Side.Left ? RagdollPart.Type.LeftHand : RagdollPart.Type.RightHand);
            SetState(DaggerState.Hand);
            this.hand = hand;
            rb.velocity = Vector3.zero;
        }

        public void OrbitHand() {
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
            if (total == 0)
                return;
            Vector3 offset = Quaternion.AngleAxis(360 / total * index, -Vector3.right) * (Vector3.up * (0.3f + 0.2f * (1 - slingshotIntensity)));
            item.rb.useGravity = false;
            UpdateJoint(hand.transform.TransformPoint(offset), Quaternion.LookRotation(controller.SlingshotDir(hand), transform.position - hand.transform.position));
            hand.transform.TransformDirection(offset);
        }

        public void IntoSlingshot(RagdollHand hand) {
            this.hand = hand;
            CreateJoint();
            SetState(DaggerState.Slingshot);
            IgnoreDaggerCollisions();
            Catalog.GetData<EffectData>("DaggerSnickFX").Spawn(transform).Play();
        }

        public void Decoration() {
            if (item.handlers.Any() || item.isTelekinesisGrabbed || item.isGripped) {
                Release();
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
            SetState(DaggerState.Decoration);
            decorationIndex = index;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;
        }
        public void IntoOrbit() {
            transform.parent = null;
            if (joint)
                DeleteJoint();
            item.mainCollisionHandler.RemovePhysicModifier(this);
            rb.isKinematic = false;
            SetState(DaggerState.Orbit);
            IgnoreDaggerCollisions();
        }
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
        public Vector2 GetShieldOffset() {
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
        public void Shield() {
            var offset = GetShieldOffset() * 0.2f;
            UpdateJoint(targetPos + (targetRot ?? default) * new Vector3(offset.x * 0.5f, offset.y, 0),
                Quaternion.LookRotation((targetRot ?? default) * Vector3.down, (targetRot ?? default) * Vector3.right), 10);
            if (!item.isFlying) {
                item.Throw(1, Item.FlyDetection.Forced);
                item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
            }
        }
        public void IntoShield(int index, RagdollHand hand = null) {
            this.index = index;
            this.hand = hand ?? controller.GetHand(Side.Right);
            state = DaggerState.Shield;
            Depenetrate();
            IgnoreDaggerCollisions();
            item.Throw(1, Item.FlyDetection.Forced);
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
            CreateJoint();
        }

        public void BlockItem(Item toBlock) {
            itemToBlock = toBlock;
            state = DaggerState.Block;
            Throw();
        }

        public void TrapAt(Vector3 point, bool activate) {
            trapPoint = point;
            if (activate) {
                rb.velocity = Vector3.zero;
                SetState(DaggerState.Trap);
                trapEffect.effects.ForEach(effect => {
                    effect.transform.position = transform.position;
                    effect.transform.rotation = transform.rotation;
                });
                trapEffect.Play();
                trapEffect.SetIntensity(1);
            } else {
                SetState(DaggerState.TrapPlan);
            }
        }

        public void TrapPlan() {
            transform.position = Vector3.Lerp(transform.position, trapPoint, Time.deltaTime * 10);
            item.PointItemFlyRefAtTarget(Vector3.down, 10);
            rb.useGravity = false;
        }

        public void Trap() {
            pidController.UpdateVelocity(trapPoint, 1, 2);
            item.PointItemFlyRefAtTarget(Vector3.down, 10);
            rb.useGravity = false;
            trapEffect.effects.ForEach(effect => {
                effect.transform.position = transform.position;
                effect.transform.rotation = transform.rotation;
            });
            if (Held()) {
                rb.useGravity = true;
                SetState(DaggerState.None);
                trapEffect.SetIntensity(0);
                trapEffect.Stop();
                return;
            }
            var nearbyCreatures = Creature.list.Where(creature => Vector3.Distance(creature.transform.position, transform.position) < 3 && creature != Player.currentCreature && creature.state != Creature.State.Dead);
            if (nearbyCreatures.Any()) {
                trapEffect.SetIntensity(0);
                trapEffect.End();
                trapEffect = null;
                Track(nearbyCreatures.First());
            }
        }

        public void CreateJoint() {
            DeleteJoint();
            jointObj = jointObj ?? new GameObject().AddComponent<Rigidbody>();
            jointObj.isKinematic = true;
            jointObj.transform.position = item.GetMainHandle(hand?.side ?? Side.Left).transform.position;
            jointObj.transform.rotation = item.flyDirRef.rotation;
            joint = Utils.CreateTKJoint(jointObj, item.GetMainHandle(hand.side), hand.side);
        }

        public void UpdateJoint(Vector3 position, Quaternion rotation, float strengthMult = 1) {
            if (!jointObj)
                return;
            Utils.UpdateDriveStrengths(jointObj.GetComponent<ConfigurableJoint>(), strengthMult);
            jointObj.transform.position = position;
            jointObj.transform.rotation = rotation;
        }

        public void DeleteJoint() {
            Destroy(joint);
        }

        public void IgnoreDaggerCollisions() {
            foreach (var dagger in controller.daggers) {
                if (dagger == this)
                    continue;
                foreach (Collider thisCollider in this.gameObject.GetComponentsInChildren<Collider>()) {
                    foreach (Collider otherCollider in dagger.gameObject.GetComponentsInChildren<Collider>()) {
                        Physics.IgnoreCollision(thisCollider, otherCollider, true);
                    }
                }
            }
        }

        public void ResetDaggerCollisions() {
            if (controller?.daggers == null || gameObject == null)
                return;
            foreach (var dagger in controller.daggers) {
                if (dagger == this)
                    continue;
                if (gameObject.GetComponentInChildren<Collider>() && dagger.GetComponentInChildren<Collider>())
                    foreach (Collider thisCollider in gameObject.GetComponentsInChildren<Collider>()) {
                        foreach (Collider otherCollider in dagger.gameObject.GetComponentsInChildren<Collider>()) {
                            Physics.IgnoreCollision(thisCollider, otherCollider, false);
                        }
                    }
            }
        }

        public void Fist() {
            Vector3 position;
            Quaternion rotation;
            if (hand.playerHand.controlHand.usePressed) {
                var fistIndex = (hand.side == Side.Right) ? index : (2 - index);
                position = hand.Palm() + hand.ThumbDir() * 0.15f + hand.ThumbDir() * (0.4f * (fistIndex + 0.3f * hand.rb.velocity.magnitude));
                rotation = Quaternion.LookRotation(hand.ThumbDir(), -hand.PalmDir());
            } else {
                var angleTarget = Mathf.Clamp(60 - hand.rb.velocity.magnitude * 15, 15, 60);
                var angle = index * angleTarget - angleTarget;
                Vector3 offset = Quaternion.AngleAxis(angle, -Vector3.right)
                    * (Vector3.forward * (0.2f + 0.3f * hand.rb.velocity.magnitude / 2))
                    + (-Vector3.right - Vector3.forward) * 0.3f * hand.rb.velocity.magnitude / 2;
                position = hand.transform.TransformPoint(offset);
                rotation = Quaternion.LookRotation(hand.PointDir(), -hand.PalmDir());
            }
            UpdateJoint(position, rotation, 10);
            item.Throw(1, Item.FlyDetection.Forced);
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
        }

        public void GoToFist(RagdollHand hand, int index) {
            this.index = index;
            this.hand = hand;
            state = DaggerState.Fist;
            item.mainCollisionHandler.SetPhysicModifier(this, 3, 0.0f, 0.5f, 0.5f);
            CreateJoint();
            IgnoreDaggerCollisions();
        }

        public void Block() {
            if (!itemToBlock) {
                rb.velocity = Vector3.zero;
                item.transform.position = Vector3.Lerp(item.transform.position, itemToBlock.transform.position, Time.deltaTime * 20);
                if (item.mainCollisionHandler.isColliding) {
                    IntoOrbit();
                    if (controller.itemsBeingBlocked.Contains(itemToBlock))
                        controller.itemsBeingBlocked.Remove(itemToBlock);
                }
                return;
            }
            IntoOrbit();
            if (controller.itemsBeingBlocked.Contains(itemToBlock))
                controller.itemsBeingBlocked.Remove(itemToBlock);
        }

        public void FlyTo(Vector3 position) {
            targetPos = position;
            targetRot = null;
            IgnoreDaggerCollisions();
            state = DaggerState.Flying;
        }

        public void FlyTo(Vector3 position, Quaternion rotation) {
            targetPos = position;
            targetRot = rotation;
            IgnoreDaggerCollisions();
            state = DaggerState.Flying;
        }

        public void FlyToLoop() {
            pidController.UpdateVelocity(targetPos, 3);
            if (targetRot != null) {
                pidController.UpdateTorque(targetRot ?? default);
            }
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
                    IntoOrbit();
                    return;
                } else {
                    Depenetrate();
                }
            }
            if (Time.time - lastNoOrbitTime > 0.5f) {
                var target = targetCreature.GetHead().transform;
                if (!startedTracking) {
                    SpawnThrowFX(target.position - transform.position);
                    startedTracking = true;
                }
                item.PointItemFlyRefAtTarget(target.position - item.transform.position, 1);
                rb.AddForce((target.position - item.transform.position).normalized * 2f, ForceMode.Impulse);
                // Reorient force vector to target head
                if (Vector3.Distance(targetCreature.GetHead().transform.position, transform.position) > 0.2f)
                    rb.velocity = Vector3.Project(rb.velocity, targetCreature.GetHead().transform.position - transform.position);
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
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
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

        public void ThrowForce(Vector3 velocity, bool homing = false, float homingAngle = 40) {
            if (homing)
                velocity = HomingThrow(velocity, homingAngle);
            float modifier = 1;
            if (rb.mass < 1) {
                modifier *= rb.mass;
            } else {
                modifier *= rb.mass * Mathf.Clamp(rb.drag, 1, 2);
            }
            rb.isKinematic = false;
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
        TrapPlan,
        Trap,
        Slingshot,
        Fist,
        Decoration,
        Pouch,
        Shield
    }

    public enum ImbueType {
        Fire, Gravity, Lightning
    }
}
