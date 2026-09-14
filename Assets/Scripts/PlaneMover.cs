using UnityEngine;
using UnityEngine.Events;

public class PlaneMover : MonoBehaviour
{
    public enum MovementMode
    {
        DirectToPoint, // Moves directly toward each waypoint
        SingleAxis     // Moves along a selected axis towards each waypoint
    }

    public enum Axis
    {
        X,
        Y,
        Z
    }

    [Header("Waypoints")]
    [Tooltip("List of points/transforms for the object to move between.")]
    public Transform[] waypoints;

    [Tooltip("How close the object needs to get to a point before moving to the next.")]
    public float reachThreshold = 0.1f;

    [Header("Movement Settings")]
    [Tooltip("Movement speed in units per second.")]
    public float speed = 5.0f;

    [Tooltip("Choose direct movement or constrain movement along an axis.")]
    public MovementMode movementMode = MovementMode.DirectToPoint;

    [Tooltip("Selected axis to move along (used if MovementMode is set to SingleAxis).")]
    public Axis moveAxis = Axis.Z;

    [Header("Events")]
    [Tooltip("Triggered when the plane reaches the final waypoint.")]
    public UnityEvent onFinalWaypointReached;

    private int currentWaypointIndex = 0;
    private bool isFinished = false;

    void Update()
    {
        // Stop updating movement if finished or if no waypoints exist
        if (isFinished || waypoints == null || waypoints.Length == 0) return;

        Transform targetWaypoint = waypoints[currentWaypointIndex];
        if (targetWaypoint == null) return;

        Vector3 targetPosition = targetWaypoint.position;
        Vector3 currentPosition = transform.position;

        // Calculate direction based on selected movement mode
        Vector3 moveDirection = Vector3.zero;

        if (movementMode == MovementMode.DirectToPoint)
        {
            // Direct vector towards target
            moveDirection = (targetPosition - currentPosition).normalized;
        }
        else if (movementMode == MovementMode.SingleAxis)
        {
            // Move along chosen axis towards target's coordinate
            float delta = 0f;

            switch (moveAxis)
            {
                case Axis.X:
                    delta = targetPosition.x - currentPosition.x;
                    moveDirection = transform.right * Mathf.Sign(delta);
                    break;
                case Axis.Y:
                    delta = targetPosition.y - currentPosition.y;
                    moveDirection = transform.up * Mathf.Sign(delta);
                    break;
                case Axis.Z:
                    delta = targetPosition.z - currentPosition.z;
                    moveDirection = transform.forward * Mathf.Sign(delta);
                    break;
            }
        }

        // Move the object smoothly towards the target position
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        // Check if close enough to current waypoint
        if (Vector3.Distance(transform.position, targetPosition) <= reachThreshold)
        {
            AdvanceToNextWaypoint();
        }
    }

    private void AdvanceToNextWaypoint()
    {
        // Check if we just reached the last waypoint
        if (currentWaypointIndex >= waypoints.Length - 1)
        {
            isFinished = true;
            
            // Snap to exact target position upon completion
            transform.position = waypoints[waypoints.Length - 1].position;

            // Trigger Unity Event
            onFinalWaypointReached?.Invoke();
            return;
        }

        currentWaypointIndex++;
    }

    // Visualize waypoints and path in the Unity Editor scene view
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            // Draw sphere at waypoint location
            Gizmos.DrawWireSphere(waypoints[i].position, 0.3f);

            // Draw connecting line to the next waypoint
            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
    }
}