using UnityEngine;

public class Tower : MonoBehaviour
{

    public float range = 8f;
    public int damage = 1;
    public float fireRate = 1f;
    private float fireCooldown = 0f;

    public GameObject target;
    void Start()
    {
        
    }

    void Update()
    {
        if(target)
        {
            transform.right = target.transform.position - transform.position;
        }   
    }
}
