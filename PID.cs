using UnityEngine;
using ExtensionMethods;

[System.Serializable]
public class PID {
	public float pFactor, iFactor, dFactor;
		
	Vector3 integral;
	Vector3 lastError;
	
	public PID(float pFactor, float iFactor, float dFactor) {
		this.pFactor = pFactor;
		this.iFactor = iFactor;
		this.dFactor = dFactor;
	}

	public Vector3 Update(Vector3 present, float timeFrame) {
		integral += present * timeFrame;
		Vector3 deriv = (present - lastError) / timeFrame;
		lastError = present;
		return present * pFactor + integral * iFactor + deriv * dFactor;
	}
}

public class PIDRigidbodyHelper {
	public PID velocityPID;
	public PID slowingPID;
	public PID headingPID;
	public PID dampeningPID;
	public Rigidbody rb;
	public bool isActive;

	public PIDRigidbodyHelper(Rigidbody rigidbody, float acceleration, float dampening) {
		rb = rigidbody;
		isActive = true;
		velocityPID = new PID(acceleration, 0, 0.3f);
		slowingPID = new PID(dampening, 0, 0.3f);
		headingPID = new PID(acceleration, 0, 0.3f);
		dampeningPID = new PID(dampening, 0, 0.3f);
    }

	public void Update(Vector3 targetPos, Quaternion targetRot) {
		if (!isActive)
			return;
		UpdateVelocity(targetPos);
		UpdateTorque(targetRot);
    }

	public void UpdateVelocity(Vector3 targetPos, float forceMult = 1f, float slowMult = 1f) {
		if (!isActive)
			return;
		if (Time.deltaTime != 0 && Time.deltaTime != float.NaN) {
			var force = velocityPID.Update(targetPos - rb.transform.position, Time.deltaTime).SafetyClamp() * forceMult
					  + slowingPID.Update(-rb.velocity, Time.deltaTime).SafetyClamp() * slowMult;
			rb.AddForce(force);
		}
    }

	public void UpdateTorque(Quaternion targetRot) {
		if (!isActive)
			return;
		if (Time.deltaTime != 0 && Time.deltaTime != float.NaN) {
			var torque = -headingPID.Update(Vector3.Cross(rb.transform.rotation * Vector3.forward, targetRot * Vector3.forward), Time.deltaTime).SafetyClamp()
					   + dampeningPID.Update(-rb.angularVelocity, Time.deltaTime).SafetyClamp();
			rb.AddTorque(torque);
		}
    }
}
