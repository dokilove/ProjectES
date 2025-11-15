using UnityEngine;

[CreateAssetMenu(fileName = "NewArcadeVehicleSettings", menuName = "Vehicle/Arcade Vehicle Settings")]
public class ArcadeVehicleDataSO : ScriptableObject
{
    [Header("Movement")]
    public float maxSpeed = 15f;
    public float rotationSpeed = 15f;

    [Header("Gravity")]
    [Tooltip("Extra gravity force to apply, making the vehicle fall faster. Acts as an acceleration.")]
    public float extraGravityForce = 20f;
    [Tooltip("How far down to check for ground to apply snapping force.")]
    public float groundSnapDistance = 1.5f;
    [Tooltip("How strongly to push the vehicle down to stick to slopes.")]
    public float groundSnapForce = 60f;
}
