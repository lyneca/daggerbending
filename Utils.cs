using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using Valve.VR;

using ExtensionMethods;
using System.Collections;

namespace ExtensionMethods {
    static class ExtensionMethods {
        /// <summary>Get raw angular velocity of the player hand</summaryt
        public static Vector3 GetHandAngularVelocity(this InputSteamVR self, Side side) {
            return SteamVR_Actions.Default.Pose.GetAngularVelocity((side == Side.Right) ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand);
        }

        /// <summary>Get raw rotation of the player hand</summary>
        public static Quaternion GetHandRotation(this InputSteamVR self, Side side) {
            return SteamVR_Actions.Default.Pose.GetLocalRotation((side == Side.Right) ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand);
        }

        /// <summary>Get raw angular velocity of the player hand</summary>
        public static Vector3 GetHandAngularVelocity(this InputOculus self, Side side) {
            return OVRInput.GetLocalControllerAngularVelocity(side == Side.Right ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch);
        }

        /// <summary>Get raw rotation of the player hand</summary>
        public static Quaternion GetHandRotation(this InputOculus self, Side side) {
            return OVRInput.GetLocalControllerRotation(side == Side.Right ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch);
        }

        public static bool IsEmpty(this RagdollHand hand) {
            return !hand.caster.isFiring
                && !hand.caster.isMerging
                && !Player.currentCreature.mana.mergeActive
                && hand.grabbedHandle == null
                && !hand.isGrabbed
                && hand.caster.telekinesis.catchedHandle == null;
        }

        public static int Capacity(this Holder holder) => holder.data.maxQuantity;

        /// <summary>Get raw platform-independant hand rotation</summary>
        public static Quaternion GetRawHandRotation(this RagdollHand hand) {
            if (PlayerControl.input is InputSteamVR inputSteam) {
                return inputSteam.GetHandRotation(hand.side);
            } else if (PlayerControl.input is InputOculus inputOculus) {
                return inputOculus.GetHandRotation(hand.side);
            } else {
                return Quaternion.identity;
            }
        }

        /// <summary>Get raw platform-independant hand angular velocity</summary>
        public static Vector3 GetRawHandAngularVelocity(this RagdollHand hand) {
            if (PlayerControl.input is InputSteamVR inputSteam) {
                return inputSteam.GetHandAngularVelocity(hand.side);
            } else if (PlayerControl.input is InputOculus inputOculus) {
                return inputOculus.GetHandAngularVelocity(hand.side);
            } else {
                return Vector3.zero;
            }
        }

        /// <summary>
        ///  Get hand local angular velocity
        /// </summary>
        public static Vector3 LocalAngularVelocity(this RagdollHand hand) => hand.transform.InverseTransformDirection(hand.rb.angularVelocity);

        /// <summary>
        ///  Promise-like Task chaining
        /// </summary>
        public static Task<TOutput> Then<TInput, TOutput>(this Task<TInput> task, Func<TInput, TOutput> func) {
            return task.ContinueWith((input) => func(input.Result));
        }

        /// <summary>
        ///  Promise-like Task chaining
        /// </summary>
        public static Task Then(this Task task, Action<Task> func) {
            return task.ContinueWith(func);
        }

        /// <summary>
        ///  Promise-like Task chaining
        /// </summary>
        public static Task Then<TInput>(this Task<TInput> task, Action<TInput> func) {
            return task.ContinueWith((input) => func(input.Result));
        }

        /// <summary>
        /// Get a component from the gameobject, or create it if it doesn't exist
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
            return obj.GetComponent<T>() ?? obj.AddComponent<T>();
        }

        /// <summary>
        /// Force this WhooshPoint to play its effect
        /// </summary>
        public static void Play(this WhooshPoint point) {
            if ((Utils.GetInstanceField(point, "trigger") is WhooshPoint.Trigger trigger) && trigger != WhooshPoint.Trigger.OnGrab && Utils.GetInstanceField(point, "effectInstance") != null)
                (Utils.GetInstanceField(point, "effectInstance") as EffectInstance)?.Play();
            Utils.SetInstanceField(point, "effectActive", true);
            Utils.SetInstanceField(point, "dampenedIntensity", 0);
        }

        /// <summary>
        /// Attempt to point an item's FlyDirRef at a target vector
        /// </summary>
        /// <param name="target">Target vector</param>
        /// <param name="lerpFactor">Lerp factor (if you're calling over multiple frames)</param>
        /// <param name="upDir">Up direction</param>
        public static void PointItemFlyRefAtTarget(this Item item, Vector3 target, float lerpFactor, Vector3? upDir = null) {
            Vector3 up = upDir ?? Vector3.up;
            if (item.flyDirRef) {
                item.transform.rotation = Quaternion.Slerp(
                    item.transform.rotation * item.flyDirRef.localRotation,
                    Quaternion.LookRotation(target, up),
                    lerpFactor) * Quaternion.Inverse(item.flyDirRef.localRotation);
            } else if (item.holderPoint) {
                item.transform.rotation = Quaternion.Slerp(
                    item.transform.rotation * item.holderPoint.localRotation,
                    Quaternion.LookRotation(target, up),
                    lerpFactor) * Quaternion.Inverse(item.holderPoint.localRotation);
            } else {
                Quaternion pointDir = Quaternion.LookRotation(item.transform.up, up);
                item.transform.rotation = Quaternion.Slerp(item.transform.rotation * pointDir, Quaternion.LookRotation(target, up), lerpFactor) * Quaternion.Inverse(pointDir);
            }
        }

        /// <summary>
        /// Is is this hand gripping?
        /// </summary>
        public static bool IsGripping(this RagdollHand hand) => hand?.playerHand?.controlHand?.gripPressed ?? false;
        public static void HapticTick(this RagdollHand hand, float intensity = 1) => hand.playerHand.controlHand.HapticShort(intensity);
        public static void PlayHapticClipOver(this RagdollHand hand, AnimationCurve curve, float duration) {
            hand.StartCoroutine(HapticPlayer(hand, curve, duration));
        }
        public static IEnumerator HapticPlayer(RagdollHand hand, AnimationCurve curve, float duration) {
            var time = Time.time;
            while (Time.time - time < duration) {
                hand.HapticTick(curve.Evaluate((Time.time - time) / duration));
                yield return 0;
            }
        }

        /// <summary>
        /// Return the minimum entry in an interator using a custom comparable function
        /// </summary>
        public static T MinBy<T>(this IEnumerable<T> enumerable, Func<T, IComparable> comparator) {
            if (!enumerable.Any())
                return default;
            return enumerable.Aggregate((curMin, x) => (curMin == null || (comparator(x).CompareTo(comparator(curMin)) < 0)) ? x : curMin);
        }

        /// <summary>
        /// .Select(), but only when the output of the selection function is non-null
        /// </summary>
        public static IEnumerable<TOut> SelectNotNull<TIn, TOut>(this IEnumerable<TIn> enumerable, Func<TIn, TOut> func)
            => enumerable.Where(item => func(item) != null).Select(func);
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T> enumerable)
            => enumerable.Where(item => item != null);

        /// <summary>
        /// Get a point above the player's hand
        /// </summary>
        public static Vector3 PosAboveBackOfHand(this RagdollHand hand) => hand.transform.position
            - hand.transform.right * 0.1f
            + hand.transform.forward * 0.2f;

        /// <summary>
        /// Vector pointing away from the palm
        /// </summary>
        public static Vector3 PalmDir(this RagdollHand hand) => hand.transform.forward * -1;

        /// <summary>
        /// Vector pointing away in the direction of the fingers
        /// </summary>
        public static Vector3 PointDir(this RagdollHand hand) => -hand.transform.right;
        public static Vector3 Palm(this RagdollHand hand) => hand.transform.position + hand.PointDir() * 0.1f;
        public static Vector3 Velocity(this RagdollHand hand) => Player.local.transform.rotation * hand.playerHand.controlHand.GetHandVelocity();
        public static float MapOverCurve(this float time, params Tuple<float, float>[] points) {
            var curve = new AnimationCurve();
            foreach (var point in points) {
                curve.AddKey(new Keyframe(point.Item1, point.Item2));
            }
            return curve.Evaluate(time);
        }
        public static float MapOverCurve(this float time, params Tuple<float, float, float, float>[] points) {
            var curve = new AnimationCurve();
            foreach (var point in points) {
                curve.AddKey(new Keyframe(point.Item1, point.Item2, point.Item3, point.Item4));
            }
            return curve.Evaluate(time);
        }

        /// <summary>
        /// Vector pointing in the direction of the thumb
        /// </summary>
        public static Vector3 ThumbDir(this RagdollHand hand) => (hand.side == Side.Right) ? hand.transform.up : -hand.transform.up;

        /// <summary>
        /// Clamp a number between -1000 and 1000, just in case
        /// </summary>
        public static float SafetyClamp(this float num) => Mathf.Clamp(num, -1000, 1000);

        /// <summary>
        /// I miss Rust's .abs()
        /// </summary>
        public static float Abs(this float num) => Mathf.Abs(num);

        /// <summary>
        /// float.SafetyClamp() but for vectors
        /// </summary>
        public static Vector3 SafetyClamp(this Vector3 vec) => vec.normalized * vec.magnitude.SafetyClamp();
        
        /// <summary>
        /// Returns true if the vector's X component is its largest component
        /// </summary>
        public static bool MostlyX(this Vector3 vec) => vec.x.Abs() > vec.y.Abs() && vec.x.Abs() > vec.z.Abs();

        /// <summary>
        /// Returns true if the vector's Y component is its largest component
        /// </summary>
        public static bool MostlyY(this Vector3 vec) => vec.y.Abs() > vec.x.Abs() && vec.y.Abs() > vec.z.Abs();

        /// <summary>
        /// Returns true if the vector's Z component is its largest component
        /// </summary>
        public static bool MostlyZ(this Vector3 vec) => vec.z.Abs() > vec.x.Abs() && vec.z.Abs() > vec.y.Abs();

        /// <summary>
        /// Get a creature's part from a PartType
        /// </summary>
        public static RagdollPart GetPart(this Creature creature, RagdollPart.Type partType) => creature.ragdoll.GetPart(partType);

        /// <summary>
        /// Get a creature's head
        /// </summary>
        public static RagdollPart GetHead(this Creature creature) => creature.ragdoll.headPart;

        /// <summary>
        /// Get a creature's torso
        /// </summary>
        public static RagdollPart GetTorso(this Creature creature) => creature.GetPart(RagdollPart.Type.Torso);

        public static Vector3 Rotated(this Vector3 vector, Quaternion rotation, Vector3 pivot = default) {
            return rotation * (vector - pivot) + pivot;
        }

        public static Vector3 Rotated(this Vector3 vector, Vector3 rotation, Vector3 pivot = default) {
            return Rotated(vector, Quaternion.Euler(rotation), pivot);
        }

        public static Vector3 Rotated(this Vector3 vector, float x, float y, float z, Vector3 pivot = default) {
            return Rotated(vector, Quaternion.Euler(x, y, z), pivot);
        }

        public static void SetPosition(this EffectInstance instance, Vector3 position) {
            instance.effects.ForEach(effect => effect.transform.position = position);
        }
        public static void SetRotation(this EffectInstance instance, Quaternion rotation) {
            instance.effects.ForEach(effect => effect.transform.rotation = rotation);
        }

        public static void RunCoroutine(this MonoBehaviour mono, Func<IEnumerator> function, float delay = 0) {
            mono.StartCoroutine(RunAfterCoroutine(function, delay));
        }
        public static void RunAfter(this MonoBehaviour mono, System.Action action, float delay = 0) {
            mono.StartCoroutine(RunAfterCoroutine(action, delay));
        }
        public static IEnumerator RunAfterCoroutine(Func<IEnumerator> function, float delay) {
            yield return new WaitForSeconds(delay);
            yield return function();
        }
        public static IEnumerator RunAfterCoroutine(System.Action action, float delay) {
            yield return new WaitForSeconds(delay);
            action();
        }

        // This method is ILLEGAL and only acceptable in places where no other option exists
        public static object Call(this object o, string methodName, params object[] args) {
            var mi = o.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi != null) {
                return mi.Invoke(o, args);
            }
            return null;
        }
        public static Item UnSnapOne(this Holder holder, bool silent) {
            Item obj = holder.items.LastOrDefault();
            if (obj)
                holder.UnSnap(obj, silent);
            return obj;
        }
    }
}
static class Utils {
    // WARNING: If you can find a way to not use the following two methods, please do - they are INCREDIBLY bad practice
    /// <summary>
    /// Get a private field from an object
    /// </summary>
    internal static object GetInstanceField<T>(T instance, string fieldName) {
        BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static;
        FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
        return field.GetValue(instance);
    }
    public static void Explosion(Vector3 origin, float force, float radius, bool massCompensation = false, bool disarm = false) {
        var seenRigidbodies = new List<Rigidbody>();
        var seenCreatures = new List<Creature> { Player.currentCreature };
        foreach (var collider in Physics.OverlapSphere(origin, radius)) {
            if (collider.attachedRigidbody == null)
                continue;
            if (collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerHandAndFoot) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotion) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotionObject))
                continue;
            if (!seenRigidbodies.Contains(collider.attachedRigidbody)) {
                seenRigidbodies.Add(collider.attachedRigidbody);
                float modifier = 1;
                if (collider.attachedRigidbody.mass < 1) {
                    modifier *= collider.attachedRigidbody.mass * 2;
                } else {
                    modifier *= collider.attachedRigidbody.mass;
                }
                if (!massCompensation)
                    modifier = 1;
                collider.attachedRigidbody.AddExplosionForce(force * modifier, origin, radius, 1, ForceMode.Impulse);
            } else if (collider.GetComponentInParent<Creature>() is Creature npc && npc != null && !seenCreatures.Contains(npc)) {
                seenCreatures.Add(npc);
                npc.brain.instance.TryPush((npc.ragdoll.rootPart.transform.position - origin).normalized, npc.ragdoll.creature.brain.instance.gravityPushBehaviorPerLevel[2]);
                if (disarm) {
                    npc.handLeft.TryRelease();
                    npc.handRight.TryRelease();
                }
            }
        }
    }

    public static Transform GetPlayerChest() {
        return Player.currentCreature.ragdoll.GetPart(RagdollPart.Type.Torso).transform;
    }

    public static Vector3 UniqueVector(GameObject obj, float min, float max, int salt = 0) {
        var rand = new System.Random(obj.GetInstanceID() + salt);
        return new Vector3(
            (float)rand.NextDouble() * (max - min) + min,
            (float)rand.NextDouble() * (max - min) + min,
            (float)rand.NextDouble() * (max - min) + min);
    }

    /// <summary>
    /// Set a private field from an object
    /// </summary>
    internal static void SetInstanceField<T, U>(T instance, string fieldName, U value) {
        BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static;
        FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
        field.SetValue(instance, value);
    }

    /// <summary>
    /// Get a list of live NPCs
    /// </summary>
    public static IEnumerable<Creature> GetAliveNPCs() => Creature.list
        .Where(creature => creature != Player.currentCreature
                        && creature.state != Creature.State.Dead);

    /// <summary>
    /// ConeCast only creatures
    /// </summary>
    /// <param name="origin">Origin position</param>
    /// <param name="maxRadius">Maximum cone radius</param>
    /// <param name="direction">Cone direction</param>
    /// <param name="maxDistance">Maximum cone distance</param>
    /// <param name="coneAngle">Cone angle</param>
    /// <param name="npc">Only detect NPCs (i.e. ignore the player)</param>
    /// <param name="live">Only detect live creatures</param>
    public static IEnumerable<Creature> ConeCastCreature(Vector3 origin, float maxRadius, Vector3 direction, float maxDistance, float coneAngle, bool npc = true, bool live = true) {
        return ConeCastAll(origin, maxRadius, direction, maxDistance, coneAngle)
            .SelectNotNull(hit => hit.rigidbody?.gameObject.GetComponent<Creature>())
            .Where(creature => (!npc || creature != Player.currentCreature) && (!live || creature.state != Creature.State.Dead));
    }
    public static IEnumerable<Creature> SphereCastCreature(Vector3 origin, float maxRadius, Vector3 direction, float maxDistance, bool npc = true, bool live = true) {
        return Physics.SphereCastAll(origin, maxRadius, direction, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .SelectNotNull(hit => hit.rigidbody?.gameObject.GetComponent<Creature>())
            .Where(creature => (!npc || creature != Player.currentCreature) && (!live || creature.state != Creature.State.Dead));
    }

    public static void UpdateDriveStrengths(ConfigurableJoint joint, float strength) {
        if (joint == null)
            return;
        JointDrive posDrive = new JointDrive();
        posDrive.positionSpring = 100 * strength;
        posDrive.positionDamper = 10 * strength;
        posDrive.maximumForce = 1000;
        JointDrive rotDrive = new JointDrive();
        rotDrive.positionSpring = 10 * strength;
        rotDrive.positionDamper = 1 * strength;
        rotDrive.maximumForce = 100;
        joint.xDrive = posDrive;
        joint.yDrive = posDrive;
        joint.zDrive = posDrive;
        joint.angularXDrive = rotDrive;
        joint.angularYZDrive = rotDrive;
    }

    static public ConfigurableJoint CreateTKJoint(Rigidbody source, Handle target, Side side) {
        var joint = source.gameObject.AddComponent<ConfigurableJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero;
        joint.connectedBody = target.rb;
        joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(target.GetDefaultAxisPosition(side));
        joint.rotationDriveMode = RotationDriveMode.XYAndZ;
        JointDrive jointDrive1 = new JointDrive();
        JointDrive jointDrive2 = new JointDrive();
        jointDrive1.positionSpring = 100;
        jointDrive1.positionDamper = 10;
        jointDrive1.maximumForce = 1000;
        jointDrive2.positionSpring = 10;
        jointDrive2.positionDamper = 1;
        jointDrive2.maximumForce = 1000;
        joint.xDrive = jointDrive1;
        joint.yDrive = jointDrive1;
        joint.zDrive = jointDrive1;
        joint.angularXDrive = jointDrive2;
        joint.angularYZDrive = jointDrive2;
        joint.xMotion = ConfigurableJointMotion.Free;
        joint.yMotion = ConfigurableJointMotion.Free;
        joint.zMotion = ConfigurableJointMotion.Free;
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;
        return joint;
    }

    // Original idea from walterellisfun on github: https://github.com/walterellisfun/ConeCast/blob/master/ConeCastExtension.cs
    /// <summary>
    /// Like SphereCastAll but in a cone
    /// </summary>
    /// <param name="origin">Origin position</param>
    /// <param name="maxRadius">Maximum cone radius</param>
    /// <param name="direction">Cone direction</param>
    /// <param name="maxDistance">Maximum cone distance</param>
    /// <param name="coneAngle">Cone angle</param>
    public static RaycastHit[] ConeCastAll(Vector3 origin, float maxRadius, Vector3 direction, float maxDistance, float coneAngle) {
        RaycastHit[] sphereCastHits = Physics.SphereCastAll(origin, maxRadius, direction, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        List<RaycastHit> coneCastHitList = new List<RaycastHit>();

        if (sphereCastHits.Length > 0) {
            for (int i = 0; i < sphereCastHits.Length; i++) {
                Vector3 hitPoint = sphereCastHits[i].point;
                Vector3 directionToHit = hitPoint - origin;
                float angleToHit = Vector3.Angle(direction, directionToHit);

                if (angleToHit < coneAngle) {
                    coneCastHitList.Add(sphereCastHits[i]);
                }
            }
        }

        return coneCastHitList.ToArray();
    }
}
