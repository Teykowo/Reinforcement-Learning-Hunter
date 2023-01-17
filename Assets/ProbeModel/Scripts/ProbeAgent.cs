// ----------------------------------------- imports -----------------------------------------
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using static System.Math; // only for Sqrt and Sin, and recall to cast to float with (float)Sqrt(x, y) https://stackoverflow.com/questions/24806568/casting-math-pow-to-float
// -------------------------------------------------------------------------------------------

// --------------------------------------- Probe Class ---------------------------------------
public class ProbeAgent : Agent
{
    // --------------------------------- -Initializing variables ---------------------------------
    [Tooltip("Displacement speed of the agent.")]
    public float moveSpeed = 5f;
    [Tooltip("Rotation speed of the agent.")]
    public float rotSpeed = 180f;
    [Tooltip("Layer mask for raycasts.")]
    public LayerMask rayCastMask;
    [Tooltip("List of raycast directions.")]
    public float[] raycastDirs = { 19f, 38f, 76f };
    [Tooltip("Raycasts length.")]
    public float rayCastLength = 5f;
    [Tooltip("The minimum and maximum values for maxSteps")]
    public int minAllowedSteps = 5000;
    public int maxAllowedSteps = 25000;

    // An objects rigidbody has to be manually fetched, contrary to transform.
    new private Rigidbody rigidbody;
    // Links to the trainingGround of this current probe.
    private TrainingGround trainingGround;
    // The reward removed each loss to encourage action.
    private float stepRewardLoss;

    /* override the initialize mlagent function, which is called at the object's construction */
    public override void Initialize()
    {
        base.Initialize();
        rigidbody = GetComponent<Rigidbody>();
        trainingGround = GetComponentInParent<TrainingGround>();
        // stepRewardLoss is a function of MaxSteps and spawnQuantityDef, and MaxSteps is itself a function of spawnQuantityDef, so we need to seperate them to define MaxSteps.
        // We modify MaxStep, reminder that it has to be an int.
        MaxStep = Convert.ToInt32(Math.Floor((maxAllowedSteps - minAllowedSteps) / (1 + Math.Pow(1.2, (-1 * trainingGround.spawnQuantityDef) + 20)) + minAllowedSteps));
        stepRewardLoss = (-1f / MaxStep) * trainingGround.spawnQuantityDef;
    }
    // -------------------------------------------------------------------------------------------

    // ------------------------------------ Action Processing ------------------------------------
    /* Define the NN inputs of our Agent. */
    public override void CollectObservations(VectorSensor sensor)
    {
        // The probe is aware of its own orientation, a Vector of size 3.
        sensor.AddObservation(transform.right);

        // Raycast data as to what is in front of the agent.
        // Create the rays.
        List<Ray> raycasts = new List<Ray>();
        // The position is absolute, and the right vector is added to start at the front of the Probe. The direction is towards the front.
        raycasts.Add(new Ray(transform.position + transform.right, transform.right));
        // iterate over the list of angles to make a cross shaped raycast array.
        foreach (float angle in this.raycastDirs)
        {
            float angleRad = angle * Mathf.Deg2Rad;
            // each angle has to be multiplied by the deg2rad function tog et radians from degrees, then we can use Sin to get the x or z component, no need to normalize as it's just direction.
            raycasts.Add(new Ray(transform.position + transform.right, transform.right + transform.forward * (float)Sin(angleRad)));
            // we multiply by -1 in order to have rays in both directions.
            raycasts.Add(new Ray(transform.position + transform.right, transform.right + transform.forward * (-1 * (float)Sin(angleRad))));
            raycasts.Add(new Ray(transform.position + transform.right, transform.right + transform.up * (float)Sin(angleRad)));
            raycasts.Add(new Ray(transform.position + transform.right, transform.right + transform.up * (-1 * (float)Sin(angleRad))));
        }
        // Init lists for observations containing distance of colision if any, and nature of the object that collided.
        float[] rayDistObs = new float[13];
        float[] rayHitObs = new float[13];
        // Fill the lists.
        for (int index = 0; index < 13; index++)
        {
            // Init the variable that will be populated if something connects with the raycast. It'll hold what connected.
            RaycastHit hit;
            // If the ray connects:
            if (Physics.Raycast(raycasts[index], out hit, this.rayCastLength, rayCastMask))
            {
                // Draw the raycast in red if collision occured.
                Debug.DrawRay(raycasts[index].origin, raycasts[index].direction * this.rayCastLength, Color.red, 1);
                // Get the distance.
                rayDistObs[index] = hit.distance;
                // Get the object's tag, if it's the wall set this value to 0, and 1 if it's the target.
                rayHitObs[index] = (hit.transform.tag == "Target") ? 1f : 0f;
            }
            // If the ray doesn't connect:
            else
            {
                // Draw the raycast in white if no collision occured.
                Debug.DrawRay(raycasts[index].origin, raycasts[index].direction * this.rayCastLength, Color.white, 1);
                // Set the distance and object tag to -1.
                rayDistObs[index] = -1f;
                rayHitObs[index] = -1f;
            }
        };
        sensor.AddObservation(rayDistObs);
        sensor.AddObservation(rayHitObs);
    }

    /* This function takes an array of numerical values that pertain to the agent's action, aka its NN's outputs, either continuous or discrete. */
    public override void OnActionReceived(ActionBuffers actions)
    {
        // ------------------ Movement Actions Gathering ------------------
        // Whether to move forward or not.
        float moveBoolean = actions.DiscreteActions[0];

        // Whether and in what direction to rotate around the Y axis, default to 0 meaning to not rotate.
        float rotYValue = 0f;
        // This value is between 0 and 2, 0 means not to rotate, 1 to rotate counter clockwise and 2 means rotating clockwise.
        if (actions.DiscreteActions[1] == 1f)
        {
            rotYValue = -1;
        }
        else if (actions.DiscreteActions[1] == 2f)
        {
            rotYValue = 1;
        }

        // Whether and in what direction to rotate around the X axis, default to 0 meaning to not rotate.
        float rotXValue = 0f;
        // This value is between 0 and 2, 0 means not to rotate, 1 to rotate counter clockwise and 2 means rotating clockwise.
        if (actions.DiscreteActions[2] == 1f)
        {
            rotXValue = -1;
        }
        else if (actions.DiscreteActions[2] == 2f)
        {
            rotXValue = 1;
        }
        // ----------------------------------------------------------------

        // ----------------- -Movement Actions Processing -----------------
        // Apply the movement smoothly to the probe object. Right means forward (red), up means right (green), forward means up (blue).
        rigidbody.MovePosition(transform.position + transform.right * moveBoolean * moveSpeed * Time.fixedDeltaTime);
        // Rotate, or not, around the Y axis.
        transform.Rotate(transform.forward * rotYValue * rotSpeed * Time.fixedDeltaTime, Space.World);
        // Rotate, or not, around the X axis.
        transform.Rotate(transform.up * rotXValue * rotSpeed * Time.fixedDeltaTime, Space.World);
        // ----------------------------------------------------------------

        // Applying a tiny negative reward each step encourages actions. Here we use addreward to add this small decrease each step, in order to have at episode's end, the total reward minus the addition of all these step rewards.
        if (MaxStep > 0) AddReward(stepRewardLoss);
    }

    /* Override Heuristic function for user-controlled testing, the inputs are sent to OnActionReceived just like a NN's output */
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Init variables for all the actions, default as 0 as it means not moving.
        int forwardAction = 0;
        int rotYAction = 0;
        int rotXAction = 0;

        // If escape bar is pressed, the probe will move forward.
        if (Input.GetKey(KeyCode.Space))
        {
            // move forward
            forwardAction = 1;
        }

        // Use the arrows to rotate.
        if (Input.GetKey(KeyCode.UpArrow))
        {
            // Rotate upward.
            rotXAction = 1;
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            // Rotate downward.
            rotXAction = 2;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            // Rotate to the right.
            rotYAction = 1;
        }
        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            // Rotate to the left.
            rotYAction = 2;
        }

        // Output the actions in the ActionBuffer read by OnActionReceived.
        actionsOut.DiscreteActions.Array[0] = forwardAction;
        actionsOut.DiscreteActions.Array[1] = rotYAction;
        actionsOut.DiscreteActions.Array[2] = rotXAction;
    }
    // -------------------------------------------------------------------------------------------

    // ----------------------------------- Collision Detection -----------------------------------
    /* This function will be called on collision with another object's collider mesh. meaning Trigger doesn't have to */
    private void OnCollisionEnter(Collision collision)
    {
        // If collision occurs with a target:
        if (collision.transform.CompareTag("Target"))
        {
            // Remove it from the environment, and from the list of targets.
            trainingGround.RemoveSpecificTarget(collision.gameObject);
            // And increase the episode score, since the task was completed.
            AddReward(1f);
            // If all the targets have been collided with:
            if (trainingGround.RemainingTargets <= 0)
            {
                // Add a final reward to encourage finding all targets and end episode.
                AddReward(1f);
                EndEpisode();
            }
        }
        // If collision occurs with a wall:
        else if (collision.transform.CompareTag("Walls"))
        {
            // Set a negative reward to penalize colliding with a wall.
            AddReward(-2f);
            // End the epoch.
            EndEpisode();
        }
    }
    // -------------------------------------------------------------------------------------------

    // -------------------------------- Set and Reset Environment --------------------------------
    /* Reset the agent and area whenever an episode begins. */
    public override void OnEpisodeBegin()
    {
        trainingGround.ResetArena();
    }
    // -------------------------------------------------------------------------------------------
}
// -------------------------------------------------------------------------------------------