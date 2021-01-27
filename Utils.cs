﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using Valve.VR;

using ExtensionMethods;
namespace ExtensionMethods {
    static class ExtensionMethods {
        /// <summary>Get raw angular velocity of the player hand</summary>
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
        public static bool IsGripping(this RagdollHand hand) => hand.playerHand.controlHand.gripPressed;

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

        /// <summary>
        /// Vector pointing in the direction of the thumb
        /// </summary>
        public static Vector3 ThumbDir(this RagdollHand hand) => hand.transform.up;

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

    public static Vector3 UniqueVector(GameObject obj, float min, float max) {
        var rand = new System.Random(obj.GetInstanceID());
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