using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Movement;

public class Character : MonoBehaviour
{
    [SerializeField]
    protected float moveSpeed;
   
   void Update()
   {
       Move();
   }

   public virtual void Move()
   {
       Movement.Move(transform, moveSpeed, Movement.AxisDelta);
   }
}
