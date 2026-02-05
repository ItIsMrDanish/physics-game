using System.Collection;
using System.Collection.Generic;
using UnityEngine;

public class Enemie : MonoBehaviour
{
    public float MaxSpeed;
    private float Speed;

    private Collider[] hitColliders;
    private RaycastHit Hit;

    public float SightRange;
    public float DetectionRange;
    
    public Rigidbody rb;
    public GameObject Target;

    private bool seePlayer;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       
        
        if (!seePlayer)
        {
            hitColliders = Physics.OverlapsSphere(transform.position, DetectionRange);
            foreach (var HitCollider in  hitColliders)
            {
                if(HitCollider.tag == "Player")
                {
                    Target = HitCollider.gameObject;
                    seePlayer = true;
                }
            }
        }
        else
        {
            if(physics.Raycast(transform.position, (Target.tranform.position - transform.position), out Hit, SightRange))
            {
                if(Hit.collider.tag!= "Player")
                {
                    seePlayer = false;
                }
                else
                {


                }
            }
        }       
    }
}
