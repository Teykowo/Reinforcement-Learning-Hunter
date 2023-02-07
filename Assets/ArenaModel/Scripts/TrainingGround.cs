// ----------------------------------------- imports -----------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using TMPro;
using System.Linq;
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


    [Tooltip("The list of possible chunk models.")]
    public List<GameObject> chunkModels; // Library of each hexagonal chunk model prefabs.

    [Tooltip("Prefab of the arena's walls.")]
    public GameObject wallPrefab;

    [Tooltip("Number of circonvolutions for the map generation.")]
    public int CirconvolutionsCount = 3; // The depth defines how many layers we want to process, not counting the center, meaning a depth of one is 7 chunks, of two is 19 etc...


    [Tooltip("Text mesh that'll display the cumulative reward in a clean, in-environment way.")]
    public TextMeshPro cumulativeRewardText;

    // To get accessed later on, I initialize these variables beforehand.
    private float totalHeight; // Used for checking if the spawned object is clipping underground.
    private float arenaHeight; // Height of the sea object.
    private float arenaHeightHalf; // Offset up and down from the center of the sea object.
    private float arenaWidthHalf; // Offset around the center of the sea object to it's borders.
    private Vector3 arenaCenter; // Position of the center of the sea object.

    public void GenerateMap()
    {
        int mapMatrixWidth = 11; // Width of the matrix representing the map.
        int mapMatrixHeight = 11; // Height of the same matrix.
        int[,] mapMatrix = new int[mapMatrixWidth, mapMatrixHeight]; // Init a 0 matrix 0's represent the lack of chunk at these coordinates and 1's represent the presence of one.

        // Store the coordinates for the center of the map matrix.
        int startX = mapMatrixWidth / 2;
        int startY = mapMatrixHeight / 2;
        mapMatrix[startX, startY] = 1; // Initialize the center of the matrix as already being generated since we always have a starting chunk.

        // Init those lists with the data relative to the initial chunk.
        List<int[]> chunksNewBasisCoordinates = new List<int[]> { new int[] { startX, startY } }; // This list of arrays will hold the coordinates of chunks using new basis vectors so as to be useable as indices in the mapMatrix.
        List<float[]> chunksUnityCoordinates = new List<float[]> { new float[] { 0, 0 } }; // This list of arrays will hold the coordinates of chunks in unity.

        // Create a list of vectors that when added to another, point to its neighbours.
        int[,] neighbourAccessVectors = { { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };

        // Store the offset from which we'll iterate over the created chunks to generate more.
        int processingOffset = 0;
        int layersLen = 0; // Intermediate value for the storing the offset.
        var random = new System.Random(); // init a random generator to choose from the list of chunk models.

        int[] chunkBasisCoordinates; // Init empty array to store the chunk's new basis coordinates and then append to the vector of chunk coordinates.
        float[] newChunkUnityC; // Init empty array to store chunks unity coordinates.

        // For each of the desired layers:
        for (int layer = 0; layer <= CirconvolutionsCount; layer++)
        {
            // store the number of chunks at the currently already generated layers.
            layersLen = chunksNewBasisCoordinates.Count;

            // For each chunk in the previous layer:
            for (int chunkIndex = processingOffset; chunkIndex < layersLen; chunkIndex++)
            {
                // Iterate over the neighbour access vector:
                for (int neighbourVectorIndex = 0; neighbourVectorIndex < neighbourAccessVectors.GetLength(0); neighbourVectorIndex++)
                {
                int[] chunk = chunksNewBasisCoordinates[chunkIndex];
                    // And generate the new chunk's basis coordinates.
                    int newChunkBasisX = chunk[0] + neighbourAccessVectors[neighbourVectorIndex, 0];
                    int newChunkBasisY = chunk[1] + neighbourAccessVectors[neighbourVectorIndex, 1];


                    // Check if a chunk wasn't already generated there.
                    if (mapMatrix[newChunkBasisX, newChunkBasisY] == 0)
                    {
                        //  Generate the unity coordinate. We substract the starting coordinates from the matrix since it's centered in a grid, we need to start at 0 in unity.
                        newChunkUnityC = offsetX(newChunkBasisX - startX, newChunkBasisY - startY);

                        if (layer >= CirconvolutionsCount)
                        {
                            // The orientation of the walls depends on the neighbour they are trying to acces, we will use the neighbour index to rotate the wall object such that 0:-90, 1:-30, 2:30, 3:90, 4:150, 5:210
                            Quaternion wallRotation = Quaternion.Euler(0, -90 + neighbourVectorIndex * 60, 90);
                            GameObject mapWallGameObject = Instantiate<GameObject>(wallPrefab.gameObject, new Vector3(newChunkUnityC[0], 0, newChunkUnityC[1]), wallRotation);

                            // Assign the object to the correct parent. (TrainingGround -> Map -> Walls)
                            mapWallGameObject.transform.SetParent(transform.GetChild(2).GetChild(1).gameObject.transform, false);

                            // Post instantiation, move the wall toward the chunk's edge to close the map. Thanks to our rotation method all walls face the center of their chunks so we just move them formward relative to themselves.
                            mapWallGameObject.transform.position = mapWallGameObject.transform.position + 8.66f * mapWallGameObject.transform.forward;
                        }
                        else
                        {
                            // If not, then we can append the chunk to the list of chunk and update the board.
                            chunkBasisCoordinates = new int[] { newChunkBasisX, newChunkBasisY };
                            chunksNewBasisCoordinates.Add(chunkBasisCoordinates);

                            // We can also append the unity coordinates to the related list.
                            chunksUnityCoordinates.Add(newChunkUnityC);
                            // Instantiate a copy of the chunk prefab object after randomly picking and index for the list of possible models.
                            int index = random.Next(chunkModels.Count);
                            // To have less of a repetition in the map we rotate the randomly chunk.
                            int chunkRotation = random.Next(6);

                            GameObject chunkGameObject = Instantiate<GameObject>(chunkModels[index].gameObject, new Vector3(newChunkUnityC[0], 0, newChunkUnityC[1]), Quaternion.Euler(0, chunkRotation * 60, 0));

                            // Assign the object to the correct parent. (TrainingGround -> Map -> Tiles)
                            chunkGameObject.transform.SetParent(transform.GetChild(2).GetChild(0).gameObject.transform, false);

                            // Update the value in the mapMatrix to keep track of chunks.
                            mapMatrix[newChunkBasisX, newChunkBasisY] = 1;
                        }
                    }
                }
            }
            // Update the processingOffset with the layersLength from before the creation of the new chunks.
            processingOffset = layersLen;
        }
    }

    // Define a function that takes the unit-length-basis-coordinates of a chunk and generates the x and y offsets needed to place the chunk using the original unity basis.
    // each chunk is a hexagone of length vertex to vertex equal to 20, and side to side 17.32.
    private float[] offsetX(int xNewBasis, int yNewBasis)
    {
        // Each chunk is moved 17.32 x from the center of the previous one, hence we multiply this number by the coordinates to get the displacement from 0.
        float xOffset = (float) 17.32 * xNewBasis;
        // We do the same for the y direction with 15.
        float yOffset = (float) 15 * yNewBasis;

        // multiply the offset between lines by the y value to get the offset of this line.
        xOffset += (float) 8.66 * yNewBasis;

        float[] UnityCoordinates = { xOffset, yOffset };
        return UnityCoordinates;
    }

    /* Override the function Initialize, which runs during object construction, to access data only available after game objects are built (i could append to the constructor). */
    /* If changed to MonoBehaviour inheritence, just create the function instead of overriding and call it in start() */
    public void Initialize()
    {
        GenerateMap();

        // Get the sea gameObject.
        Transform sea = transform.GetChild(2).GetChild(0).GetChild(0).GetChild(1).gameObject.transform;
        // Get the "sea" object's height and width, this then, defines the arena size, without the sand layer.
        arenaHeight = sea.lossyScale.y * 2f; // Up from the center is the actual returned height.
        arenaHeightHalf = sea.lossyScale.y;
        arenaWidthHalf = (float) CirconvolutionsCount * 17.32f;
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
            targetObject.transform.position = GetSpawnCoordinates(arenaCenter, -1 * arenaHeightHalf * 0.20f, arenaHeightHalf - arenaHeightHalf * 0.20f, 0, 360, arenaWidthHalf * 0.15f, arenaWidthHalf);
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