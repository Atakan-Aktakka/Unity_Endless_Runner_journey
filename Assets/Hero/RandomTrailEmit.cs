using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomTrailEmit : MonoBehaviour
{
    private TrailRenderer trailRenderer;
    private float duration;
    private float timestamp;

    // Start is called before the first frame update
    void Start()
    {
        trailRenderer = GetComponent<TrailRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time > timestamp + duration)
        {
            duration = Random.Range(0.05f, 0.3f);
            timestamp = Time.time;
            trailRenderer.emitting = !trailRenderer.emitting;
        }
    }
}
