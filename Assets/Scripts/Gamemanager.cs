using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Gamemanager : MonoBehaviour
{
    public static Gamemanager instance;

    [SerializeField]
    GameObject textBox;

    public GameObject TextBox { get => textBox;}

    void Awake()
    {
        instance = this;
    }
}
