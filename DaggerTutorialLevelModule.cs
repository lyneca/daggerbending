using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.AddressableAssets;
using ExtensionMethods;

namespace DaggerBending {
    class DaggerTutorialLevelModule : LevelModule {
        public override IEnumerator OnLoadCoroutine(Level level) {
            var thrusterObj = GameObject.Find("ThrusterObject");
            foreach (var thruster in thrusterObj.GetComponentsInChildren<Transform>()) {
                thruster.gameObject.AddComponent<Thruster>();
            }
            var thrusterBehaviour = thrusterObj.AddComponent<PIDThrusterBehaviour>();
            thrusterBehaviour.targetTransform = GameObject.Find("ThrusterTarget").transform;
            var updown = GameObject.Find("SandSword").AddComponent<MoveUpAndDown>();
            updown.position = updown.transform.position;
            return base.OnLoadCoroutine(level);
        }
    }

    public static class ExtensionMethods {
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
            return obj.GetComponent<T>() ?? obj.AddComponent<T>();
        }
    }

    public class PIDThrusterBehaviour : MonoBehaviour {
        PID slowingPID;
        PID thrustPID;
        PID avoidancePID;
        PID slowingRotationPID;
        PID targetRotationPID;
        Rigidbody rb;
        public GameObject effectPrefab;
        public Vector3 targetPosition;
        public Vector3 targetRotation;
        public Transform targetTransform = null;
        public float slowSpeed = 10;
        public float thrustSpeed = 30;
        public float slowingRotation = 50;
        public float maxForce = 100;
        //public float avoidanceAmount = 50;

        bool ready;

        void Start() {
            slowingPID = new PID(slowSpeed, 0, 0.2f);
            thrustPID = new PID(thrustSpeed, 0, 0.2f);
            slowingRotationPID = new PID(slowingRotation, 0, 0.25f);
            targetRotationPID = new PID(30, 0, 0.23f);
            //avoidancePID = new PID(avoidanceAmount, 0, 0.2382979f);
            rb = GetComponent<Rigidbody>();
            Addressables.LoadAssetAsync<GameObject>("Lyneca.Daggerbending.ThrusterFX").Completed += result => {
                effectPrefab = result.Result;
                ready = true;
                foreach (Transform thruster in transform) {
                    if (!thruster.gameObject.name.StartsWith("Thruster"))
                        continue;
                    ThrusterComponent component = thruster.gameObject.AddComponent<ThrusterComponent>();
                    component.SetEffect(Instantiate(effectPrefab));
                }
            };
        }

        float DistanceToCollider(Collider collider) {
            return Vector3.Distance(collider.ClosestPoint(transform.position), transform.position);
        }

        void Update() {
            if (!ready)
                return;
            Vector3 target = targetTransform?.position ?? targetPosition;
            Vector3 thrust = slowingPID.Update(-rb.velocity, Time.deltaTime);
            thrust += thrustPID.Update(target - transform.position, Time.deltaTime);
            float totalThrust = 0;
            foreach (Transform thruster in transform) {
                totalThrust += Vector3.Project(thrust, -thruster.forward).magnitude;
            }
            foreach (Transform thruster in transform) {
                if (!thruster.gameObject.name.StartsWith("Thruster"))
                    continue;
                if ((Vector3.Project(thrust, thruster.forward).normalized - thruster.forward).magnitude < 0.00001f)
                    continue;

                Vector3 targetPos = transform.position + targetTransform.transform.rotation * thruster.localPosition;
                Vector3 force = Vector3.Project(thrust, -thruster.forward);
                force *= (thrust.magnitude / totalThrust);
                //force += Vector3.Project(-rb.GetRelativePointVelocity(thruster.position) * 0.8f, -thruster.forward);
                var forceToTarget = thruster.gameObject.GetOrAddComponent<ThrusterComponent>().targetPid.Update(targetPos - thruster.transform.position, Time.deltaTime)
                    + thruster.gameObject.GetOrAddComponent<ThrusterComponent>().slowPid.Update(-rb.GetRelativePointVelocity(thruster.position), Time.deltaTime);
                force += Vector3.Project(forceToTarget, -thruster.forward) / 10;

                force = force.normalized * Mathf.Clamp(force.magnitude, 0, maxForce);
                thruster.gameObject.GetOrAddComponent<ThrusterComponent>().SetThrustAmount(force.magnitude);
                if (float.IsNaN(force.x))
                    return;
                rb.AddForceAtPosition(force, thruster.position);
                //rb.AddTorque(slowingRotationPID.Update(-rb.angularVelocity, Time.deltaTime));
                //rb.AddTorque(-targetRotationPID.Update(Vector3.Cross(transform.forward, targetRotation), Time.deltaTime));
                //Debug.DrawLine(thruster.position, thruster.position - force / 20);
            }
        }

        class ThrusterComponent : MonoBehaviour {
            public PID targetPid;
            public PID slowPid;
            GameObject effect;
            public void Start() {
                targetPid = new PID(2, 0, 0.2f);
                slowPid = new PID(3, 0, 0.2f);
            }

            public void SetEffect(GameObject effect) {
                this.effect = effect;
                this.effect.GetComponent<VisualEffect>().Play();
                effect.transform.SetParent(this.transform);
                effect.transform.localPosition = Vector3.zero;
                effect.transform.localScale = Vector3.one;
                effect.transform.localRotation = Quaternion.identity;
            }

            public void SetThrustAmount(float amount) {
                effect.GetComponent<VisualEffect>().SetFloat("Force", amount);
            }
        }
    }

    public class Thruster : MonoBehaviour {
        // Start is called before the first frame update
        void Start() {

        }

        // Update is called once per frame
        void Update() {

        }
    }
}

[ExecuteInEditMode]
public class MoveUpAndDown : MonoBehaviour {
    public Vector3 position;
    // Start is called before the first frame update
    void Start() {
    }

    // Update is called once per frame
    void Update() {
        transform.position = position + new Vector3(0, Mathf.Sin(Time.time / 5) / 2, 0);
    }
}

