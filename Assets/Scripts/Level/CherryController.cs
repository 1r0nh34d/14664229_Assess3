using UnityEngine;

// Spawns the bonus cherry at a random point just outside the level and lerps it in a straight line through the level
// centre to the far side, ignoring walls, ghosts and pellets. A SpriteMask over the level means it is only visible
// while inside the level bounds. The next cherry spawns 5 seconds after the previous one is destroyed.
public class CherryController : MonoBehaviour
{
    public GameObject cherryPrefab;
    public float speed = 4f;
    public float spawnDelay = 5f;
    public Vector3 levelCentre = Vector3.zero;
    public float levelRadius = 21f;   // just outside the corners of the level
    public float despawnRadius = 40f; // past the edge of the camera view

    Cherry current;
    Vector3 lerpFrom;
    Vector3 lerpTo;
    float lerpProgress;
    float duration;
    float timer;
    bool spawning = true;

    void Start()
    {
        timer = spawnDelay;
    }

    void Update()
    {
        if (current == null)
        {
            if (!spawning) return;
            timer -= Time.deltaTime;
            if (timer <= 0f) Spawn();
            return;
        }

        lerpProgress += Time.deltaTime / duration;
        current.transform.position = Vector3.Lerp(lerpFrom, lerpTo, lerpProgress);
        if (lerpProgress >= 1f) RemoveCurrent();
    }

    void Spawn()
    {
        // A random direction gives a different start point every time
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction == Vector2.zero) direction = Vector2.right;
        lerpFrom = levelCentre + (Vector3)(direction * levelRadius);
        lerpTo = levelCentre - (Vector3)(direction * despawnRadius);
        duration = Vector3.Distance(lerpFrom, lerpTo) / speed;
        lerpProgress = 0f;
        current = Instantiate(cherryPrefab, lerpFrom, Quaternion.identity).GetComponent<Cherry>();
    }

    void RemoveCurrent()
    {
        if (current != null) Destroy(current.gameObject);
        current = null;
        timer = spawnDelay;
    }

    public void CherryEaten(Cherry cherry)
    {
        if (cherry == current) RemoveCurrent();
        else Destroy(cherry.gameObject);
    }

    public void StopSpawning()
    {
        spawning = false;
        RemoveCurrent();
    }
}
