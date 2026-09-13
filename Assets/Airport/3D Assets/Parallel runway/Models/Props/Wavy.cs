using UnityEngine;
using System.Collections.Generic;

public class ProceduralWave : MonoBehaviour
{
    [Header("Bone Detection")]
    public string bonePrefix = "Bone";

    [Header("Wave")]
    public float amplitude = 20f;

    [Tooltip("Minimum frequency used to generate the shared frequency.")]
    public float minFrequency = 0.5f;

    [Tooltip("Maximum frequency used to generate the shared frequency.")]
    public float maxFrequency = 1.5f;

    public float speed = 2f;

    [Header("Wave Axis")]
    public bool waveX = true;
    public bool waveY = true;
    public bool waveZ = true;

    [Header("Wave Falloff")]
    [Range(0f, 1f)]
    public float rootStrength = 0f;

    [Header("Random Phase")]
    public bool randomizePhase = true;

    [Header("Whole Object Tilt")]
    [Range(-90f, 90f)]
    public float tiltX = 0f;

    [Range(-90f, 90f)]
    public float tiltY = 0f;

    [Range(-90f, 90f)]
    public float tiltZ = 0f;

    [Header("Tilt Smoothing")]
    public float tiltSmoothness = 5f;

    // --------------------------------------------------
    // Internal data
    // --------------------------------------------------

    private class HairChain
    {
        public Transform[] bones;
        public Quaternion[] initialRotations;
        public float phase;
    }

    private List<HairChain> hairChains =
        new List<HairChain>();

    private float sharedFrequency;

    private Quaternion initialObjectRotation;

    private float currentTiltX;
    private float currentTiltY;
    private float currentTiltZ;


    // ==================================================
    // START
    // ==================================================

    void Start()
    {
        FindHairChains();

        // One frequency shared by ALL hair chains
        sharedFrequency =
            Random.Range(
                minFrequency,
                maxFrequency
            );

        initialObjectRotation =
            transform.localRotation;

        currentTiltX = tiltX;
        currentTiltY = tiltY;
        currentTiltZ = tiltZ;

        Debug.Log(
            "Hair chains found: " +
            hairChains.Count +
            " | Shared Frequency: " +
            sharedFrequency
        );
    }


    // ==================================================
    // FIND HAIR CHAINS
    // ==================================================

    void FindHairChains()
    {
        hairChains.Clear();

        Transform[] allTransforms =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform transformItem in allTransforms)
        {
            // Only start from "Bone"
            if (transformItem.name != bonePrefix)
                continue;

            // If the parent is also part of a Bone chain,
            // this is not the root of a hair chain.
            if (transformItem.parent != null &&
                transformItem.parent.name.StartsWith(bonePrefix))
            {
                continue;
            }

            List<Transform> chain =
                new List<Transform>();

            Transform current =
                transformItem;

            // ------------------------------------------
            // Follow:
            // Bone
            // Bone.001
            // Bone.002
            // ...
            // ------------------------------------------

            while (current != null)
            {
                chain.Add(current);

                string nextName =
                    GetNextBoneName(
                        current.name
                    );

                Transform nextBone =
                    FindDirectChild(
                        current,
                        nextName
                    );

                current = nextBone;
            }

            if (chain.Count == 0)
                continue;

            HairChain hair =
                new HairChain();

            hair.bones =
                chain.ToArray();

            // Store original rotations
            hair.initialRotations =
                new Quaternion[
                    hair.bones.Length
                ];

            for (int i = 0;
                 i < hair.bones.Length;
                 i++)
            {
                hair.initialRotations[i] =
                    hair.bones[i].localRotation;
            }

            // Different phase for each hair
            if (randomizePhase)
            {
                hair.phase =
                    Random.Range(
                        0f,
                        Mathf.PI * 2f
                    );
            }
            else
            {
                hair.phase = 0f;
            }

            hairChains.Add(hair);
        }
    }


    // ==================================================
    // GET NEXT BONE NAME
    // ==================================================

    string GetNextBoneName(
        string currentName)
    {
        // Bone → Bone.001
        if (currentName == bonePrefix)
        {
            return bonePrefix + ".001";
        }

        // Bone.001 → Bone.002
        string numberPart =
            currentName.Substring(
                bonePrefix.Length + 1
            );

        if (!int.TryParse(
            numberPart,
            out int number))
        {
            return "";
        }

        number++;

        return bonePrefix +
               "." +
               number.ToString("D3");
    }


    // ==================================================
    // FIND DIRECT CHILD
    // ==================================================

    Transform FindDirectChild(
        Transform parent,
        string childName)
    {
        if (string.IsNullOrEmpty(childName))
            return null;

        foreach (Transform child
            in parent)
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }


    // ==================================================
    // UPDATE
    // ==================================================

    void Update()
    {
        UpdateHairWave();

        UpdateObjectTilt();
    }


    // ==================================================
    // HAIR WAVE
    // ==================================================

    void UpdateHairWave()
    {
        for (int h = 0;
             h < hairChains.Count;
             h++)
        {
            HairChain hair =
                hairChains[h];

            int boneCount =
                hair.bones.Length;

            for (int i = 0;
                 i < boneCount;
                 i++)
            {
                // ------------------------------
                // Position along the hair
                // ------------------------------

                float normalizedIndex =
                    boneCount <= 1
                    ? 0f
                    : (float)i /
                      (boneCount - 1);


                // ------------------------------
                // Root → Tip falloff
                // ------------------------------

                float falloff =
                    Mathf.Lerp(
                        rootStrength,
                        1f,
                        normalizedIndex
                    );


                // ------------------------------
                // Wave phase
                // ------------------------------

                float phase =
                    normalizedIndex *
                    sharedFrequency *
                    Mathf.PI *
                    2f;


                // ------------------------------
                // Calculate wave
                // ------------------------------

                float wave =
                    Mathf.Sin(
                        Time.time * speed +
                        phase +
                        hair.phase
                    );


                float angle =
                    wave *
                    amplitude *
                    falloff;


                // ------------------------------
                // Axis controls
                // ------------------------------

                float rotationX =
                    waveX ? angle : 0f;

                float rotationY =
                    waveY ? angle : 0f;

                float rotationZ =
                    waveZ ? angle : 0f;


                // ------------------------------
                // Apply rotation
                // ------------------------------

                hair.bones[i].localRotation =
                    hair.initialRotations[i] *
                    Quaternion.Euler(
                        rotationX,
                        rotationY,
                        rotationZ
                    );
            }
        }
    }


    // ==================================================
    // WHOLE OBJECT TILT
    // ==================================================

    void UpdateObjectTilt()
    {
        currentTiltX =
            Mathf.Lerp(
                currentTiltX,
                tiltX,
                Time.deltaTime *
                tiltSmoothness
            );

        currentTiltY =
            Mathf.Lerp(
                currentTiltY,
                tiltY,
                Time.deltaTime *
                tiltSmoothness
            );

        currentTiltZ =
            Mathf.Lerp(
                currentTiltZ,
                tiltZ,
                Time.deltaTime *
                tiltSmoothness
            );


        transform.localRotation =
            initialObjectRotation *
            Quaternion.Euler(
                currentTiltX,
                currentTiltY,
                currentTiltZ
            );
    }
}