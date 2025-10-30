using UnityEngine;

[CreateAssetMenu(fileName = "NewWheelSettings", menuName = "Vehicle/Wheel Settings")]
public class WheelSettingsSO : ScriptableObject
{
    [Header("Movement Settings")]
    public float motorForce = 1500f; // Torque applied to motor wheels
    public float maxSteerAngle = 30f; // Max steering angle for front wheels
    public float brakeForce = 3000f; // Brake torque applied to all wheels
    public float maxSpeed = 50f; // Maximum speed of the vehicle

    [Header("Friction Settings")]
    public float handbrakeFrictionStiffness = 0.4f; // Stiffness for drifting
    public float defaultSidewaysFrictionStiffness = 1.0f; // Default sideways friction stiffness for normal driving

    // You can add more WheelCollider properties here if needed,
    // such as forwardFriction, suspensionDistance, mass, etc.
}
