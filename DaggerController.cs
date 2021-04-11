/* TODO
 * Make Gather one-handed and in a cone
 * Better accuracy on Throw
 * Dagger flipping on b button
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace DaggerBending {
    public class DaggerController : MonoBehaviour {
        public List<DaggerBehaviour> daggers = new List<DaggerBehaviour>();
        public List<Item> itemsBeingBlocked = new List<Item>();
        public EffectData groupSummonEffectData;
        public float daggerImbueEffectCooldown;
        public float daggerImbueEffectDelay;
        public float lastPunchTimeLeft;
        public float lastPunchTimeRight;
        public float punchDelay = 0.1f;
        public bool wasLeftActive;
        public bool wasRightActive;
        public bool mapActive;
        public bool showDecoration = false;
        public float mapScale = 0.15f;
        public Dictionary<Side, bool> gripActive = new Dictionary<Side, bool>();
        public Dictionary<Side, bool> wasClaws = new Dictionary<Side, bool>();
        public Functionality state = null;
        public SingleHandFunctionality stateLeft = null;
        public SingleHandFunctionality stateRight = null;
        public float startHandDistance;
        public Vector3 startHandMidpoint;
        bool wereBothGripping;
        public ItemData daggerData;
        public GameObject mapHolder;
        public EffectInstance mapInstance;

        public void Start() {
            daggerData = Catalog.GetData<ItemData>("DaggerCommon");
            groupSummonEffectData = Catalog.GetData<EffectData>("GroupSummonFX");
            Player.local.head.cam.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals | DepthTextureMode.MotionVectors;
            Player.currentCreature.handRight.colliderGroup.collisionHandler.OnCollisionStartEvent += (CollisionInstance collision) => PunchHandler(Player.currentCreature.handRight, collision);
            Player.currentCreature.handLeft.colliderGroup.collisionHandler.OnCollisionStartEvent += (CollisionInstance collision) => PunchHandler(Player.currentCreature.handRight, collision);
            itemsBeingBlocked = new List<Item>();
        }

        public void PunchHandler(RagdollHand hand, CollisionInstance collision) {
            if (collision.targetColliderGroup.collisionHandler is CollisionHandler collisionHandler
                && collisionHandler.isRagdollPart
                && collision.impactVelocity.magnitude > 3.5f
                && hand.IsGripping()
                && hand.IsEmpty()) {
                var creature = collision.targetColliderGroup.collisionHandler.ragdollPart.ragdoll?.creature;
                if (creature && creature != Player.currentCreature && Time.time - (hand.side == Side.Left ? lastPunchTimeLeft : lastPunchTimeRight) > punchDelay) {
                    if (hand.side == Side.Left)
                        lastPunchTimeLeft = Time.time;
                    if (hand.side == Side.Right)
                        lastPunchTimeRight = Time.time;
                    GetDaggerClosestTo(creature.GetHead().transform.position).Track(creature);
                }
            }
        }

        public DaggerBehaviour GetDaggerClosestTo(Vector3 position) {
            return GetDaggersInState(DaggerState.Orbit).MinBy(dagger => Vector3.Distance(dagger.transform.position, position));
        }

        public SpellDagger GetSpell(Side side) {
            return Player.currentCreature.GetHand(side).caster?.spellInstance is SpellDagger dagger ? dagger : null;
        }

        public RagdollHand GetHand(Side side) => Player.currentCreature.GetHand(side);
        public bool BothHands(Func<RagdollHand, bool> func) => func(GetHand(Side.Left)) && func(GetHand(Side.Right));
        public bool OneHand(Func<RagdollHand, bool> func) => func(GetHand(Side.Left)) || func(GetHand(Side.Right));
        public RagdollHand HandWhere(Func<RagdollHand, bool> func) => func(GetHand(Side.Left)) ? GetHand(Side.Left) : (func(GetHand(Side.Right)) ? GetHand(Side.Right) : null);
        public SpellDagger SpellWhere(Func<SpellDagger, bool> func) => func(GetSpell(Side.Left)) ? GetSpell(Side.Left) : (func(GetSpell(Side.Right)) ? GetSpell(Side.Right) : null);
        public bool OneSpell(Func<SpellDagger, bool> func)
            => ((GetSpell(Side.Left) is SpellDagger left) && left != null && func(left))
            || ((GetSpell(Side.Right) is SpellDagger right) && right != null && func(right));
        public bool BothSpells(Func<SpellDagger, bool> func) => GetSpell(Side.Left) != null
            && GetSpell(Side.Right) != null
            && func(GetSpell(Side.Left))
            && func(GetSpell(Side.Right));
        public IEnumerable<RagdollHand> BothHands() {
            return new List<RagdollHand> { GetHand(Side.Left), GetHand(Side.Right) };
        }
        public void ForBothHands(Action<RagdollHand> func) {
            func(GetHand(Side.Left));
            func(GetHand(Side.Right));
        }

        public void ForBothSpells(Action<SpellDagger> func) {
            if (GetSpell(Side.Left) != null) func(GetSpell(Side.Left));
            if (GetSpell(Side.Right) != null) func(GetSpell(Side.Right));
        }

        public T HandFunc<T>(Func<RagdollHand, RagdollHand, T> func) => func(GetHand(Side.Left), GetHand(Side.Right));

        public float HandVelocityTowards(RagdollHand hand, Vector3 position) {
            return HandVelocityInDirection(hand, hand.transform.position - position);
        }

        public float HandVelocityInDirection(RagdollHand hand, Vector3 direction) {
            return Vector3.Dot(hand.rb.velocity, direction);
        }

        public float HandDistance() {
            return Vector3.Distance(GetHand(Side.Left).transform.position, GetHand(Side.Right).transform.position);
        }
        public Vector3 HandMidpoint() => (GetHand(Side.Left).Palm() + GetHand(Side.Right).Palm()) / 2;
        public Vector3 HandDirection() => (GetHand(Side.Left).PalmDir() + GetHand(Side.Right).PalmDir()) / 2;
        public IEnumerable<DaggerBehaviour> GetDaggersInState(DaggerState state) => daggers.Where(dagger => dagger.GetState() == state);
        public bool FacingPosition(Vector3 origin, Vector3 direction, Vector3 target) => FacingDirection(direction, target - origin);
        public bool FacingDirection(Vector3 sourceDirection, Vector3 targetDirection) => Vector3.Angle(sourceDirection, targetDirection) < 50;
        public void SpawnDagger(Action<DaggerBehaviour> callback) {
            daggerData.SpawnAsync(dagger => {
                DaggerBehaviour behaviour = dagger.gameObject.AddComponent<DaggerBehaviour>();
                daggers.Add(behaviour);
                behaviour.Start();
                callback(behaviour);
            });
        }

        public Vector3 SlingshotDir(RagdollHand hand) {
            return ((hand.transform.position - hand.otherHand.transform.position).normalized + hand.PointDir().normalized / 2).normalized;
        }

        public List<DaggerBehaviour> SpawnDaggersInArea(Vector3 position, float range, int count, float velocity, Vector3 fxVelocity = default) {
            var spawnedDaggers = new List<DaggerBehaviour>();
            var effect = groupSummonEffectData.Spawn(position, Quaternion.LookRotation(Vector3.up, Vector3.forward));
            if (effect.effects.First() is EffectVfx vfx)
                vfx.vfx.SetVector3("Force", Quaternion.LookRotation(Vector3.up, Vector3.forward) * fxVelocity);
            effect.Play();

            for (int i = 0; i < count; i++) {
                SpawnDagger(dagger => {
                    dagger.transform.position = new Vector3(
                        position.x + UnityEngine.Random.Range(-range, range),
                        position.y,
                        position.z + UnityEngine.Random.Range(-range, range));
                    dagger.rb.AddForce(Vector3.up * 3f * velocity, ForceMode.Impulse);
                    dagger.item.Throw();
                    dagger.SetState(DaggerState.Orbit);
                    dagger.item.PointItemFlyRefAtTarget(Vector3.up, 1, (position - dagger.transform.position).normalized);
                    Array types = Enum.GetValues(typeof(ImbueType));
                    dagger.Imbue((ImbueType)types.GetValue(UnityEngine.Random.Range(0, types.Length)));
                    spawnedDaggers.Add(dagger);
                });
            }
            return spawnedDaggers;
        }

        public void SendDaggersToOrbit(Func<DaggerBehaviour, bool> filter, bool force = false) {
            foreach (DaggerBehaviour dagger in Item.list.Where(i => i.itemId == "DaggerCommon")
                                                        .SelectNotNull(item => force
                                                                        ? item.gameObject.GetOrAddComponent<DaggerBehaviour>()
                                                                        : item.gameObject.GetComponent<DaggerBehaviour>())
                                                        .Where(behaviour => filter(behaviour)
                                                                         && behaviour.CanOrbit()
                                                                         && (force || behaviour.ShouldOrbit()))) {
                dagger.ThrowForce(Vector3.up * 0.01f);
                dagger.IntoOrbit();
            };
        }

        public void DetectDualHandGestures() {
            var orgState = state;
            var orgStateRight = stateRight;
            var bothGripAndCast = BothSpells(spell => spell.isCasting) && wereBothGripping;
            var handMidpointDifference = (HandMidpoint() - startHandMidpoint);
            var avgHandPalmDir = (GetHand(Side.Left).PalmDir() + GetHand(Side.Right).PalmDir()) / 2;
            if (SlingshotFunctionality.Test(this)) {
                state = new SlingshotFunctionality();
            } else if (bothGripAndCast && PullInFunctionality.Test(this)) {
                state = new PullInFunctionality();
            } else if (bothGripAndCast && PushForwardFunctionality.Test(this, handMidpointDifference)) {
                state = new PushForwardFunctionality();
            } else if (bothGripAndCast && PullOutFunctionality.Test(this)) {
                state = new PullOutFunctionality();
            } else if (bothGripAndCast && PushDownFunctionality.Test(this, handMidpointDifference, avgHandPalmDir)) {
                state = new PushDownFunctionality();
            }
            if (state != orgState) {
                orgState?.Exit(this);
                state?.Enter(this);
            }
        }

        public void DetectSingleHandGestures(Side side, ref SingleHandFunctionality sideState) {
            if (SingleHandShield.Test(this, GetHand(side))) {
                sideState = new SingleHandShield();
            } else if (TrapFunctionality.Test(this, GetHand(side))) {
                sideState = new TrapFunctionality();
            } else if (SummonFunctionality.Test(this, GetHand(side))) {
                sideState = new SummonFunctionality();
            }
            if (sideState != null) {
                sideState.Enter(this, GetHand(side));
            }
        }

        public void ShowDaggerMap() {
            mapHolder = new GameObject();
            mapHolder.transform.position = HandMidpoint();
            mapInstance = Catalog.GetData<EffectData>("DaggerMap").Spawn(mapHolder.transform);
            mapInstance.Play();
        }

        public void HideDaggerMap() {
            mapInstance.Despawn();
            Destroy(mapHolder, 1);
        }

        private Color PosToColor(Vector3 pos, float alpha = 1) => new Color(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f, alpha);
        public Tuple<uint, Texture2D> GetDaggerMapTexture() {
            var items = Item.list.Where(item => item.itemId == "DaggerCommon"
                                             && Vector3.Distance(
                                                 Player.currentCreature.transform.position,
                                                 item.transform.position) < 127 * mapScale);
            uint numDaggers = (uint)items.Count();
            var texture = new Texture2D(64, 64, TextureFormat.ARGB32, false) {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                wrapMode = TextureWrapMode.Repeat,
            };
            var i = 0;
            foreach (Item item in items) {
                var relativePos = item.transform.position - Player.currentCreature.transform.position;
                texture.SetPixel(i % 64, 63 - Mathf.FloorToInt(i / 64), PosToColor(relativePos * mapScale, (item.GetComponent<DaggerBehaviour>() == null) ? 1 : 0));
                i++;
            }
            texture.Apply();
            return new Tuple<uint, Texture2D>(numDaggers, texture);
        }
        public Tuple<uint, Texture2D> GetEnemyMapTexture() {
            var creatures = Creature.list.Where(creature => !creature.isPlayer
                                                         && creature.state != Creature.State.Dead
                                                         && Vector3.Distance(
                                                             Player.currentCreature.transform.position,
                                                             creature.transform.position) < 127 * mapScale);
            uint numEnemies = (uint)creatures.Count();
            var texture = new Texture2D(64, 64, TextureFormat.ARGB32, false) {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                wrapMode = TextureWrapMode.Repeat,
            };
            var i = 0;
            foreach (Creature creature in creatures) {
                var relativePos = creature.transform.position - Player.currentCreature.transform.position;
                texture.SetPixel(i % 64, 63 - Mathf.FloorToInt(i / 64), PosToColor(relativePos * mapScale, 1));
                i++;
            }
            texture.Apply();
            return new Tuple<uint, Texture2D>(numEnemies, texture);
        }
        public void UpdateDaggerMap() {
            if (!mapInstance?.effects.Any() ?? false)
                return;
            mapHolder.transform.position = HandMidpoint();
            var vfx = (mapInstance.effects.First() as EffectVfx).vfx;
            var (daggerCount, daggerTexture) = GetDaggerMapTexture();
            vfx.SetTexture("DaggerPositions", daggerTexture);
            vfx.SetUInt("DaggerCount", daggerCount);
            var (enemyCount, enemyTexture) = GetEnemyMapTexture();
            vfx.SetTexture("EnemyPositions", enemyTexture);
            vfx.SetUInt("EnemyCount", enemyCount);
        }

        public void ImbueRandomDagger(SpellCastCharge imbueSpell, Transform imbueEffectSource = null) {
            var orbitingDaggers = GetDaggersInState(DaggerState.Orbit);
            var dagger = orbitingDaggers
                .Where(d => d.GetImbue()?.spellCastBase?.id != imbueSpell.id || d.GetImbue()?.energy < d.GetImbue()?.maxEnergy * 0.8f)
                .OrderBy(d => {
                    if (!d.item.imbues.Any() || d.item.imbues.Sum(imbue => imbue.energy) == 0)
                        return 1;
                    if (d.GetImbue()?.spellCastBase?.id == imbueSpell.id)
                        return 0;
                    return 2;
                })
                .FirstOrDefault();
            if (dagger) {
                if (imbueEffectSource != null) {
                    if (Time.time - daggerImbueEffectCooldown > daggerImbueEffectDelay) {
                        daggerImbueEffectCooldown = Time.time;
                        var imbueObject = new GameObject();
                        imbueObject.transform.position = imbueEffectSource.transform.position;
                        imbueObject.transform.rotation = imbueEffectSource.transform.rotation;
                        imbueObject.layer = LayerMask.NameToLayer("TransparentFX");
                        var imbueInstance = imbueSpell.imbueBladeEffectData.Spawn(imbueObject.transform.position, imbueObject.transform.rotation, imbueObject.transform);
                        imbueInstance.Play();
                        var castInstance = Catalog.GetData<EffectData>(imbueSpell.chargeEffectId).Spawn(imbueObject.transform.position, imbueObject.transform.rotation, imbueObject.transform);
                        castInstance.Play();
                        imbueObject.AddComponent<ImbueEffectBehaviour>().Init(dagger, imbueSpell, imbueInstance, castInstance);
                    }
                }
            }
        }

        public void CheckSideActive(Side side, ref bool wasActive, ref SingleHandFunctionality sideState) {
            if ((GetHand(side)?.IsGripping() ?? false) && (GetSpell(side)?.isCasting ?? false)) {
                if (state == null && sideState == null)
                    DetectSingleHandGestures(side, ref sideState);
                wasActive = true;
            } else {
                if (wasActive) {
                    sideState?.Exit(this);
                    sideState = null;
                }
                wasActive = false;
            }
        }

        public void Update() {
            CheckSideActive(Side.Left, ref wasLeftActive, ref stateLeft);
            CheckSideActive(Side.Right, ref wasRightActive, ref stateRight);
            ForBothHands(hand => {
                if (hand.IsGripping() && hand.IsEmpty()) {
                    if (gripActive[hand.side]) {
                        if (wasClaws[hand.side] == hand.playerHand.controlHand.usePressed) {
                            Catalog.GetData<EffectData>(wasClaws[hand.side] ? "ClawsToSword" : "SwordToClaws").Spawn(hand.transform).Play();
                            wasClaws[hand.side] = !hand.playerHand.controlHand.usePressed;
                        }
                    } else {
                        gripActive[hand.side] = true;
                        wasClaws[hand.side] = !hand.playerHand.controlHand.usePressed;
                    }
                    int i = 0;
                    foreach (var dagger in GetDaggersInState(DaggerState.Orbit).OrderBy(item => Vector3.Distance(hand.transform.position, item.transform.position)).Take(3)) {
                        if (!GetDaggersInState(DaggerState.Fist).Where(item => item.hand == hand).Select(item => item.index).Contains(i)) {
                            dagger.GoToFist(hand, i);
                        }
                        i++;
                    }
                } else {
                    gripActive[hand.side] = false;
                    foreach (var dagger in GetDaggersInState(DaggerState.Fist).Where(dagger => dagger.hand == hand)) {
                        dagger.IntoOrbit();
                    }
                }
            });
            if (BothHands(hand => Vector3.Angle(hand.PalmDir(),
                hand.otherHand.palmCollider.transform.position - hand.palmCollider.transform.position) < 30)
                && HandDistance() > 0.1f && HandDistance() < 0.3f) {
                if (!mapActive) {
                    ShowDaggerMap();
                    mapActive = true;
                }
                UpdateDaggerMap();
            } else {
                if (mapActive) {
                    mapActive = false;
                    HideDaggerMap();
                }
            }

            if (BothHands(hand => hand.IsGripping())) {
                if (!wereBothGripping) {
                    wereBothGripping = true;
                    startHandDistance = HandDistance();
                    startHandMidpoint = HandMidpoint();
                }
            } else {
                wereBothGripping = false;
            }

            if (state != null && !wereBothGripping) {
                state?.Exit(this);
                state = null;
            }

            foreach (Item item in Item.list
                .Where(i => Vector3.Distance(i.transform.position, Player.currentCreature.GetHead().transform.position) < 4
                         && !itemsBeingBlocked.Contains(i)
                         && i.itemId == "MagicProjectile"
                         && i.lastHandler?.creature != Player.currentCreature)) {
                itemsBeingBlocked.Add(item);
                item.OnDespawnEvent += () => { if (itemsBeingBlocked.Contains(item)) itemsBeingBlocked.Remove(item); };
                GetDaggerClosestTo(item.transform.position).BlockItem(item);
            }

            // Send anything nearby into orbit
            SendDaggersToOrbit(dagger => Vector3.Distance(dagger.transform.position, Player.currentCreature.transform.position) < 3);

            Vector3 handAngularVelocity = GetHand(Side.Right).LocalAngularVelocity();
            daggers = Item.list.Where(i => i.itemId == "DaggerCommon"
                                        && i.GetComponent<DaggerBehaviour>() != null)
                               .Select(dagger => dagger.GetComponent<DaggerBehaviour>())
                               .ToList();
            while (daggers.Count() > SpellDagger.maxDaggerCount) {
                var toDespawn = daggers.Where(
                    dagger => !dagger.item.IsVisible()
                    && dagger.CanDespawn()).FirstOrDefault();
                if (toDespawn != null) {
                    daggers.Remove(toDespawn);
                    toDespawn.Despawn();
                } else {
                    break;
                }
            }


            if (showDecoration) {
                var orbiting = GetDaggersInState(DaggerState.Orbit);
                var decoration = GetDaggersInState(DaggerState.Decoration);
                if (orbiting.Count() + decoration.Count() >= 10) {
                    var i = decoration.Count() - 1;
                    foreach (var dagger in orbiting.Take(Mathf.Clamp(4 - decoration.Count(), 0, 4))) {
                        dagger.IntoDecoration(i++);
                    }
                } else {
                    foreach (var dagger in decoration)
                        dagger.IntoOrbit();
                }
            }

            if (state == null)
                DetectDualHandGestures();

            state?.Update(this);
            stateLeft?.Update(this);
            stateRight?.Update(this);
        }
    }

    public abstract class Functionality {
        public float enterTime;
        public virtual void Enter(DaggerController controller) {
            enterTime = Time.time;
        }
        public virtual void Exit(DaggerController controller) { }
        public virtual void Update(DaggerController controller) { }
    }

    public abstract class SingleHandFunctionality {
        public RagdollHand hand;
        public float enterTime;
        public virtual void Enter(DaggerController controller, RagdollHand hand) {
            enterTime = Time.time;
            this.hand = hand;
        }
        public virtual void Exit(DaggerController controller) { }
        public virtual void Update(DaggerController controller) { }
    }

    public class PullInFunctionality : Functionality {
        public static bool Test(DaggerController controller) =>
            controller.startHandDistance > 0.2f
            && controller.BothHands(hand => controller.FacingPosition(hand.Palm(), hand.PalmDir(), hand.otherHand.Palm()))
            && controller.HandDistance() < controller.startHandDistance - 0.1f;

        public override void Enter(DaggerController controller) {
            base.Enter(controller);
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Orbit)) {
                dagger.FlyTo(controller.HandMidpoint() + (Player.currentCreature.handLeft.PointDir() + Player.currentCreature.handRight.PointDir()).normalized * 2);
            }
        }
        public override void Update(DaggerController controller) {
            base.Update(controller);
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Flying)) {
                Vector3 pos = Utils.UniqueVector(dagger.item.gameObject, -controller.HandDistance() * 2, +controller.HandDistance() * 2);
                Vector3 normal = Utils.UniqueVector(dagger.item.gameObject, -controller.HandDistance() * 2, +controller.HandDistance() * 2, 1);
                Vector3 attractionCenter = (Player.currentCreature.handLeft.PointDir() + Player.currentCreature.handRight.PointDir()).normalized * 2;
                dagger.FlyTo(
                    controller.HandMidpoint()
                    + attractionCenter
                    + pos.Rotated(Quaternion.AngleAxis(Time.time * 40, normal)));
            }
        }
        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            var handVelocity = controller.BothHands().Select(hand => hand.rb.velocity).Aggregate((a, b) => a + b) / 2;
            if (handVelocity.magnitude > 2) {
                foreach (var dagger in controller.GetDaggersInState(DaggerState.Flying)) {
                    dagger.Release();
                    dagger.ThrowForce(handVelocity, true);
                }
            } else {
                foreach (var dagger in controller.GetDaggersInState(DaggerState.Flying)) {
                    dagger.Release();
                    dagger.IntoOrbit();
                }
            }
        }
    }

    public class SingleHandShield : SingleHandFunctionality {
        public override void Enter(DaggerController controller, RagdollHand hand) {
            base.Enter(controller, hand);
            Debug.Log($"Entering shield for side {hand.side}");
        }
        public static bool Test(DaggerController controller, RagdollHand hand) {
            return controller.HandVelocityInDirection(hand, -hand.PalmDir()) > 1.2f;
        }
        public override void Update(DaggerController controller) {
            base.Update(controller);
            var count = controller.GetDaggersInState(DaggerState.Shield).Where(dagger => dagger.hand == hand).Count();
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Orbit).Take(7 - count)) {
                dagger.IntoShield(count++, hand);
            }
            count = controller.GetDaggersInState(DaggerState.Shield).Where(dagger => dagger.hand == hand).Count();
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Shield).Where(dagger => dagger.hand == hand)) {
                dagger.targetPos = hand.Palm() + hand.PalmDir() * -0.3f + hand.PointDir() * 0.1f;
                dagger.targetRot = Quaternion.LookRotation(hand.PalmDir(), -hand.PointDir());
                dagger.total = count;
            }
            foreach (Item item in Item.list.Where(item => item.itemId == "MagicProjectile")) {
                item.GetComponentInChildren<SphereCollider>().radius = 0.2f;
            }
        }
        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Shield).Where(dagger => dagger.hand == hand)) {
                dagger.IntoOrbit();
            }
        }
    }

    public class TrapFunctionality : SingleHandFunctionality {
        public DaggerBehaviour dagger;
        public Vector3? hoverPoint;

        public static bool Test(DaggerController controller, RagdollHand hand) {
            Vector3 handAngularVelocity = hand.LocalAngularVelocity();
            return hand.caster.isFiring && hand.IsGripping() && (hand.side == Side.Right ? handAngularVelocity.z < -7 : handAngularVelocity.z > 7) && handAngularVelocity.MostlyZ();
        }

        public override void Enter(DaggerController controller, RagdollHand hand) {
            base.Enter(controller, hand);
        }

        public override void Update(DaggerController controller) {
            base.Update(controller);
            if (!dagger) {
                dagger = controller.GetDaggerClosestTo(hand.transform.position);
                if (!dagger)
                    return;
            }
            if (Physics.Raycast(hand.transform.position + hand.PointDir() * 0.3f, hand.PointDir(), out RaycastHit hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
                hoverPoint = hit.point + Vector3.up * 1;
                dagger.TrapAt(hoverPoint ?? default, false);
            }
        }

        public override void Exit(DaggerController controller) {
            if (dagger && hoverPoint != null) {
                dagger.TrapAt(hoverPoint ?? default, true);
            }
            base.Exit(controller);
        }
    }

    public class PushForwardFunctionality : Functionality {
        public static bool Test(DaggerController controller, Vector3 handMidpointDifference) =>
            handMidpointDifference.magnitude > 0.2f
            && controller.FacingDirection(handMidpointDifference, Player.currentCreature.GetHead().transform.forward)
            && controller.BothHands(hand => controller.FacingDirection(hand.PalmDir(), hand.otherHand.PalmDir()));

        public override void Enter(DaggerController controller) {
            base.Enter(controller);
        }
        public override void Update(DaggerController controller) {
            base.Update(controller);
            var count = controller.GetDaggersInState(DaggerState.Shield).Count();
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Orbit).Take(17 - count)) {
                dagger.IntoShield(count++);
            }
            count = controller.GetDaggersInState(DaggerState.Shield).Count();
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Shield)) {
                dagger.targetPos = controller.HandMidpoint() + controller.HandDirection() * 0.4f;
                dagger.targetRot = Quaternion.LookRotation(controller.HandDirection());
                dagger.total = count;
            }
        }
        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Shield)) {
                dagger.IntoOrbit();
            }
        }
    }

    public class PullOutFunctionality : Functionality {
        public float delay = 0.3f;
        public static bool Test(DaggerController controller) => controller.HandDistance() > controller.startHandDistance + 0.1f;
        public override void Enter(DaggerController controller) {
            base.Enter(controller);
        }

        public override void Update(DaggerController controller) {
            base.Update(controller);
            if (Time.time - enterTime > delay) {
                enterTime = Time.time;
                controller.daggers
                    .Where(d => d.GetState() == DaggerState.Orbit)
                    .ElementAtOrDefault(UnityEngine.Random.Range(0, controller.daggers.Count()))?
                    .TrackRandomTarget(Utils.ConeCastCreature(
                        controller.HandMidpoint(), 20, (controller.GetHand(Side.Left).PointDir() + controller.GetHand(Side.Right).PointDir()) / 2, Mathf.Infinity, 60, live: false));
            }
        }
    }

    public class PushDownFunctionality : Functionality {
        public override void Enter(DaggerController controller) {
            base.Enter(controller);
            var midpoint = new GameObject();
            midpoint.transform.position = controller.HandMidpoint();
            Catalog.GetData<EffectData>("DaggerPullFX").Spawn(midpoint.transform).Play();
        }
        public static bool Test(DaggerController controller, Vector3 handMidpointDifference, Vector3 handAvgPalmDir) =>
            handMidpointDifference.magnitude > 0.2f
            && controller.FacingDirection(handMidpointDifference, Vector3.down)
            && controller.FacingDirection(handAvgPalmDir, Vector3.down);

        public override void Update(DaggerController controller) {
            base.Update(controller);
            controller.SendDaggersToOrbit(dagger => dagger.GetState() != DaggerState.Hand, true);
        }
    }

    public class SlingshotFunctionality : Functionality {
        RagdollHand holdHand;
        RagdollHand drawHand;
        EffectData effectData = Catalog.GetData<EffectData>("SlingshotFX");
        EffectInstance effect;
        float lastFired = 0;
        float fireInterval = 0.25f;

        public static bool Test(DaggerController controller) => controller.OneSpell(spell => spell.handle && spell.handle.handlers.Any());

        public override void Enter(DaggerController controller) {
            base.Enter(controller);
            holdHand = controller.SpellWhere(spell => spell != null && (spell.handle?.handlers?.Any() ?? false))?.spellCaster?.ragdollHand;
            var source = holdHand.transform;
            effect = effectData.Spawn(source.position, source.rotation);
            effect.Play();
            effect.SetIntensity(0.0f);
            drawHand = holdHand.otherHand;
        }

        public override void Update(DaggerController controller) {
            base.Update(controller);
            if (!holdHand || !drawHand) {
                return;
            }
            int numDaggersNeeded = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(holdHand.transform.position, drawHand.transform.position) * 1.3f * 6), 0, 6);
            float intensity = Mathf.Clamp(Vector3.Distance(holdHand.transform.position, drawHand.transform.position) * 1.3f, 0, 1);
            effect.SetIntensity(intensity);
            holdHand.playerHand.controlHand.HapticShort(Mathf.Lerp(intensity, 0.05f, 0.3f));
            effect.effects.ForEach(effect => {
                effect.transform.position = holdHand.transform.position;
                effect.transform.rotation = Quaternion.LookRotation(holdHand.PointDir());
            });
            int numDaggersOwned = controller.daggers.Where(dagger => dagger.GetState() == DaggerState.Slingshot).Count();
            if (numDaggersNeeded > numDaggersOwned) {
                int numDaggersToGather = numDaggersNeeded - numDaggersOwned;
                foreach (var dagger in controller.daggers
                    .Where(dagger => dagger.GetState() == DaggerState.Orbit)
                    .OrderBy(dagger => Vector3.Distance(dagger.transform.position, holdHand.transform.position))
                    .Take(numDaggersToGather)) {
                    dagger.SetState(DaggerState.Slingshot);
                    dagger.hand = holdHand;
                    Catalog.GetData<EffectData>("DaggerSnickFX").Spawn(dagger.transform).Play();
                }
            } else if (numDaggersNeeded < numDaggersOwned) {
                int numDaggersToRelease = numDaggersOwned - numDaggersNeeded;
                foreach (var dagger in controller.daggers
                    .Where(dagger => dagger.GetState() == DaggerState.Slingshot)
                    .Take(numDaggersToRelease)) {
                    dagger.SetState(DaggerState.Orbit);
                }
            }
            IEnumerable<DaggerBehaviour> slingshotDaggers = controller.GetDaggersInState(DaggerState.Slingshot).OrderBy(dagger => dagger.item.GetInstanceID());
            int i = 0;
            int count = slingshotDaggers.Count();
            if (drawHand.playerHand.controlHand.usePressed) {
                if (Time.time - lastFired > fireInterval) {
                    lastFired = Time.time;
                    var daggerToThrow = slingshotDaggers
                        .OrderBy(dagger => dagger.index)
                        .FirstOrDefault();
                    if (daggerToThrow) {
                        // remove daggerToThrow from slingshotDaggers iterable
                        slingshotDaggers = slingshotDaggers.Where(dagger => dagger != daggerToThrow);
                        daggerToThrow.ThrowForce(controller.SlingshotDir(holdHand) * 2 * (1 + controller.HandDistance() * 4), true, 20);
                        Catalog.GetData<EffectData>("SlingshotFire").Spawn(daggerToThrow.transform).Play();
                    }
                }
            } else {
                lastFired = 0;
            }
            foreach (var dagger in slingshotDaggers) {
                dagger.slingshotIntensity = intensity;
                dagger.index = i++;
                dagger.total = count;
            }
        }

        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            //Catalog.GetData<EffectData>("SlingshotFire").Spawn(holdHand.transform).Play();
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Slingshot)) {
                dagger.ThrowForce(controller.SlingshotDir(holdHand) * (1 + controller.HandDistance() * 4), true);
            }
            effect.SetIntensity(0.0f);
            effect.End();
        }
    }
    public class SummonFunctionality : SingleHandFunctionality {
        List<DaggerBehaviour> summonedDaggers;

        public static bool Test(DaggerController controller, RagdollHand hand) =>
            controller.FacingDirection(hand.PalmDir(), Vector3.up)
            && controller.HandVelocityInDirection(hand, Vector3.up) > 1.4f;

        public override void Enter(DaggerController controller, RagdollHand hand) {
            base.Enter(controller, hand);
            var spawnLocation = hand.transform.position
                              + hand.PointDir() * 3;
            spawnLocation.y = Mathf.Max(spawnLocation.y, Player.currentCreature.GetPart(RagdollPart.Type.Torso).transform.position.y);
            summonedDaggers = controller.SpawnDaggersInArea(spawnLocation, 1, 6, controller.HandVelocityInDirection(hand, Vector3.up), hand.rb.velocity);
        }

        public override void Update(DaggerController controller) {
            base.Update(controller);
            //summonedDaggers = summonedDaggers.Where(dagger => dagger?.item?.gameObject == null).ToList();
            foreach (var dagger in summonedDaggers.NotNull()) {
                if (dagger.item?.gameObject == null)
                    continue;
                Vector3 attractionCenter = hand.PointDir() * 3;
                Vector3 pos = Utils.UniqueVector(dagger.item.gameObject, -1, 1);
                Vector3 normal = Utils.UniqueVector(dagger.item.gameObject, -1, 1, 1);
                dagger.FlyTo(hand.transform.position
                    + attractionCenter
                    + pos.Rotated(Quaternion.AngleAxis(Time.time * 40, normal)));
            }
        }

        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            summonedDaggers.ForEach(dagger => {
                if (hand.rb.velocity.magnitude > 2) {
                    dagger.Release();
                    dagger.ThrowForce(hand.rb.velocity, true);
                } else {
                    dagger.Release();
                    dagger.IntoOrbit();
                }
            });
        }
    }

    public class ImbueEffectBehaviour : MonoBehaviour {
        DaggerBehaviour dagger;
        Rigidbody rb;
        EffectInstance imbue;
        EffectInstance charge;
        SpellCastCharge spell;
        //PIDRigidbodyHelper pidController;
        float spawnTime;
        public void Start() {
            rb = gameObject.GetOrAddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            spawnTime = Time.time;
        }
        public void Init(DaggerBehaviour dagger, SpellCastCharge imbueSpell, EffectInstance imbue, EffectInstance charge) {
            this.dagger = dagger;
            this.imbue = imbue;
            this.charge = charge;
            spell = imbueSpell;
        }

        public void Update() {
            if (!dagger)
                return;
            transform.position = Vector3.Lerp(transform.position, dagger.transform.position, Mathf.Clamp(Time.time - spawnTime, 0, 1));
            charge.SetIntensity(1 - Mathf.Clamp(Vector3.Distance(transform.position, dagger.transform.position), 0, 1));
            if (Vector3.Distance(transform.position, dagger.transform.position) < 0.3f) {
                dagger.Imbue(spell);
                imbue.SetParent(dagger.transform);
                imbue.End();
                charge.SetParent(dagger.transform);
                charge.End();
                Destroy(this);
            }
        }
    }
}
