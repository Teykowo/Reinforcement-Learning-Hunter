// ----------------------------------------- imports -----------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
// -------------------------------------------------------------------------------------------

// --------------------------------------- Probe Class ---------------------------------------
public class Target : MonoBehaviour
{
    // --------------------------------- -Initializing variables ---------------------------------
    [Tooltip("Displacement speed of the target.")]
    public float movementSpeed;

    // Get the layer mask for the direction raycast, since it's spawned inside the target, we should ignore the target's collider. It has to be called from Unity's Start function
    private int targetLayerMask;
    // Define a random speed at which the target will move to it's next position.
    private float randomizedSpeed = 0f;
    // Define a variable that'll be used to determine whether or not to get a new position, or to keep swimming.
    private float nextActionTime = -1f;
    // Define a Vector3 variable to hold the direction of the ray casting.
    private Vector3 targetMovementDirectionRay;
    // Define a Vector3 variable to hold the position towards which the target moves.
    private Vector3 targetMovementDestination;
    // Variable for the probability of the target swimming to a new position.
    private int probabilitySwim;

    private void Start()
    {
        targetLayerMask = LayerMask.GetMask("Default", "Walls");
    }
    // -------------------------------------------------------------------------------------------

    /* FixedUpdate is called at a regular interval of 0.02 seconds, independent of frame rate, so we can interact with this object normally even if the agent's training at an increased game speed. */
    private void FixedUpdate()
    {
        // If the target has a movement speed, and only as often as the probability allows
        if (movementSpeed > 0f)
        {
            Swim();
        }
    }

    /* Define the Swim function, in which a ray is cast to determine the position toward which the target will move. */
    private void Swim()
    {
        // If the time has come for the target to move to a new position:
        if (Time.fixedTime >= nextActionTime)
        {
            // Get a random probability.
            probabilitySwim = UnityEngine.Random.Range(0, 300);

            // And given this probability:
            if (probabilitySwim <= 0)
            {
                // Pick a random destination for the target by ray casting.
                // Init a raycast hit object.
                RaycastHit hit;
                // Init a variable to hold the distance between target and ray cast collision.
                float collisionDist;
                // Get a random, normalized direction for the ray.
                targetMovementDirectionRay = new Vector3(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f)).normalized;
                // If there is a collision of a raycast that starts at the target's position, toward a random direction, for 100 units and ignoring targets colliders:
                if (Physics.Raycast(transform.position, targetMovementDirectionRay, out hit, 100f, targetLayerMask))
                {
                    // Get the distance between the target and the collision.
                    collisionDist = hit.distance;
                    // If the collision occured far enough for there to be sense in moving the target:
                    if (collisionDist > 3)
                    {
                        // Randomize the speed between 0.5 and 1.5 times the target's default speed.
                        randomizedSpeed = movementSpeed * UnityEngine.Random.Range(0.2f, 1f);
                        // Get final destination by taking a percentage of the max distance in this direction, with an arbitrary minimum of one third. This is added to the current position to define arrival coordinates.
                        targetMovementDestination = transform.position + targetMovementDirectionRay * (collisionDist * UnityEngine.Random.Range(0.33f, 0.66f));
                        // Rotate toward the target's target position. This function takes the direction to face and the vector that defines "up". No slerp nescessary for this one. Finally rotate the object to face the direction,
                        // given the weird import error when it comes to basis vectors.
                        transform.rotation = Quaternion.LookRotation(targetMovementDirectionRay, transform.forward) * Quaternion.Euler(-90, 0, 90);

                        // Compute the time to get there with the fish's current randomized speed.
                        float commuteTime = Vector3.Distance(transform.position, targetMovementDestination) / randomizedSpeed;
                        nextActionTime = Time.fixedTime + commuteTime;
                    }
                }
            }
        }
        // Else, keep swimming toward the destination.
        else
        {
            // Get the displacement of the target toward it's destination for this frame. (again *-1 due to the model being reversed).
            Vector3 moveVector = randomizedSpeed * (-1 * transform.right) * Time.fixedDeltaTime;
            // If the displacement's magnitude is less than the distance left to travel, everything is fine and the target moves as normal.
            if (moveVector.magnitude <= Vector3.Distance(transform.position, targetMovementDestination))
            {
                // Move the target toward it's destination by simply changing position, instead of using MovePosition, for simplicity.
                transform.position += moveVector;
            }
            // If the displacement leads the target beyond it's destination:
            else
            {
                // Simply change the target's position to the destination.
                transform.position = targetMovementDestination;
                // And Change the nextActionTimer to be the time between fixed frames.
                nextActionTime = Time.fixedTime;
            }
        }
    }
}
// -------------------------------------------------------------------------------------------