using UnityEngine;

// Throwaway test monster. Its only tell: the head slowly turns to stare at whatever
// is watching the cabin, be it the active CCTV camera, the driver, or the player on foot.
// To remove it: delete this file, its prefab, and MonsterStopIndex in the scene builder.
public class StaringMonster : Monster {
    [SerializeField] float turnSpeed = 25f;
    [SerializeField] float yawLimit = 110f;
    [SerializeField] float pitchLimit = 35f;

    protected override void Tick(float deltaTime) {
        if (State != PassengerState.Seated || head == null) {
            return;
        }
        Camera watcher = Cabin.ViewCamera;
        if (watcher == null) {
            return;
        }
        Vector3 toWatcher = transform.InverseTransformPoint(watcher.transform.position) - head.localPosition;
        float yaw = Mathf.Clamp(Mathf.Atan2(toWatcher.x, toWatcher.z) * Mathf.Rad2Deg, -yawLimit, yawLimit);
        float flat = new Vector2(toWatcher.x, toWatcher.z).magnitude;
        float pitch = Mathf.Clamp(-Mathf.Atan2(toWatcher.y, flat) * Mathf.Rad2Deg, -pitchLimit, pitchLimit);
        head.localRotation = Quaternion.RotateTowards(head.localRotation, Quaternion.Euler(pitch, yaw, 0f), turnSpeed * deltaTime);
    }

    protected override void OnLeaving(bool kicked) {
        if (head != null) {
            head.localRotation = Quaternion.identity;
        }
    }
}
