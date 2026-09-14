using UnityEngine;

public class PlaneMover : MonoBehaviour
{
    // Enum to create a dropdown menu in the Unity Inspector
    public enum Axis
    {
        X,
        Y,
        Z
    }

    [Header("Movement Settings")]
    [Tooltip("Movement speed in units per second.")]
    public float speed = 5.0f;

    [Tooltip("Select the axis along which the object will move.")]
    public Axis moveAxis = Axis.Z;

    void Update()
    {
        // Determine movement direction vector based on the chosen axis
        Vector3 direction = Vector3.zero;

        switch (moveAxis)
        {
            case Axis.X:
                direction = transform.right; // Local X-axis
                break;
            case Axis.Y:
                direction = transform.up;    // Local Y-axis
                break;
            case Axis.Z:
                direction = transform.forward; // Local Z-axis
                break;
        }

        // Translate the object frame-rate independently
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }
}