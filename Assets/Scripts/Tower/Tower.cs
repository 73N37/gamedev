using UnityEngine;

public class Tower : MonoBehaviour
{

    public float Range = 8f;
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
            if(fireCooldown >= fireRate)
            {
                transform.right = target.transform.position - transform.position;
                
                target.GetComponent<Enemy>().TakeDamage(damage);
                fireCooldown = 0f;
            }
            else
            {
                fireCooldown += 1 * Time.deltaTime;
            }
        }   
    }
}
