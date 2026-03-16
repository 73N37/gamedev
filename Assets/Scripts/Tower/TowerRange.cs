using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TowerRange : MonoBehaviour
{

    [SerializeField] private Tower Tower;

    private List<GameObject> targetsInRange = new List<GameObject>();
    void Start()
    {
        UpdateRange();
    }

    void Update()
    {
        if (targetsInRange.Count > 0)
        {
            Tower.target = targetsInRange[0];
        }
        else
        {
            Tower.target = null;
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
        transform.localScale = new Vector3(Tower.range, Tower.range, Tower.range);
    }
}
