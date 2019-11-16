using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Movement;


public class Player : Character
{

    Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public override void Move()
    {
        base.Move();

        animator.SetFloat("move", Mathf.Abs(Movement.Axis.magnitude));
    }
}
