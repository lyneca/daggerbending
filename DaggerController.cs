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
    using States;
    using System.Collections;

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
        public string itemId;
        public Dictionary<Side, bool> gripActive = new Dictionary<Side, bool>();
        public Dictionary<Side, bool> wasClaws = new Dictionary<Side, bool>();
        public Dictionary<RagdollHand, bool> handHasFlipped = new Dictionary<RagdollHand, bool>();
        public Dictionary<RagdollHand, bool> handFlipping = new Dictionary<RagdollHand, bool>();
        public Functionality state = null;
        public SingleHandFunctionality stateLeft = null;
        public SingleHandFunctionality stateRight = null;
        public float startHandDistance;
        public Vector3 startHandMidpoint;
        bool wereBothGripping;
        public ItemData daggerData;
        public GameObject mapHolder;
        public EffectInstance mapInstance;
        float lastUnSnap = 0;
        readonly float unSnapDelay = 0.00f;
        public bool debug = false;
        public GameObject debugObj;
        public bool daggersOrbitWhenIdle = true;

        public void Start() {
            daggerData = Catalog.GetData<ItemData>(itemId);
            groupSummonEffectData = Catalog.GetData<EffectData>("GroupSummonFX");
            handFlipping[GetHand(Side.Left)] = false;
            handFlipping[GetHand(Side.Right)] = false;
            handHasFlipped[GetHand(Side.Left)] = false;
            handHasFlipped[GetHand(Side.Right)] = false;
            //Player.local.head.cam.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals | DepthTextureMode.MotionVectors;
            if (SpellDagger.allowPunchDagger) {
                Player.currentCreature.handRight.colliderGroup.collisionHandler.OnCollisionStartEvent += (CollisionInstance collision) => PunchHandler(Player.currentCreature.handRight, collision);
                Player.currentCreature.handLeft.colliderGroup.collisionHandler.OnCollisionStartEvent += (CollisionInstance collision) => PunchHandler(Player.currentCreature.handRight, collision);
            }
            itemsBeingBlocked = new List<Item>();
        }

        public void PunchHandler(RagdollHand hand, CollisionInstance collision) {
            if (collision.targetColliderGroup?.collisionHandler is CollisionHandler collisionHandler
                && collisionHandler.isRagdollPart
                && collision.impactVelocity.magnitude > 3.5f
                && DaggerAvailable()
                && hand.IsEmpty()) {
                var creature = collision.targetColliderGroup.collisionHandler.ragdollPart.ragdoll?.creature;
                if (creature && creature != Player.currentCreature && Time.time - (hand.side == Side.Left ? lastPunchTimeLeft : lastPunchTimeRight) > punchDelay) {
                    if (hand.side == Side.Left)
                        lastPunchTimeLeft = Time.time;
                    if (hand.side == Side.Right)
                        lastPunchTimeRight = Time.time;
                    if (DaggerAvailable())
                        GetFreeDaggerClosestTo(creature.GetHead().transform.position).TrackCreature(creature);
                }
            }
        }
        public DaggerBehaviour GetOrbitingDaggerClosestTo(Vector3 position) {
            return GetDaggersInState<OrbitState>().MinBy(dagger => Vector3.Distance(dagger.transform.position, position));
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
        public Vector3 AverageHandPointDir() => (GetHand(Side.Left).PointDir() + GetHand(Side.Right).PointDir()).normalized;
        public Vector3 AverageHandVelocity() => (GetHand(Side.Left).Velocity() + GetHand(Side.Right).Velocity());
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
        public void PlayThrowClip(RagdollHand hand) =>
            hand.PlayHapticClipOver(
                new AnimationCurve(
                    new Keyframe(0f, 0),
                    new Keyframe(0.2f, 0, 0, 10),
                    new Keyframe(0.3f, 1, 10, -10),
                    new Keyframe(0.4f, 0, -10, 0),
                    new Keyframe(0.6f, 0, 0, 10),
                    new Keyframe(0.7f, 1, 10, -10),
                    new Keyframe(0.8f, 0, -10, 0),
                    new Keyframe(1, 0)), 0.3f);

        public float HandVelocityTowards(RagdollHand hand, Vector3 position) {
            return HandVelocityInDirection(hand, hand.transform.position - position);
        }

        public float HandVelocityInDirection(RagdollHand hand, Vector3 direction) {
            return Vector3.Dot(hand.Velocity(), direction);
        }

        public float HandDistance() {
            return Vector3.Distance(GetHand(Side.Left).transform.position, GetHand(Side.Right).transform.position);
        }
        public Vector3 HandMidpoint() => (GetHand(Side.Left).Palm() + GetHand(Side.Right).Palm()) / 2;
        public Vector3 HandDirection() => (GetHand(Side.Left).PalmDir() + GetHand(Side.Right).PalmDir()) / 2;
        public IEnumerable<DaggerBehaviour> GetDaggersInState<T>() where T: DaggerState => daggers.Where(dagger => dagger.item.holder == null && dagger.GetState() is T);
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

        public List<DaggerBehaviour> SpawnDaggersInArea(Vector3 position, float range, int count, float velocity, Vector3 fxVelocity = default, bool imbue = false) {
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
                    dagger.IntoState<OrbitState>();
                    dagger.item.PointItemFlyRefAtTarget(Vector3.up, 1, (position - dagger.transform.position).normalized);
                    if (imbue) {
                        Array types = Enum.GetValues(typeof(ImbueType));
                        dagger.Imbue((ImbueType)types.GetValue(UnityEngine.Random.Range(0, types.Length)));
                    }
                    spawnedDaggers.Add(dagger);
                });
            }
            return spawnedDaggers;
        }

        public void SendDaggersToOrbit(Func<DaggerBehaviour, bool> filter, bool force = false, Func<DaggerBehaviour, float> order = null) {
            foreach (DaggerBehaviour dagger in Item.list.Where(i => i.itemId == itemId)
                                                        .SelectNotNull(item => force
                                                                        ? item.gameObject.GetOrAddComponent<DaggerBehaviour>()
                                                                        : item.gameObject.GetComponent<DaggerBehaviour>())
                                                        .Where(behaviour => filter(behaviour)
                                                                         && behaviour.CanOrbit()
                                                                         && (force || behaviour.ShouldOrbit()))
                                                        .OrderBy(dagger => (order == null) ? 0 : order(dagger))) {
                dagger.ThrowForce(Vector3.up * 0.01f);
                if (daggersOrbitWhenIdle) {
                    dagger.IntoState<OrbitState>();
                } else {
                    dagger.IntoState<PouchState>();
                }
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
            } else if (bothGripAndCast && SwarmFunctionality.Test(this)) {
                state = new SwarmFunctionality();
            } else if (bothGripAndCast && ShieldFunctionality.Test(this, handMidpointDifference)) {
                state = new ShieldFunctionality();
            //} else if (bothGripAndCast && PullOutFunctionality.Test(this)) {
            //    state = new PullOutFunctionality();
            } else if (bothGripAndCast && GatherFunctionality.Test(this, handMidpointDifference, avgHandPalmDir)) {
                state = new GatherFunctionality();
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

        public IEnumerable<Holder> GetPouches(float maxDistance = 0) {
            return Item.list.Where(item => item.itemId == "DaggerPouch")
                .Where(pouch => pouch.GetComponentInChildren<Holder>() is var holder)
                .Where(pouch => pouch.holder?.creature != null
                             || pouch.mainHandler != null
                             || (maxDistance > 0 && (Vector3.Distance(Player.currentCreature.transform.position, pouch.transform.position) < maxDistance)))
                .Select(pouch => pouch.GetComponentInChildren<Holder>());
        }

        public IEnumerable<Holder> GetNonFullPouches(float maxDistance = 0) => GetPouches(maxDistance).Where(pouch => pouch.HasSlotFree());
        public IEnumerable<Holder> GetNonEmptyPouches(float maxDistance = 0) => GetPouches(maxDistance).Where(pouch => pouch.currentQuantity > 0);


        public int GetFreePouchSlots(float maxDistance = 0) => GetPouches(maxDistance).Sum(pouch => pouch.data.maxQuantity - pouch.currentQuantity);
        public bool PouchSlotAvailable() => GetFreePouchSlots() - GetDaggersInState<PouchState>().Count() > 0;

        public bool DaggerAvailable(float maxPouchDistance = 0) {
            if (Time.time - lastUnSnap < unSnapDelay)
                return false;
            return GetDaggersInState<OrbitState>().Any()
                || GetNonEmptyPouches(maxPouchDistance).Any();
        }
        public DaggerBehaviour GetFreeDaggerClosestTo(Vector3 pos, float maxPouchDistance = 0) {
            var freeDaggers = GetDaggersInState<OrbitState>();
            var pouches = GetNonEmptyPouches(maxPouchDistance);
            if (freeDaggers.NotNull().Any()) {
                return freeDaggers.OrderBy(dagger => Vector3.Distance(dagger.transform.position, pos)).FirstOrDefault();
            } else if (pouches.NotNull().Any()) {
                if (Time.time - lastUnSnap < unSnapDelay)
                    return null;
                lastUnSnap = Time.time;
                var dagger = pouches.MinBy(pouch => Vector3.Distance(pouch.transform.position, pos))
                    .UnSnapOne(false)
                    .gameObject
                    .GetOrAddComponent<DaggerBehaviour>();
                dagger?.item?.whooshPoints?.ForEach(point => point?.Play());
                return dagger;
            }
            return null;
        }

        public DaggerBehaviour GetFreeDagger(float maxPouchDistance = 0) {
            var orbitingDaggers = GetDaggersInState<OrbitState>();
            var pouches = GetNonEmptyPouches(maxPouchDistance);
            if (orbitingDaggers.NotNull().Any()) {
                return orbitingDaggers.First();
            } else if (pouches.NotNull().Any()) {
                if (Time.time - lastUnSnap < unSnapDelay)
                    return null;
                lastUnSnap = Time.time;
                var dagger = pouches.First()
                    .UnSnapOne(false)
                    .gameObject
                    .GetOrAddComponent<DaggerBehaviour>();
                dagger?.item?.whooshPoints?.ForEach(point => point?.Play());
                return dagger;
            }
            return null;
        }

        private Color PosToColor(Vector3 pos, float alpha = 1) => new Color(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f, alpha);
        public Tuple<uint, Texture2D> GetDaggerMapTexture() {
            var items = Item.list.Where(item => item.itemId == itemId
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
            var orbitingDaggers = GetDaggersInState<OrbitState>().Concat(GetDaggersInState<PouchState>());
            foreach (var pouch in GetNonEmptyPouches(5)) {
                orbitingDaggers = orbitingDaggers.Concat(pouch.items.Select(item => item.gameObject.GetOrAddComponent<DaggerBehaviour>()));
            }

            var rand = new System.Random();
            var dagger = orbitingDaggers
                .Where(d => d.GetImbue()?.spellCastBase?.id != imbueSpell.id || d.GetImbue()?.energy < d.GetImbue()?.maxEnergy * 0.8f)
                .OrderBy(d => {
                    if (!d.item.imbues.Any() || d.item.imbues.Sum(imbue => imbue.energy) == 0)
                        return 1 + rand.Next() * 0.5f;
                    if (d.GetImbue()?.spellCastBase?.id == imbueSpell.id)
                        return 0 + rand.Next() * 0.5f;
                    return 2 + rand.Next() * 0.5f;
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
            Debug.Log($"{GetFreePouchSlots()}, {GetDaggersInState<PouchState>().Count()}, {PouchSlotAvailable()}");
            CheckSideActive(Side.Left, ref wasLeftActive, ref stateLeft);
            CheckSideActive(Side.Right, ref wasRightActive, ref stateRight);
            ForBothHands(hand => {
                if (hand.IsGripping()
                 && hand.IsEmpty()
                 && hand.caster.spellInstance is SpellDagger
                 && !handFlipping[hand]
                 && state == null
                 && (hand.side == Side.Left ? stateLeft : stateRight) == null) {
                    if (gripActive[hand.side]) {
                        if (wasClaws[hand.side] == hand.playerHand.controlHand.usePressed) {
                            Catalog.GetData<EffectData>(wasClaws[hand.side] ? "ClawsToSword" : "SwordToClaws").Spawn(hand.transform).Play();
                            wasClaws[hand.side] = !hand.playerHand.controlHand.usePressed;
                        }
                    } else {
                        gripActive[hand.side] = true;
                        wasClaws[hand.side] = !hand.playerHand.controlHand.usePressed;
                    }
                    for (int i = 0; i < 3; i++) {
                        if (DaggerAvailable() && !GetDaggersInState<ClawSwordState>().Where(dagger => dagger.IsClawSwordOn(hand)).Select(dagger => dagger.ClawSwordIndex()).Contains(i)) {
                            GetFreeDaggerClosestTo(hand.transform.position)?.IntoClawSword(hand, i);
                        }
                    }
                    var daggersInHand = GetDaggersInState<ClawSwordState>().Where(dagger => dagger.IsClawSwordOn(hand));
                    if (daggersInHand.Any() && daggersInHand.Sum(dagger => dagger.rb.velocity.magnitude) / 3 >= 4) {
                        var intensity = Mathf.InverseLerp(4, 12, daggersInHand.Sum(dagger => dagger.rb.velocity.magnitude) / 3);
                        hand.playerHand.controlHand.HapticShort(Mathf.Lerp(intensity, 0, 0.3f));
                    }
                } else {
                    gripActive[hand.side] = false;
                    var handVelocity = hand.Velocity();
                    if (handVelocity.magnitude > 1.5f) {
                        if (GetDaggersInState<ClawSwordState>().Where(dagger => dagger.IsClawSwordOn(hand)).Any()) {
                            PlayThrowClip(hand);
                        }
                        foreach (var dagger in GetDaggersInState<ClawSwordState>().Where(dagger => dagger.IsClawSwordOn(hand))) {
                            dagger.ThrowForce(handVelocity * 0.7f, true);
                        }
                    } else {
                        foreach (var dagger in GetDaggersInState<ClawSwordState>().Where(dagger => dagger.IsClawSwordOn(hand))) {
                            dagger.IntoState<OrbitState>();
                        }
                    }
                }
            });
            if (BothHands(hand => Vector3.Angle(hand.PalmDir(),
                hand.otherHand.palmCollider.transform.position - hand.palmCollider.transform.position) < 30)
                && HandDistance() > 0.1f && HandDistance() < 0.3f
                && OneSpell(spell => spell != null)) {
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

            foreach (var pouch in GetNonFullPouches()) {
                var orbiting = GetDaggersInState<OrbitState>();
                foreach (var dagger in orbiting.Take(pouch.Capacity() - pouch.currentQuantity)) {
                    dagger.IntoState<PouchState>();
                }
            }

            foreach (var pouch in GetNonEmptyPouches()) {
                pouch.items.ForEach(item => item.gameObject.GetOrAddComponent<DaggerBehaviour>());
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

            // Send anything nearby into orbit
            SendDaggersToOrbit(
                dagger => Vector3.Distance(dagger.transform.position, Player.currentCreature.transform.position) < 3,
                false,
                dagger => Vector3.Distance(dagger.transform.position, Player.currentCreature.transform.position));

            Vector3 handAngularVelocity = GetHand(Side.Right).LocalAngularVelocity();
            daggers = Item.list.Where(i => i?.gameObject != null
                                        && i.itemId == itemId
                                        && i.GetComponent<DaggerBehaviour>() != null
                                        && i.gameObject.activeSelf)
                               .Select(dagger => dagger.GetComponent<DaggerBehaviour>())
                               .ToList();
            while (daggers.Count() > SpellDagger.maxDaggerCount) {
                var toDespawn = daggers.Where(
                    dagger => !(dagger?.item?.IsVisible() ?? true)
                    && dagger.CanDespawn()).FirstOrDefault();
                if (toDespawn != null) {
                    daggers.Remove(toDespawn);
                    toDespawn.Despawn();
                } else {
                    break;
                }
            }

            /*
            if (showDecoration) {
                var orbiting = GetDaggersInState<OrbitState>();
                var decoration = GetDaggersInState<DecorationState>();
                if (orbiting.Count() + decoration.Count() >= 10) {
                    var i = decoration.Count() - 1;
                    foreach (var dagger in orbiting.Take(Mathf.Clamp(4 - decoration.Count(), 0, 4))) {
                        dagger.IntoDecoration(i++);
                    }
                } else {
                    foreach (var dagger in decoration)
                        dagger.IntoState<OrbitState>();
                }
            }
            */

            if (state == null)
                DetectDualHandGestures();

            state?.Update(this);
            stateLeft?.Update(this);
            stateRight?.Update(this);
            ForBothHands(hand => {
                if (hand.grabbedHandle?.item?.itemId == itemId && hand.playerHand.controlHand.alternateUsePressed && !handHasFlipped[hand] && !handFlipping[hand]) {
                    handHasFlipped[hand] = true;
                    StartCoroutine(DaggerFlip(hand));
                } else if (!handFlipping[hand]) {
                    handHasFlipped[hand] = false;
                }
            });
        }

        public IEnumerator DaggerFlip(RagdollHand hand) {
            handFlipping[hand] = true;
            var startTime = Time.time;
            var dagger = hand.grabbedHandle.item;
            var offset = hand.transform.InverseTransformPoint(dagger.transform.position);
            var initalRotation = dagger.transform.rotation * Quaternion.Inverse(hand.transform.rotation);
            float axisPosition = hand.gripInfo.axisPosition;
            HandleOrientation currentOrientation = hand.gripInfo.orientation;
            var spell = hand.caster.spellInstance;
            var targetOrientation = hand.grabbedHandle.orientations
                        .Where(orientation => orientation.side == currentOrientation.side && orientation != currentOrientation)
                .Where(orientation => {
                    Vector3 currentAngles = currentOrientation.transform.localEulerAngles;
                    Vector3 newAngles = orientation.transform.localEulerAngles;
                    return Mathf.Approximately(Mathf.Abs(currentAngles.x - newAngles.x) + Mathf.Abs(currentAngles.y - newAngles.y) + Mathf.Abs(currentAngles.z - newAngles.z), 360f);
                })
                .FirstOrDefault();
            if (dagger.GetComponent<DaggerBehaviour>() is var behaviour) {
                behaviour.lastNoOrbitTime = Time.time;
            }
            hand.TryRelease();
            dagger.rb.isKinematic = true;
            dagger.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
            //hand.SetClosePose()
            while (Time.time - startTime < 0.15f) {
                var ratio = Mathf.Sqrt(Mathf.Clamp((Time.time - startTime) / 0.15f, 0, 1));
                dagger.transform.rotation = initalRotation * Quaternion.AngleAxis(ratio * 180, hand.PalmDir()) * hand.transform.rotation;
                dagger.transform.position = hand.transform.TransformPoint(offset);
                yield return 0;
            }
            dagger.ResetRagdollCollision();
            dagger.rb.isKinematic = false;
            if (hand.grabbedHandle != null) {
                hand.caster.spellInstance = spell;
                handFlipping[hand] = false;
                yield break;
            }
            hand.Grab(dagger.GetMainHandle(hand.side), targetOrientation, axisPosition, true);
            hand.HapticTick(1);
            hand.caster.spellInstance = spell;
            handFlipping[hand] = false;
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

    public class SwarmFunctionality : Functionality {
        public static bool Test(DaggerController controller) =>
            controller.startHandDistance > 0.2f
            && controller.BothHands(hand => controller.FacingPosition(hand.Palm(), hand.PalmDir(), hand.otherHand.Palm()))
            && controller.HandDistance() < controller.startHandDistance - 0.1f;

        public override void Enter(DaggerController controller) {
            base.Enter(controller);
        }
        public override void Update(DaggerController controller) {
            base.Update(controller);
            while (controller.DaggerAvailable(5)) {
                controller.GetFreeDagger(5)?
                    .FlyTo(
                        controller.HandMidpoint() + controller.AverageHandPointDir() * 2,
                        Quaternion.LookRotation(controller.AverageHandVelocity()));
            }
            foreach (var dagger in controller.GetDaggersInState<FlyState>()) {
                Vector3 pos = Utils.UniqueVector(dagger.item.gameObject, -controller.HandDistance() * 2, +controller.HandDistance() * 2);
                Vector3 normal = Utils.UniqueVector(dagger.item.gameObject, -controller.HandDistance() * 2, +controller.HandDistance() * 2, 1);
                Vector3 centerPos = controller.HandMidpoint() + controller.AverageHandPointDir() * 2;
                Quaternion handAngle = Quaternion.LookRotation(controller.GetHand(Side.Left).transform.position - controller.GetHand(Side.Right).transform.position);
                Vector3 facingDir = centerPos + handAngle * pos.Rotated(Quaternion.AngleAxis(Time.time * 120 + 30, normal)) - dagger.transform.position;
                dagger.FlyTo(
                    centerPos + handAngle * pos.Rotated(Quaternion.AngleAxis(Time.time * 120, normal)),
                    Quaternion.LookRotation((controller.AverageHandVelocity() + facingDir).normalized));
            }
            var daggers = controller.GetDaggersInState<FlyState>();
            var intensity = Mathf.InverseLerp(2, 12, daggers.Sum(dagger => dagger.rb.velocity.magnitude) / daggers.Count());
            controller.ForBothHands(hand => hand.HapticTick(intensity));
        }
        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            var handVelocity = controller.BothHands().Select(hand => hand.Velocity()).Aggregate((a, b) => a + b) / 2;
            if (handVelocity.magnitude > 2) {
                controller.ForBothHands(hand => controller.PlayThrowClip(hand));
                foreach (var dagger in controller.GetDaggersInState<FlyState>()) {
                    dagger.ThrowForce(handVelocity, true);
                }
            } else {
                foreach (var dagger in controller.GetDaggersInState<FlyState>()) {
                    dagger.IntoState<OrbitState>();
                }
            }
        }
    }

    public class SingleHandShield : SingleHandFunctionality {
        public override void Enter(DaggerController controller, RagdollHand hand) {
            base.Enter(controller, hand);
            hand.PlayHapticClipOver(new AnimationCurve(new Keyframe(0f, 0), new Keyframe(1f, 1)), 0.3f);
        }
        public static bool Test(DaggerController controller, RagdollHand hand) {
            return controller.HandVelocityInDirection(hand, -hand.PalmDir()) > 1.2f;
        }
        public override void Update(DaggerController controller) {
            base.Update(controller);
            var count = controller.GetDaggersInState<ShieldState>().Where(dagger => dagger.IsShieldOnHand(hand)).Count();
            while (controller.DaggerAvailable() && count < 7) {
                controller.GetFreeDaggerClosestTo(hand.transform.position)?.IntoOneHandShield(count++, hand);
            }
            count = controller.GetDaggersInState<ShieldState>().Where(dagger => dagger.IsShieldOnHand(hand)).Count();
            foreach (var dagger in controller.GetDaggersInState<ShieldState>().Where(dagger => dagger.IsShieldOnHand(hand))) {
                dagger.UpdateShield(
                    hand.Palm() + hand.PalmDir() * -0.23f + hand.PointDir() * 0.1f,
                    Quaternion.LookRotation(hand.PalmDir(), -hand.PointDir()),
                    count);
            }
            foreach (Item item in Item.list.Where(item => item.itemId == "MagicProjectile")) {
                item.GetComponentInChildren<SphereCollider>().radius = 0.2f;
            }
        }
        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            var handVelocity = controller.BothHands().Select(hand => hand.Velocity()).Aggregate((a, b) => a + b) / 2;
            if (handVelocity.magnitude > 1.5f) {
                controller.PlayThrowClip(hand);
                foreach (var dagger in controller.GetDaggersInState<ShieldState>().Where(dagger => dagger.IsShieldOnHand(hand))) {
                    var shield = dagger.state as ShieldState;
                    dagger.ThrowForce(handVelocity + shield.GetOffset() * 3, true);
                }
            } else {
                foreach (var dagger in controller.GetDaggersInState<ShieldState>().Where(dagger => dagger.IsShieldOnHand(hand))) {
                    dagger.IntoState<OrbitState>();
                }
            }
        }
    }

    public class TrapFunctionality : SingleHandFunctionality {
        public DaggerBehaviour dagger;
        public Vector3? hoverPoint;
        Vector3 lastTickPos;
        float tickDistance = 1f;

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
                if (controller.DaggerAvailable()) {
                    dagger = controller.GetFreeDaggerClosestTo(hand.transform.position);
                    lastTickPos = dagger.transform.position;
                }
                if (!dagger)
                    return;
            }
            if (Vector3.Distance(lastTickPos, dagger.transform.position) > tickDistance) {
                lastTickPos = dagger.transform.position;
                hand.HapticTick(1);
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

    public class ShieldFunctionality : Functionality {
        public static bool Test(DaggerController controller, Vector3 handMidpointDifference) =>
            handMidpointDifference.magnitude > 0.2f
            && controller.FacingDirection(handMidpointDifference, Player.currentCreature.GetHead().transform.forward)
            && controller.BothHands(hand => controller.FacingDirection(hand.PalmDir(), hand.otherHand.PalmDir()));

        public override void Enter(DaggerController controller) {
            base.Enter(controller);
            controller.ForBothHands(hand => hand.PlayHapticClipOver(new AnimationCurve(new Keyframe(0f, 0), new Keyframe(1f, 1)), 0.3f));
        }
        public override void Update(DaggerController controller) {
            base.Update(controller);
            var count = controller.GetDaggersInState<ShieldState>().Where(dagger => dagger.IsLargeShield()).Count();
            while (controller.DaggerAvailable() && count < 17) {
                controller.GetFreeDaggerClosestTo(controller.HandMidpoint())?.IntoLargeShield(count++);
            }
            foreach (var dagger in controller.GetDaggersInState<ShieldState>().Where(dagger => dagger.IsLargeShield())) {
                dagger.UpdateShield(
                    controller.HandMidpoint() + controller.HandDirection() * 0.4f,
                    Quaternion.LookRotation(controller.HandDirection(), controller.AverageHandPointDir()),
                    count);
            }
        }
        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            foreach (var dagger in controller.GetDaggersInState<ShieldState>().Where(dagger => dagger.IsLargeShield())) {
                dagger.IntoState<OrbitState>();
            }
        }
    }

    public class TrackFireFunctionality : Functionality {
        public float delay = 0.7f;
        public static bool Test(DaggerController controller) => controller.HandDistance() > controller.startHandDistance + 0.1f;
        public override void Enter(DaggerController controller) {
            base.Enter(controller);
        }
        public override void Update(DaggerController controller) {
            base.Update(controller);
            if (Time.time - enterTime > delay) {
                enterTime = Time.time;
                controller.GetFreeDagger()?
                    .TrackRandomTarget(Utils.SphereCastCreature(
                        controller.HandMidpoint(), 5, (controller.GetHand(Side.Left).PointDir() + controller.GetHand(Side.Right).PointDir()) / 2, Mathf.Infinity, live: false));
            }
        }
    }

    public class GatherFunctionality : Functionality {
        public override void Enter(DaggerController controller) {
            base.Enter(controller);
            var midpoint = new GameObject();
            midpoint.transform.position = controller.HandMidpoint();
            Catalog.GetData<EffectData>("DaggerPullFX").Spawn(midpoint.transform).Play();
            controller.ForBothHands(hand => hand.PlayHapticClipOver(new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)), 0.25f));
        }
        public static bool Test(DaggerController controller, Vector3 handMidpointDifference, Vector3 handAvgPalmDir) =>
            handMidpointDifference.magnitude > 0.15f
            && controller.FacingDirection(handMidpointDifference, Vector3.down)
            && controller.FacingDirection(handAvgPalmDir, Vector3.down);

        public override void Update(DaggerController controller) {
            base.Update(controller);
            controller.SendDaggersToOrbit(dagger => true, true, dagger => Vector3.Distance(dagger.transform.position, Player.currentCreature.transform.position));
        }
    }

    public class SlingshotFunctionality : Functionality {
        RagdollHand holdHand;
        RagdollHand drawHand;
        EffectData effectData = Catalog.GetData<EffectData>("SlingshotFX");
        EffectInstance effect;
        float lastFired = 0;
        float fireInterval = 0.25f;
        float lastHandDistance;
        float tickDistance = 0.05f;

        public static bool Test(DaggerController controller) => controller.OneSpell(spell => spell.handle && spell.handle.handlers.Any() && spell.spellCaster.ragdollHand.otherHand.IsGripping());

        public override void Enter(DaggerController controller) {
            base.Enter(controller);
            holdHand = controller.SpellWhere(spell => spell != null && (spell.handle?.handlers?.Any() ?? false))?.spellCaster?.ragdollHand;
            var source = holdHand.transform;
            effect = effectData.Spawn(source.position, source.rotation);
            effect.Play();
            effect.SetIntensity(0.0f);
            drawHand = holdHand.otherHand;
            lastHandDistance = controller.HandDistance();
        }

        public override void Update(DaggerController controller) {
            base.Update(controller);
            if (!holdHand || !drawHand) {
                return;
            }
            float intensity = Mathf.Clamp(Vector3.Distance(holdHand.transform.position, drawHand.transform.position) * 2f - 0.5f, 0, 1);
            int numDaggersNeeded = Mathf.RoundToInt(intensity * 6);
            if (Mathf.Abs(controller.HandDistance() - lastHandDistance) > tickDistance) {
                drawHand.HapticTick(intensity / 2);
                lastHandDistance = controller.HandDistance();
            }

            effect.SetIntensity(intensity);
            //holdHand.playerHand.controlHand.HapticShort(Mathf.Lerp(intensity, 0.05f, 0.3f));
            effect.effects.ForEach(effect => {
                effect.transform.position = holdHand.transform.position;
                effect.transform.rotation = Quaternion.LookRotation(holdHand.PointDir());
            });
            int numDaggersOwned = controller.daggers.Where(dagger => dagger.CheckState<SlingshotState>()).Count();
            if (numDaggersNeeded > numDaggersOwned) {
                int numDaggersToGather = numDaggersNeeded - numDaggersOwned;
                while (controller.DaggerAvailable(5) && numDaggersToGather > 0) {
                    controller.GetFreeDaggerClosestTo(holdHand.transform.position, 5).IntoSlingshot(holdHand);
                    holdHand.HapticTick(1);
                    numDaggersToGather--;
                }
            } else if (numDaggersNeeded < numDaggersOwned) {
                int numDaggersToRelease = numDaggersOwned - numDaggersNeeded;
                foreach (var dagger in controller.daggers
                    .Where(dagger => dagger.CheckState<SlingshotState>())
                    .Take(numDaggersToRelease)) {
                    holdHand.HapticTick(1);
                    dagger.IntoState<OrbitState>();
                }
            }
            IEnumerable<DaggerBehaviour> slingshotDaggers = controller.GetDaggersInState<SlingshotState>().OrderBy(dagger => dagger.item.GetInstanceID());
            int i = 0;
            int count = slingshotDaggers.Count();
            if (drawHand.playerHand.controlHand.usePressed && slingshotDaggers.Count() > 0) {
                if (Time.time - lastFired > fireInterval) {
                    lastFired = Time.time;
                    var daggerToThrow = slingshotDaggers
                        .ElementAt(new System.Random().Next(slingshotDaggers.Count()));
                    if (daggerToThrow) {
                        drawHand.HapticTick(1);
                        holdHand.HapticTick(1);
                        // remove daggerToThrow from slingshotDaggers iterable
                        slingshotDaggers = slingshotDaggers.Where(dagger => dagger != daggerToThrow);
                        daggerToThrow.ThrowForce(controller.SlingshotDir(holdHand) * 3 * (1 + controller.HandDistance() * 4), true, 20);
                        Catalog.GetData<EffectData>("SlingshotFire").Spawn(daggerToThrow.transform).Play();
                    }
                }
            } else {
                lastFired = 0;
            }
            foreach (var dagger in slingshotDaggers) {
                dagger.UpdateSlingshot(i++, count, intensity);
            }
        }

        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            if (controller.GetDaggersInState<SlingshotState>().Any()) {
                drawHand.HapticTick(1);
                holdHand.HapticTick(1);
            }
            foreach (var dagger in controller.GetDaggersInState<SlingshotState>()) {
                Catalog.GetData<EffectData>("SlingshotFire").Spawn(holdHand.transform).Play();
                dagger.ThrowForce(controller.SlingshotDir(holdHand) * 2 * (1 + controller.HandDistance() * 4), true);
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
            summonedDaggers = controller.SpawnDaggersInArea(spawnLocation, 1, 6, controller.HandVelocityInDirection(hand, Vector3.up), hand.Velocity());
            hand.PlayHapticClipOver(new AnimationCurve(new Keyframe(0f, 0), new Keyframe(1f, 1)), 0.3f);
        }

        public override void Update(DaggerController controller) {
            base.Update(controller);
            //summonedDaggers = summonedDaggers.Where(dagger => dagger?.item?.gameObject == null).ToList();

            foreach (var dagger in summonedDaggers.NotNull()) {
                if (dagger.item?.gameObject == null)
                    continue;
                Vector3 pos = Utils.UniqueVector(dagger.item.gameObject, -0.5f, 0.5f);
                Vector3 normal = Utils.UniqueVector(dagger.item.gameObject, -1, 1, 1);
                Vector3 centerPos = hand.transform.position + hand.PointDir() * 2;
                Quaternion handAngle = hand.transform.rotation;
                Vector3 facingDir = centerPos + handAngle * pos.Rotated(Quaternion.AngleAxis(Time.time * 120 + 30, normal)) - dagger.transform.position;
                dagger.FlyTo(
                    centerPos + handAngle * pos.Rotated(Quaternion.AngleAxis(Time.time * 120, normal)),
                    Quaternion.LookRotation((hand.Velocity() + facingDir).normalized));
            }
            if (summonedDaggers.Any()) {
                var intensity = Mathf.InverseLerp(2, 12, summonedDaggers.Average(dagger => dagger.rb.velocity.magnitude));
                hand.HapticTick(intensity);
            }
        }

        public override void Exit(DaggerController controller) {
            base.Exit(controller);
            if (hand.Velocity().magnitude > 2) {
                controller.PlayThrowClip(hand);
                summonedDaggers.ForEach(dagger => dagger.ThrowForce(hand.Velocity(), true));
            } else {
                summonedDaggers.ForEach(dagger => dagger.IntoState<OrbitState>());
            }
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
