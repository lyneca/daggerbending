/* TODO
 * Make Gather one-handed
 * Make Summon one-handed
 * Better accuracy on Throw
 * Dagger flipping on b button
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;
using UnityEngine.AddressableAssets;

namespace DaggerBending {
    public class DaggerController : MonoBehaviour {
        public List<DaggerBehaviour> daggers = new List<DaggerBehaviour>();
        public List<Item> itemsBeingBlocked = new List<Item>();
        public float daggerImbueEffectCooldown;
        public float daggerImbueEffectDelay;
        public float lastPunchTimeLeft;
        public float lastPunchTimeRight;
        public float punchDelay = 0.1f;
        public bool leftHandHasSummoned;
        public bool rightHandHasSummoned;
        public List<DaggerBehaviour> leftSummonedDaggers;
        public List<DaggerBehaviour> rightSummonedDaggers;
        Functionality state = null;
        public float startHandDistance;
        public Vector3 startHandMidpoint;
        bool wereBothGripping;
        public ItemPhysic daggerData;

        public void Start() {
            daggerData = Catalog.GetData<ItemPhysic>("DaggerCommon");
            Player.local.head.cam.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals | DepthTextureMode.MotionVectors;
            Player.currentCreature.handRight.colliderGroup.collisionHandler.OnCollisionStartEvent += (ref CollisionStruct collision) => PunchHandler(Player.currentCreature.handRight, collision);
            Player.currentCreature.handLeft.colliderGroup.collisionHandler.OnCollisionStartEvent += (ref CollisionStruct collision) => PunchHandler(Player.currentCreature.handRight, collision);
            itemsBeingBlocked = new List<Item>();
            leftSummonedDaggers = new List<DaggerBehaviour>();
            rightSummonedDaggers = new List<DaggerBehaviour>();
        }

        public void PunchHandler(RagdollHand hand, CollisionStruct collision) {
            var collisionHandler = collision.targetColliderGroup.collisionHandler;
            if (collisionHandler && collisionHandler.isRagdollPart && collision.impactVelocity.magnitude > 3) {
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
            return daggers.MinBy(dagger => Vector3.Distance(dagger.transform.position, position));
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
            func(GetSpell(Side.Left));
            func(GetSpell(Side.Right));
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

        public Vector3 HandMidpoint() => (GetHand(Side.Left).transform.position + GetHand(Side.Right).transform.position) / 2;

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

        public List<DaggerBehaviour> SpawnDaggersInArea(Vector3 position, float range, int count, float velocity) {
            var spawnedDaggers = new List<DaggerBehaviour>();
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
            foreach (DaggerBehaviour dagger in Item.list.Where(i => i.itemId == "DaggerCommon"
                                                                 && (force || i.GetComponent<DaggerBehaviour>() is DaggerBehaviour behaviour && behaviour != null))
                                                        .Select(item => force
                                                                        ? item.gameObject.GetOrAddComponent<DaggerBehaviour>()
                                                                        : item.gameObject.GetComponent<DaggerBehaviour>())
                                                        .Where(behaviour => filter(behaviour)
                                                                         && behaviour.CanOrbit()
                                                                         && (force || behaviour.ShouldOrbit()))) {
                dagger.IntoOrbit();
            };
        }

        public void DetectInitialGestures() {
            var orgState = state;
            if (!GetSpell(Side.Left).isCasting || !GetSpell(Side.Left).IsGripping()) {
                leftHandHasSummoned = false;
                leftSummonedDaggers.ForEach(dagger => {
                    if (GetHand(Side.Left).rb.velocity.magnitude > 2)
                        dagger.ThrowForce(GetHand(Side.Left).rb.velocity, true);
                    else
                        dagger.IntoOrbit();
                });
                leftSummonedDaggers.Clear();
            }
            if (!GetSpell(Side.Right).isCasting || !GetSpell(Side.Right).IsGripping()) {
                rightHandHasSummoned = false;
                rightSummonedDaggers.ForEach(dagger => {
                    if (GetHand(Side.Right).rb.velocity.magnitude > 2)
                        dagger.ThrowForce(GetHand(Side.Right).rb.velocity, true);
                    else
                        dagger.IntoOrbit();
                });
                rightSummonedDaggers.Clear();
            }
            var bothGripAndCast = BothSpells(spell => spell.isCasting) && wereBothGripping;
            var notSingleSummoning = bothGripAndCast && !(leftHandHasSummoned || rightHandHasSummoned);
            var handMidpointDifference = (HandMidpoint() - startHandMidpoint);
            if (notSingleSummoning && SlingshotFunctionality.Test(this))  {
                state = new SlingshotFunctionality();
            } else if (notSingleSummoning && PullInFunctionality.Test(this)) {
                state = new PullInFunctionality();
            } else if (notSingleSummoning && PullOutFunctionality.Test(this)) {
                state = new PullOutFunctionality();
            } else if (notSingleSummoning && PushForwardFunctionality.Test(this, handMidpointDifference)) {
                state = new PushForwardFunctionality();
            } else {
                CheckSingleHandSummon();
            }
            if (state != orgState) {
                orgState?.Exit(this);
                state?.Enter(this);
            }
        }

        void CheckSingleHandSummon() {
                ForBothSpells(spell => {
                    var isLeft = (spell.spellCaster.ragdollHand.side == Side.Left);
                    if (spell.isCasting && spell.IsGripping())
                        if (!(isLeft ? leftHandHasSummoned : rightHandHasSummoned)) {
                            if (FacingDirection(spell.spellCaster.ragdollHand.PalmDir(), Vector3.up)
                         && HandVelocityInDirection(spell.spellCaster.ragdollHand, Vector3.up) > 1.4f) {
                                if (isLeft)
                                    leftHandHasSummoned = true;
                                else
                                    rightHandHasSummoned = true;
                                var spawnLocation = spell.spellCaster.ragdollHand.transform.position
                                                  + spell.spellCaster.ragdollHand.PointDir() * 3;
                                spawnLocation.y = Mathf.Max(spawnLocation.y, Player.currentCreature.GetPart(RagdollPart.Type.Torso).transform.position.y);
                                if (isLeft)
                                    leftSummonedDaggers = SpawnDaggersInArea(spawnLocation, 1, 6, HandVelocityInDirection(spell.spellCaster.ragdollHand, Vector3.up));
                                else
                                    rightSummonedDaggers = SpawnDaggersInArea(spawnLocation, 1, 6, HandVelocityInDirection(spell.spellCaster.ragdollHand, Vector3.up));
                            }
                        } else {
                            var summonedDaggers = isLeft ? leftSummonedDaggers : rightSummonedDaggers;
                            foreach (var dagger in summonedDaggers.NotNull()) {
                                dagger.FlyTo(spell.spellCaster.ragdollHand.transform.position
                                    + spell.spellCaster.ragdollHand.PointDir() * 3
                                    + Utils.UniqueVector(dagger.item.gameObject, -1, 1));
                            }
                        }
                });
        }

        public void ImbueRandomDagger(SpellCastCharge imbueSpell, Transform imbueEffectSource = null) {
            var orbitingDaggers = GetDaggersInState(DaggerState.Orbit);
            Debug.Log(string.Join(", ", orbitingDaggers
                .Where(d => d.GetImbue()?.spellCastBase?.id != imbueSpell.id || d.GetImbue()?.energy < d.GetImbue()?.maxEnergy * 0.8f)
                .OrderBy(d => {
                    if (!d.item.imbues.Any() || d.item.imbues.Sum(imbue => imbue.energy) == 0)
                        return 1;
                    if (d.GetImbue()?.spellCastBase?.id == imbueSpell.id)
                        return 0;
                    return 2;
                }).Select(d => {
                    if (!d.item.imbues.Any() || d.item.imbues.Sum(imbue => imbue.energy) == 0)
                        return $"{d.GetImbue()?.energy} 1";
                    if (d.GetImbue()?.spellCastBase?.id == imbueSpell.id)
                        return $"{d.GetImbue()?.energy} 0";
                    return $"{d.GetImbue()?.energy} 2";
                })));
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

        public void Update() {
            if (BothHands(hand => hand.IsGripping())) {
                if (!wereBothGripping) {
                    wereBothGripping = true;
                    startHandDistance = HandDistance();
                    startHandMidpoint = HandMidpoint();
                }
            } else {
                wereBothGripping = false;
            }

            if (!wereBothGripping) {
                state?.Exit(this);
                state = null;
            }

            foreach (Item item in Item.list
                .Where(i => Vector3.Distance(i.transform.position, Player.currentCreature.GetHead().transform.position) < 4
                         && !itemsBeingBlocked.Contains(i)
                         && i.itemId == "MagicProjectile"
                         && i.lastHandler?.creature != Player.currentCreature)) {
                Debug.Log($"Blocking {item}");
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
                    && !leftSummonedDaggers.Contains(dagger)
                    && !rightSummonedDaggers.Contains(dagger)).FirstOrDefault();
                if (toDespawn != null) {
                    daggers.Remove(toDespawn);
                    toDespawn.Despawn();
                } else {
                    break;
                }
            }
            if (state == null) {
                DetectInitialGestures();
            }
            state?.Update(this);
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

    public class PullInFunctionality : Functionality {
        public static bool Test(DaggerController controller) =>
            controller.startHandDistance > 0.2f
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
                dagger.FlyTo(
                    controller.HandMidpoint()
                    + (Player.currentCreature.handLeft.PointDir() + Player.currentCreature.handRight.PointDir()).normalized * 2
                    + Utils.UniqueVector(dagger.item.gameObject, -controller.HandDistance() * 2, + controller.HandDistance() * 2));
            }
        }
        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            var handVelocity = controller.BothHands().Select(hand => hand.rb.velocity).Aggregate((a, b) => a + b) / 2;
            if (handVelocity.magnitude > 2) {
                foreach (var dagger in controller.GetDaggersInState(DaggerState.Flying)) {
                    dagger.ThrowForce(handVelocity, true);
                }
            } else {
                foreach (var dagger in controller.GetDaggersInState(DaggerState.Flying)) {
                    dagger.IntoOrbit();
                }
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

    public class PushForwardFunctionality : Functionality {
        public static bool Test(DaggerController controller, Vector3 handMidpointDifference) =>
            controller.HandDistance() < 0.2f
            && handMidpointDifference.magnitude > 0.3f
            && controller.FacingDirection(handMidpointDifference, controller.startHandMidpoint - Player.currentCreature.GetTorso().transform.position);
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
                }
            } else if (numDaggersNeeded < numDaggersOwned) {
                int numDaggersToRelease = numDaggersOwned - numDaggersNeeded;
                foreach (var dagger in controller.daggers
                    .Where(dagger => dagger.GetState() == DaggerState.Slingshot)
                    .Take(numDaggersToRelease)) {
                    dagger.SetState(DaggerState.Orbit);
                }
            }
            var slingshotDaggers = controller.GetDaggersInState(DaggerState.Slingshot).OrderBy(dagger => dagger.item.GetInstanceID());
            int i = 0;
            int count = slingshotDaggers.Count();
            foreach (var dagger in slingshotDaggers) {
                dagger.slingshotIntensity = intensity;
                dagger.slingshotIndex = i++;
                dagger.slingshotTotal = count;
            }
        }

        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            foreach (var dagger in controller.GetDaggersInState(DaggerState.Slingshot)) {
                dagger.ThrowForce(holdHand.PointDir() * 2 * (0.5f + controller.HandDistance() * 2), true);
            }
            effect.SetIntensity(0.0f);
            effect.End();
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
            transform.position = Vector3.Lerp(transform.position, dagger.transform.position, Time.deltaTime * 2f * (Mathf.Clamp((Time.time - spawnTime) * 3f, 0, 2) + 1));
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
