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

    bool canTalk;

    public bool CanTalk { get => canTalk; set => canTalk = value; }

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        base.Update();
    }

    public override void Move()
    {
        base.Move();

        animator.SetFloat("move", Mathf.Abs(Movement.Axis.magnitude));
    }

    void OnTriggerStay(Collider col)
    {
        if(col.CompareTag("NPC"))
        {
            Gamemanager.instance.TextBox.SetActive(true);
        }
    }

    
    void OnTriggerExit(Collider col)
    {
        if(col.CompareTag("NPC"))
        {
            Gamemanager.instance.TextBox.SetActive(false);        
        }
    }
}
