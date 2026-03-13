using System.Collections.Generic;
using UnityEngine;

public class TowerRange : MonoBehaviour
{
    [SerializeField] private Tower tower;
    private List<GameObject> targets = new List<GameObject>();

    void Start()
    {
        UpdateRange();
    }

    void Update()
    {
        
    }

    public void UpdateRange()
    {
        transform.localScale = new Vector3(tower.Range, tower.Range, tower.Range);
    }
}
