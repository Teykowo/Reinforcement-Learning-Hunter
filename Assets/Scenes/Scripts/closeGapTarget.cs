// ----------------------------------------- imports -----------------------------------------
using static System.Math; // only for Sqrt, and recall to cast to float with (float)Sqrt(x, y) https://stackoverflow.com/questions/24806568/casting-math-pow-to-float
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
// -------------------------------------------------------------------------------------------

// The reward system in MLagent works like explained in https://forum.unity.com/threads/not-sure-about-setreward-and-addreward-functionality.866113/, meaning each time step the reward is reset to 0.

// --------------------------------------- Agent Class ---------------------------------------
public class closeGapTarget : Agent
{

    // Init a list to store vertex coordinates.
    public List<Vector3> vertices = new List<Vector3>();
    // Init a variable to hold the max distance between a vertex and the target.
    public float maxDist = 0f;
    // To encourage the target to get close, distance to the target is compared to the last one. Initially, without a previous distance to compare if the probe's getting closer, this distance is defined as the max distance to the target.
    // since it will be compared to "dist" where 0 is far and 1 is touching it, it will be defined as 0.
    public float lastDist = 0f;
    // Colour of the direction output vector.
    public Color directionOutColor = new Color(201, 167, 54);
    // Define a movement speed for the object. 
    public float moveSpeed = 5f;
    public float rotationSpeed = 180f;
    // Layer mask for raycasts.
    public LayerMask rayCastMask;
    // List of raycast directions.
    public float[] raycastDirs = {19f, 38f, 57f, 76f};
    // Raycasts length.
    public float rayCastLength = 10f;

    // The Initialize function is called once, when the Agent object is created (equivalent to a constructor for the end-user).
    public override void Initialize()
    {
        // Define the vertices coordinates of the environment for later use, store them in a list fo ease of iteration.
        vertices.Add(new Vector3(-10f, 20f, -10f)); // upLeftBack
        vertices.Add(new Vector3(10f, 20f, -10f)); // upRightBack
        vertices.Add(new Vector3(10f, 20f, 10f)); // upRightFront
        vertices.Add(new Vector3(-10f, 20f, 10f)); // upLeftFront
        vertices.Add(new Vector3(-10f, 2.5f, -10f)); // downLeftBack
        vertices.Add(new Vector3(10f, 2.5f, -10f)); // downRightBack
        vertices.Add(new Vector3(10f, 2.5f, 10f)); // downRightFront
        vertices.Add(new Vector3(-10f, 2.5f, 10f)); // downLeftFront
    }

    // "SerializeField" makes us able to input something from the Unity interface, this something is a Transform object as we input the "Prey" or "target" object.
    // It is thus stored in a variable as "targetTransform".
    [SerializeField] private Transform targetTransform;

    // This function triggers at the start of every epoch.
    public override void OnEpisodeBegin()
    {
        // Reset the maxDist to 0.
        maxDist = 0f;
        // Vector3 is the Unity object for storing and manipulation of the xyz coordinates of an entity, here we set the position of our objects to be random at the start of an epoch.
        // The defined boundaries are those of the "HuntingGround" group/box.
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
        transform.localPosition = new Vector3(Random.Range(-8f, 8f), Random.Range(4f, 18f), Random.Range(-8f, 8f));
        targetTransform.localPosition = new Vector3(Random.Range(-8f, 8f), Random.Range(4f, 18f), Random.Range(-8f, 8f));

        // We take the maximum possible distance from the target here for later use in computing the reward. This has to be one of the boxe's vertices (corners),
        // these are stored as class member variable to avoid repeated calculations. we iteratie over the vertices in the vertices list.
        foreach (Vector3 corner in this.vertices)
        {
            // Take the distance between it and the target's position.
            float currentDistCalc = Vector3.Distance(corner, targetTransform.localPosition);
            // If the distance is bigger than the previously computed one:
            if (currentDistCalc > maxDist)
            {
                // Then this new distance becomes the maxDist value.
                maxDist = currentDistCalc;
            }
        }
    }

    // Here is defined the input of the NN model.
    public override void CollectObservations(VectorSensor sensor)
    {
        // The "Probe" object is aware of:
        // Its position in its environment.
        sensor.AddObservation(transform.localPosition);
        //Its facing direction, cleaner and more understandable than rotation.
        sensor.AddObservation(transform.up);
        // Its target's position in space.
        sensor.AddObservation(targetTransform.localPosition);
        // Raycast data as to what is in front of the agent.
        // Create the rays.
        List<Ray> raycasts = new List<Ray>();
        // The position is absolute, and the up vector is added to start at the front of the Probe. The direction is towards the front.
        raycasts.Add(new Ray(transform.position + transform.up, transform.up));
        // iterate over the list of angles to make a cross shaped raycast array.
        foreach (float angle in this.raycastDirs){
            float angleRad = angle * Mathf.Deg2Rad;
            // each angle has to be multiplied by the deg2rad function tog et radians from degrees, then we can use Sin to get the x or z component, no need to normalize as it's just direction.
            raycasts.Add(new Ray(transform.position + transform.up, transform.up + transform.forward * (float)Sin(angleRad)));
            // we multiply by -1 in order to have rays in both directions.
            raycasts.Add(new Ray(transform.position + transform.up, transform.up + transform.forward * (-1 * (float)Sin(angleRad))));
            raycasts.Add(new Ray(transform.position + transform.up, transform.up + transform.right * (float)Sin(angleRad)));
            raycasts.Add(new Ray(transform.position + transform.up, transform.up + transform.right * (-1 * (float)Sin(angleRad))));
        }
        // Init lists for observations containing distance of colision if any, and nature of the object that collided.
        float[] rayDistObs = new float[17];
        float[] rayHitObs = new float[17];
        // Fill the lists.
        for (int index = 0; index < 17; index++){
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
            // If the ray doesn't connect:
            } else {
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

    // This function is triggered each step when the model returns its output vector. It takes as input the NN's output.
    public override void OnActionReceived(ActionBuffers actions)
    {
        // The "Probe" NN has 3 continuous output nodes:
        // Together they make the XYZ coordinate of a direction vector that the Probe wants to follow.
        float moveX = actions.ContinuousActions[0];
        float moveY = actions.ContinuousActions[1];
        float moveZ = actions.ContinuousActions[2];
        float forwardVelocity = actions.ContinuousActions[3];

        // Move the object by vector addition, here multiple parts are added, first we have the Y component, a new vector3 object is created with the moveY speed as the Y component.
        // Another vector in this operation is transform.up, which gives the vector pertaining to the component of the object to be concidered it's own relative "X", we multiply it by forwardSpeed to get
        // a movement in the faced direction. By adding the Y vector and the forward velocity we get the full displacement to apply to the current vector3 of the Probe (localPosition),
        // multiplying it by deltaTime which hold how many seconds happened during this frame makes the movement constant across frames, and multiplying by moveSpeed make the displacement bigger or smaller.
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        transform.localPosition += transform.up * forwardVelocity * Time.deltaTime * moveSpeed; 
        // In order to rotate the Probe to face the direction it needs to approach. The Rotate function accepts Euler angles instead of quaternion, meaning the vector of angles in each component we desire
        // for a specific rotation (world axes not specified so local axes by default). we use the NN's outputs as angles and do the deltaTime*moveSpeed multiplication as explained previously.
        Vector3 outputDirection = new Vector3(moveX, moveY, moveZ);
        // if the direction vector is null, no need to rotate. for this I check the magnitude instead of comparing to 0 vector, easier to compute.
        if (outputDirection.magnitude > 0.0001f){
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(outputDirection), Time.deltaTime * rotationSpeed);
        }

        // If the Probe never meets wall nor target, then its reward bust be defined here, the goal's dogma is as follow: the target shall be rewarded for getting closer to its target,
        // greatly so for touching it, and penalized for crashing into a wall. The longer it takes for the Probe to reach it's target, the lower its reward.

        // First component in the reward, the distance to the objective, the closer the bigger it should be. First we compute the distance between the Probe and the target, we then normalize it by divinding
        // it by the maximum distance the probe could have from the target, for differenciation purposes, the square root of the ratio is used, creating a inverted bell curve function instead of a linear one
        // since we want to have a lower reward the furthest the probe is to the target, we want a simple bell curve, thus we take the inverse by using "1-". final dist function is "y = 1-(dist/maxDist)�", and
        // it returns a number between 0 and 1, 1 being closest to the target and 0 the furthest away.
        //float dist = 1 - (float)Sqrt((Vector3.Distance(transform.localPosition, targetTransform.localPosition) / maxDist));
        // Using SetReward makes it so only the last steps' reward is kept. As the Probe didn't reach its target, the reward shall be minimal and simply act as a slope for the gardient to incite getting
        // closer. Since no collision occured for this to be the final reward, then the Probe maxed out it's allocated step count, thus only the distance to the target can be taken into account.
        // By experimenting, not dividing the reward made it always positive, even with a lower maxSteps value, the Probe always ended up throwing himself at the ceiling, dividing this value ensures a negative
        // reward when killing itself. if the new dist is closer, the value will be positive, if the probe's getting further away, negative.
        //SetReward((dist - lastDist) / 100);
        // Update lastDist.
        //lastDist = dist;

        // Print cummulated reward, makes me think it doesn't actually reset each step but whatever.
        //Debug.Log(this.GetCumulativeReward());
        Debug.DrawLine(transform.position, transform.position + outputDirection, directionOutColor, Time.deltaTime);
    }

    // Heuristic as a function is only used in the context of user-controled action, for debugging purposes, it is mainly useless in my current case.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Get the list of floats given as inputs to the NN.
        ActionSegment<float> continuousAction = actionsOut.ContinuousActions;
        // Manually set them to a specific value to move the Probe with the keyboard.
        continuousAction[0] = Input.GetAxisRaw("Horizontal");
        continuousAction[1] = Input.GetAxisRaw("Vertical");
    }

    // This function triggers whenever the Probe collides with either it's target or wall, it is then that a specific and more drastic reward shall be given, and that the epoch shall be ended.
    // It takes as input the object that triggered the collision, and thus the function call.
    private void OnTriggerEnter(Collider other)
    {
        // The Probe has a limited amount of steps to achieve it's goal, else it could stay idle and the epoch could never end. The MaxStep variable hold this value, we shall use it to obtain a value between
        // 0 and 1, 1 being very quick and 0 not being able to find the target at all. StepCount represents the number of steps as to now. We get the number of steps left max, and divide it by the number
        // of steps max. Again for gradient puposes we take the square root of the ratio.
        float quickness = (float)Sqrt(((this.MaxStep - this.StepCount) / this.MaxStep));

        // If the collided object is the target:
        if (other.TryGetComponent<Prey>(out Prey prey))
        {
            // In case of a successful mission simply set a reward of 100 * the quickness value.
            //SetReward(100f * quickness);
            SetReward(1f);
            // The epoch ends
            EndEpisode();
        }
        // If it's a wall:
        else if (other.TryGetComponent<arenaWalls>(out arenaWalls arenaWalls))
        {
            // While I could set a hard -100 if the Probe crashes into a wall, I could also define it so the Probe is less penalised when staying alive longer. To do that means to simply take the inverse of
            // the quickness variable, 0 means crashing early and 1 means staying alive longer, though we still need to penalize it heavily for this so a minimum reward of -75 should be assured with Min.
            //SetReward(Min(-100f * (1 - quickness), -50f));
            SetReward(-1f);
            // The epoch ends
            EndEpisode();
        }
    }
}
// -------------------------------------------------------------------------------------------