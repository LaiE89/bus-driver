using UnityEngine;

public class WeepingAngel : Monster
{
    [SerializeField] float moveSpeed = 0.2f;

    protected override void Tick(float deltaTime)
    {
        if (State != PassengerState.Seated || head == null)
        {
            return;
        }

        Camera watcher = Cabin.ViewCamera;
        if (watcher == null)
        {
            return;
        }

        if (!IsBeingWatched(watcher))
        {
            Move(deltaTime);
        }
    }

    private bool IsBeingWatched(Camera watcher)
    {
        Vector3 direction = transform.position - watcher.transform.position;

        // Is the Angel within the camera's field of view?
        float angle = Vector3.Angle(
            watcher.transform.forward,
            direction
        );

        if (angle > watcher.fieldOfView)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    void Move(float deltaTime)
    {
        Vector3 target = PlayerModeController.Instance.PlayerPosition;

        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        direction.Normalize();

        transform.position += direction * moveSpeed * deltaTime;
    }
}
