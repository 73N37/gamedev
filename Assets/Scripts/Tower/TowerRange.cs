using System.Collections.Generic;
using UnityEngine;

// TowerRange is the sensor around a tower.
// It does not shoot by itself. Instead, it watches which enemies enter and leave the
// tower's trigger area, keeps a list of those enemies, and chooses the nearest one.
//
// This means TowerRange is the bridge between "enemies are nearby" and "the tower now has a target".
// Because it has ExecuteAlways, it also keeps the visible/editor range circle matched to the tower's range
// even while values are changed in the Inspector.
[ExecuteAlways]
public class TowerRange : MonoBehaviour
{
    // tower is the parent tower that owns this range object.
    [SerializeField] private Tower tower;

    // targets stores every enemy GameObject currently inside the trigger area.
    private List<GameObject> targets = new List<GameObject>();

    // Runs once after the range object becomes active so it can find its tower and size itself.
    void Start()
    {
        if (tower == null)
        {
            tower = GetComponentInParent<Tower>();
        }

        UpdateRange();
    }

    // Runs whenever this range object is enabled so the tower reference and size stay valid.
    private void OnEnable()
    {
        if (tower == null)
        {
            tower = GetComponentInParent<Tower>();
        }

        UpdateRange();
    }

    // Runs in the editor when inspector values change so the range preview updates immediately.
    private void OnValidate()
    {
        if (tower == null)
        {
            tower = GetComponentInParent<Tower>();
        }

        UpdateRange();
    }

    // Runs every frame to keep the range object centered and select the current target.
    void Update()
    {
        // Keep the range object visually attached to the tower every frame.
        KeepRangeCentered();

        if (tower == null)
        {
            return;
        }

        // Remove destroyed enemies so the target list only contains live scene objects.
        targets.RemoveAll(target => target == null);

        if (targets.Count > 0)
        {
            // If at least one enemy is inside the range, pick the nearest one as the tower's active target.
            tower.target = GetNearestTarget();
        }
        else
        {
            // If no enemies remain in the trigger, clear the target so the tower stops shooting.
            tower.target = null;
        }
    }

    // Called by Unity when an enemy enters the tower's trigger range.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Only enemies should become valid targets for the tower.
        if (collision.CompareTag("Enemy"))
        {
            // Add the entering enemy to the list of balloons currently in range.
            targets.Add(collision.gameObject);
        }
    }

    // Called by Unity when an enemy leaves the tower's trigger range.
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            // Remove the leaving enemy so it can no longer be selected as a target.
            targets.Remove(collision.gameObject);
        }
    }

    // Called during setup or inspector changes to resize the trigger to match tower range.
    public void UpdateRange()
    {
        // If the tower reference is missing, the range object cannot size itself correctly.
        if (tower == null)
        {
            return;
        }

        // Make sure the range circle stays positioned on top of the tower before resizing it.
        KeepRangeCentered();

        // Scale the range object so the trigger area matches the tower's current range stat.
        transform.localScale = new Vector3(tower.Range, tower.Range, tower.Range);
    }

    // Runs after Update so the range visual stays snapped to the tower even if other scripts move it.
    private void LateUpdate()
    {
        KeepRangeCentered();
    }

    // Called by setup and update methods to keep the range object aligned with the tower.
    private void KeepRangeCentered()
    {
        // If the tower reference is missing, there is nothing to align to.
        if (tower == null)
        {
            return;
        }

        // Keep the range object sitting on the same world position as the tower.
        transform.position = tower.transform.position;

        // Reset rotation so the range circle stays visually upright and consistent.
        transform.rotation = Quaternion.identity;
    }

    // Called when at least one enemy is in range to pick the closest current target.
    private GameObject GetNearestTarget()
    {
        // Start by assuming the first target in the list is the nearest.
        GameObject nearestTarget = targets[0];

        // Measure the starting distance from the tower to that first candidate.
        float nearestDistance = Vector2.Distance(tower.transform.position, nearestTarget.transform.position);

        for (int i = 1; i < targets.Count; i++)
        {
            // Measure the distance to each remaining target candidate.
            float distance = Vector2.Distance(tower.transform.position, targets[i].transform.position);

            // If this candidate is closer, make it the new nearest target.
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTarget = targets[i];
            }
        }

        // Return the closest enemy currently inside the range trigger.
        return nearestTarget;
    }
}
