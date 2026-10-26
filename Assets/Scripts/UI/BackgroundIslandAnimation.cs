using UnityEngine;
using System;
using System.Collections.Generic;
public class BackgroundIslandAnimation : MonoBehaviour
{
    [System.Serializable]
    public class IslandElement
    {
        public string name = "Island";
        public RectTransform transform;

        [Header("movement")]
        public float floatSpeed = 1.5f;
        public float floatAmplitude = 10f;

        [Header("tilt")]
        public float tiltAngle = 1.0f;
        public float tiltSpeedMultiplier = 0.5f;

        [Header("offset")]
        public float phaseOffset = 0f;

        [HideInInspector] public Vector3 originalPosition;
    }

    [SerializeField] private List<IslandElement> islands = new List<IslandElement>();

    private void Awake()
    {
        foreach (var island in islands)
        {
            if (island.transform != null)
            {
                island.originalPosition = island.transform.anchoredPosition;
            }
        }
    }

    private void OnEnable()
    {
        foreach (var island in islands)
        {
            if (island.transform != null && island.originalPosition == Vector3.zero)
            {
                island.originalPosition = island.transform.anchoredPosition;
            }
        }
    }

    private void Update()
    {
        float time = Time.time;

        foreach (var island in islands)
        {
            if (island.transform == null) continue;

            float currentPhase = (time * island.floatSpeed) + island.phaseOffset;
            float newY = island.originalPosition.y + (Mathf.Sin(currentPhase) * island.floatAmplitude);

            float tiltPhase = currentPhase * island.tiltSpeedMultiplier;
            float newTilt = Mathf.Cos(tiltPhase) * island.tiltAngle;

            island.transform.anchoredPosition = new Vector2(island.originalPosition.x, newY);
            island.transform.localRotation = Quaternion.Euler(0, 0, newTilt);
        }
    }
}