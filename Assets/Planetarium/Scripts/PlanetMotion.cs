using UnityEngine;

public class PlanetMotion : MonoBehaviour
{
    public float spinSpeed = 10f;      // degrees per second around its own axis
    public Transform orbitCenter;      // e.g. the Sun (leave empty = no orbit)
    public float orbitSpeed = 3f;      // degrees per second around the center

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        if (orbitCenter != null)
            transform.RotateAround(orbitCenter.position, Vector3.up, orbitSpeed * Time.deltaTime);
    }
}