using System.Collections.Generic;
using UnityEngine;

// Trigger-based helper that decides which enemy the tower should currently target.
public class TowerRange : MonoBehaviour
{
    [SerializeField] private Tower tower;
    private List<GameObject> targetsInRange = new List<GameObject>();

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
        KeepRangeCentered();

        if (tower == null)
        {
            return;
        }

        targetsInRange.RemoveAll(target => target == null);

        if (targetsInRange.Count > 0)
        {
            tower.target = GetNearestTarget();
        }
        else
        {
            tower.target = null;
        }
    }

    // Called by Unity when an enemy enters the tower's trigger range.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Enemy"))
        {
            targetsInRange.Add(collision.gameObject);
        }
    }

    // Called by Unity when an enemy leaves the tower's trigger range.
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            targetsInRange.Remove(collision.gameObject);
        }
    }

    // Called during setup or inspector changes to resize the trigger to match tower range.
    public void UpdateRange()
    {
        if (tower == null)
        {
            return;
        }

        KeepRangeCentered();
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
        if (tower == null)
        {
            return;
        }

        transform.position = tower.transform.position;
        transform.rotation = Quaternion.identity;
    }

    // Called when at least one enemy is in range to pick the closest current target.
    private GameObject GetNearestTarget()
    {
        GameObject nearestTarget = targetsInRange[0];
        float nearestDistance = Vector2.Distance(tower.transform.position, nearestTarget.transform.position);

        for (int i = 1; i < targetsInRange.Count; i++)
        {
            float distance = Vector2.Distance(tower.transform.position, targetsInRange[i].transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTarget = targetsInRange[i];
            }
        }

        return nearestTarget;
    }
}
