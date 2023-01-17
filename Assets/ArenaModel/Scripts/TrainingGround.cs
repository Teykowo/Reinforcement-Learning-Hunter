// ----------------------------------------- imports -----------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using TMPro;
// -------------------------------------------------------------------------------------------

// --------------------------------------- Arena Class ---------------------------------------
public class TrainingGround : MonoBehaviour
{
    // --------------------------------- -Initializing variables ---------------------------------
    // Define some public variables that will be configureable in the Unity interface.
    [Tooltip("The Probe Agent inside the area.")]
    public ProbeAgent probeAgent; // Refer to the in-game probe object.

    [Tooltip("Prefab of a target object.")]
    public Target targetPrefab; // Refer to the target prefab file for building targets.

    [Tooltip("Number of targets to spawn.")]
    public int spawnQuantityDef = 1;

    [Tooltip("The list of Target objects.")]
    public List<GameObject> targetList;

    [Tooltip("Text mesh that'll display the cumulative reward in a clean, in-environment way.")]
    public TextMeshPro cumulativeRewardText;

    // To get accessed later on, I initialize these variables beforehand.
    private float totalHeight; // Used for checking if the spawned object is clipping underground.
    private float arenaHeight; // Height of the sea object.
    private float arenaHeightHalf; // Offset up and down from the center of the sea object.
    private float arenaWidth; // Width of the sea object.
    private float arenaWidthHalf; // Offset around the center of the sea object to it's borders.
    private Vector3 arenaCenter; // Position of the center of the sea object.

    /* Override the function Initialize, which runs during object construction, to access data only available after game objects are built (i could append to the constructor). */
    /* If changed to MonoBehaviour inheritence, just create the function instead of overriding and call it in start() */
    public void Initialize()
    {
        // Get the sea gameObject. Since we need to use it twice, we also get the sea object's direct parent.
        Transform seaParent = transform.GetChild(0).GetChild(0).GetChild(0).gameObject.transform;
        Transform sea = seaParent.GetChild(seaParent.childCount-1).transform;
        // Get the "sea" object's height and width, this then, defines the arena size, without the sand layer.
        arenaHeight = sea.lossyScale.y * 2f; // Up from the center is the actual returned height.
        arenaHeightHalf = sea.lossyScale.y;
        arenaWidth = sea.lossyScale.x * 2f;
        arenaWidthHalf = sea.lossyScale.x;
        arenaCenter = sea.position;

    }
    // -------------------------------------------------------------------------------------------

    // -------------------------------- Target Removing Functions --------------------------------
    /* Remove a specific target after contact with a probe. It's input is a specific object. */
    public void RemoveSpecificTarget(GameObject targetObject)
    {
        // This removes the object from the list.
        targetList.Remove(targetObject);
        // And this removes the object from the unity game instance.
        Destroy(targetObject);
    }

    /* Get the remaining number of targets. This is a type of variables that gets and returns a value when called, dynamically. */
    public int RemainingTargets
    {
        get {return targetList.Count;}
    }

    /* Create a function that deletes all target objects in the scene. */
    private void RemoveAllTarget()
    {
        // If there is any target present:
        if (targetList != null)
        {
            // Then for each entry in the list of targets:
            for (int i = 0; i < targetList.Count; i++)
            {
                // If the target at this entry (static list) is still present:
                if (targetList[i] != null)
                {
                    // Delete it from the game instance.
                    // No need to remove it from the list, as this function deletes it whole, we might as well just create a new list to replace the other. We do need to tell unity to destroy the objects.
                    Destroy(targetList[i]);
                }
            }
        }
        // Reset the list of targets to be empty.
        targetList = new List<GameObject>();
    }
    // -------------------------------------------------------------------------------------------

    // ------------------------------------ -Spawning Objects ------------------------------------
    /* Choose the 3D spawning coordinates for any object inside a cylinder, and return them as a Unity Vector3 object. The parameters act as the cylinder's definition. The cylinder's center is defined by the point at parameter "center"
       To spawn an object inside a cylinder, three components are nescessary: the "y" component is self-explicit, it defines the y coordinates relative to the cylinder's center; the "theta" component defines the angle in degrees,
       starting at the arbitrary global "X" or "Rigth" component/direction; and finally "z" defines how far from the origin the point is, the min generally being 0 and the max the cylinder's radius.
       Since default values have to be compile-time constants, I have to define them as such, the actual values being by nature variables. */
    public Vector3 GetSpawnCoordinates(Vector3 center, float minY = -1 /*arenaHeightHalf*/, float maxY = 1 /*arenaHeightHalf*/, float minTheta = 0, float maxTheta = 360, float minZ = 0, float maxZ = 1 /*arenaWidthHalf*/)
    {
        // BY default define level, radius and angle as the minimum values, in case they are also the maxiumum.
        float level = minY;
        float radius = minZ;
        float angle = minTheta;

        // Thus only if there is a max value bigger than the min, pick random values between them:
        if (maxY > minY)
        {
            level = UnityEngine.Random.Range(minY, maxY);
        }
        if (maxTheta > minTheta)
        {
            angle = UnityEngine.Random.Range(minTheta, maxTheta);
        }
        if (maxZ > minZ)
        {
            radius = UnityEngine.Random.Range(minZ, maxZ);
        }

        // Thus the vecto3 position is: the center point, to which is added the level value in the Y direction thanks to transform.up vector, we then take the quaternion defined by the euler angle vector
        // with the angle of rotation around the y axis, the quaternion multiplication operator is overloaded to automatically mutliply a vector by the quaternion's inverse, so we have the rotated "right" vector,
        // that we naturaly multiply by our radius to get the length away from the center. Y, Theta, Z.
        Vector3 spawnCoords = center + Vector3.up * level + Quaternion.Euler(0f, angle, 0f) * transform.right * radius;


        // Init a raycast hit object.
        RaycastHit hit;
        // Init a ground coordinates Vector3 object.
        Vector3 groundCoords;
        // raycast's starting coordinate, base on the spawned object's X and Z coordinates.
        Vector3 raycastSpawn = new Vector3(spawnCoords.x, arenaCenter.y + arenaHeightHalf, spawnCoords.z);
        // If there is a collision of a raycast that starts at the top of the arena, toward the "down" direction.
        if (Physics.Raycast(raycastSpawn, transform.up * -1, out hit))
        {
            // Get the coordinates of the collision.
            groundCoords = hit.point;
            // If the ground is higher than the intended spawning point:
            if (groundCoords.y > spawnCoords.y)
            {
                // then prop the object up to this ground level, plus a bit of leeway.
                spawnCoords.y = groundCoords.y + 1;
            }
        }
        // Return, modified or not, the spawn coordinates of the object.
        return spawnCoords;
    }

    /* Spawning function for the probe, we may want it to be spawned more or less far from its target to make the task more or less complex. */
    private void ResetProbe(Vector3 center, float minY, float maxY, float minTheta, float maxTheta, float minZ, float maxZ)
    {
        // Get the rigidbody element of the probe object in unity.
        Rigidbody rigidbody = probeAgent.GetComponent<Rigidbody>();
        // Reset velocity and angular velocity so as not to spin out of control and get flicked out in a direction, it may happen when the object clips slightly inside a wall and translates to the next episode if not addressed.
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        // Get the spawning coordinates.
        probeAgent.transform.position = GetSpawnCoordinates(center, minY, maxY, minTheta, maxTheta, minZ, maxZ);
        // Set a random rotation for the object.
        UnityEngine.Random.InitState(System.DateTime.Now.Millisecond);
        probeAgent.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
    }

    /* Spawn a defined number of target in the environment given the cylinder spawn zone. The only parameter is then how many targets to spawn, defaulting to 1. */
    private Vector3 SpawnTargets(int spawnQuantity = 1)
    {
        // Init a vector3 for use outside of the for loop.
        Vector3 lastSpawnedTargetCoords = Vector3.zero;
        // Enumerate over the spawnQuantity.
        for (int i = 0; i < spawnQuantity; i++)
        {
            // Instantiate a copy of the target prefab object.
            GameObject targetObject = Instantiate<GameObject>(targetPrefab.gameObject);
            // Get the spawnpoint's coordinates.
            targetObject.transform.position = GetSpawnCoordinates(arenaCenter, -1 * arenaHeightHalf * 0.20f, arenaHeightHalf - arenaHeightHalf * 0.20f, 0, 360, 0, arenaWidthHalf - arenaWidthHalf * 0.20f);
            // Save the last position.
            if (i == spawnQuantity - 1)
            {
                lastSpawnedTargetCoords = targetObject.transform.position;
            }

            // Set a random rotation for the object.
            targetObject.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            // Set the parent of the target to be the arena.
            targetObject.transform.SetParent(transform);
            // Add the new target to the list of targets.
            targetList.Add(targetObject);

            // Set the movement speed of this target.
            targetObject.GetComponent<Target>().movementSpeed = 1;
        }
        return lastSpawnedTargetCoords;
    }
    // -------------------------------------------------------------------------------------------

    // -------------------------------- Set and Reset Environment --------------------------------
    /* Create a reset function. */
    public void ResetArena()
    {
        RemoveAllTarget();
        Vector3 lastSpawnedTargetCoords = SpawnTargets(spawnQuantityDef);
        ResetProbe(arenaCenter, -1 * arenaHeightHalf * 0.20f, arenaHeightHalf - arenaHeightHalf * 0.20f, 0, 360, 0, arenaWidthHalf - arenaWidthHalf * 0.20f); // lastSpawnedTargetCoords, 3, 5, 0, 360, 3, 5 <- for spawning coordinates based on last targets.
    }

    /* The Start function plays when the game starts */
    public void Start()
    {
        Initialize();
        ResetArena();
    }

    /* The Update function is called every frame */
    public void Update()
    {
        // Set the reward text to the current reward of the agent.
        cumulativeRewardText.text = probeAgent.GetCumulativeReward().ToString("0.00");
    }
}
// -------------------------------------------------------------------------------------------