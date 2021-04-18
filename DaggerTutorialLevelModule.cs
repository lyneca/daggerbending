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
            var targetObj = GameObject.Find("Targets");
            foreach (var target in targetObj.GetComponentsInChildren<Transform>()) {
                target.gameObject.AddComponent<TargetBehaviour>();
            }
            //var updown = GameObject.Find("SandSword").AddComponent<MoveUpAndDown>();
            //updown.position = updown.transform.position;
            EventManager.onPossess += PlatformPlayerAttach;
            return base.OnLoadCoroutine(level);
        }
        public void PlatformPlayerAttach(Creature creature, EventTime time) {
            var handler = GameObject.Find("Platform").AddComponent<PlatformShaderHandler>();
            handler.targetTransform = creature.transform;
        }
        public override void OnUnload(Level level) {
            base.OnUnload(level);
            EventManager.onPossess -= PlatformPlayerAttach;
        }
    }

    public static class ExtensionMethods {
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
            return obj.GetComponent<T>() ?? obj.AddComponent<T>();
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

public class TargetBehaviour : MonoBehaviour {
    public float respawnDuration = 4;
    float lastHit;
    bool active = true;
    VisualEffect vfx;
    // Start is called before the first frame update
    void Start() {
        vfx = GetComponent<VisualEffect>();
    }

    // Update is called once per frame
    void Update() {
        if (!active && Time.time - lastHit > respawnDuration) {
            active = true;
            vfx.SetBool("Hit", false);
        }
    }

    void OnCollisionEnter(Collision collision) {
        if (!active)
            return;
        active = false;
        var averagePoint = collision.contacts.Aggregate(Vector3.zero, (a, point) => point.point + a) / collision.contacts.Count();
        lastHit = Time.time;
        Catalog.GetData<EffectData>("TargetBell").Spawn(transform).Play();
        vfx.SetVector3("Hit Pos", averagePoint);
        vfx.SetBool("Hit", true);
    }
}

public class PlatformShaderHandler : MonoBehaviour {
    public Transform targetTransform;
    Material material;
    // Start is called before the first frame update
    void Start() {
        material = GetComponent<Renderer>().sharedMaterial;
    }

    // Update is called once per frame
    void Update() {
        material.SetVector("Position", targetTransform.position);
    }
}

