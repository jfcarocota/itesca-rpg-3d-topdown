using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Movement
{
    public static class Movement
    {
        public static Vector3 Axis {get => new Vector3(Input.GetAxis("Horizontal"), 0,Input.GetAxis("Vertical"));}

        public static Vector3 AxisDelta 
        {
            get => new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * Time.deltaTime;
        }

        public static void Move(Transform t, float moveSpeed, Vector3 dir)
        {
            t.Translate(dir * moveSpeed);
        }

        public static void Move3DTopDown(Transform t, float moveSpeed, Vector3 dir)
        {
            t.Translate(Vector3.forward * moveSpeed * dir.magnitude);
            if(dir != Vector3.zero)
            {
                t.rotation = Quaternion.LookRotation(dir);
            }
        }
    }
}
