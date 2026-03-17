using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class TowerRange : MonoBehaviour
{
    [SerializeField] private Tower tower;
    private List<GameObject> targets = new List<GameObject>();

    void Start()
    {
        if (tower == null)
        {
            tower = GetComponentInParent<Tower>();
        }

        UpdateRange();
    }

    private void OnEnable()
    {
        if (tower == null)
        {
            tower = GetComponentInParent<Tower>();
        }

        UpdateRange();
    }

    private void OnValidate()
    {
        if (tower == null)
        {
            tower = GetComponentInParent<Tower>();
        }

        UpdateRange();
    }

    void Update()
    {
        KeepRangeCentered();

        if (tower == null)
        {
            return;
        }

        targets.RemoveAll(target => target == null);

        if (targets.Count > 0)
        {
            tower.target = GetNearestTarget();
        }
        else
        {
            tower.target = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.GameObject().CompareTag("Enemy"))
        {
            targetsInRange.Add(collision.GameObject());
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.GameObject().CompareTag("Enemy"))
        {
            targetsInRange.Remove(collision.GameObject());
        }
    }

    public void UpdateRange()
    {
        if (tower == null)
        {
            return;
        }

        KeepRangeCentered();
        transform.localScale = new Vector3(tower.Range, tower.Range, tower.Range);
    }

    private void LateUpdate()
    {
        KeepRangeCentered();
    }

    private void KeepRangeCentered()
    {
        if (tower == null)
        {
            return;
        }

        transform.position = tower.transform.position;
        transform.rotation = Quaternion.identity;
    }

    private GameObject GetNearestTarget()
    {
        GameObject nearestTarget = targets[0];
        float nearestDistance = Vector2.Distance(tower.transform.position, nearestTarget.transform.position);

        for (int i = 1; i < targets.Count; i++)
        {
            float distance = Vector2.Distance(tower.transform.position, targets[i].transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTarget = targets[i];
            }
        }

        return nearestTarget;
    }
}
