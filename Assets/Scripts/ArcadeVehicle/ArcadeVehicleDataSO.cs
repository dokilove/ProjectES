using UnityEngine;

[CreateAssetMenu(fileName = "NewArcadeVehicleSettings", menuName = "Vehicle/Arcade Vehicle Settings")]
public class ArcadeVehicleDataSO : ScriptableObject
{
    [Header("Movement")]
    public float maxSpeed = 15f;
    public float rotationSpeed = 15f;
    [Range(0, 1)]
    [Tooltip("Speed multiplier while charging the dash (0 = stop, 1 = full speed).")]
    public float chargeMoveSpeedMultiplier = 0.2f;

    [Header("Gravity")]
    [Tooltip("Extra gravity force to apply, making the vehicle fall faster. Acts as an acceleration.")]
    public float extraGravityForce = 20f;
    [Tooltip("How far down to check for ground to apply snapping force.")]
    public float groundSnapDistance = 1.5f;
    [Tooltip("How strongly to push the vehicle down to stick to slopes.")]
    public float groundSnapForce = 60f;

    [Header("Dash")]
    [Tooltip("The speed of the dash with minimum charge.")]
    public float minDashSpeed = 20f;
    [Tooltip("The speed of the dash with maximum charge.")]
    public float maxDashSpeed = 40f;
    [Tooltip("Minimum time the player must hold the button to trigger a dash.")]
    public float minChargeTime = 0.1f;
    [Tooltip("How long the player can hold the button to charge the dash fully.")]
    public float maxChargeTime = 1.5f;
    [Tooltip("The duration of the dash with minimum charge.")]
    public float minDashDuration = 0.2f;
    [Tooltip("The duration of the dash with maximum charge.")]
    public float maxDashDuration = 0.5f;
}
