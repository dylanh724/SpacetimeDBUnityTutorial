using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;
using SpacetimeDB;

public class LocalPlayer : MonoBehaviour
{
    [SerializeField] private GameObject cameraRig;
    [SerializeField] private int movementUpdateSpeed;

    public static LocalPlayer instance;

    // Start is called before the first frame update
    void Start()
    {
        instance = this;
        cameraRig.SetActive(true);
        PlayerMovementController.Local = GetComponent<PlayerMovementController>();
        PlayerAnimator.Local = GetComponentInChildren<PlayerAnimator>(true);        
    }
    
    private float? lastUpdateTime;
    private void FixedUpdate()
    {
        if ((lastUpdateTime.HasValue && Time.time - lastUpdateTime.Value > 1.0f / movementUpdateSpeed) || !SpacetimeDBClient.instance.IsConnected())
        {
            return;
        }

        lastUpdateTime = Time.time;
        var p = PlayerMovementController.Local.GetModelPosition();
        Reducer.UpdatePlayerPosition(new StdbVector3
            {
                X = p.x,
                Y = p.y,
                Z = p.z,
            },
            PlayerMovementController.Local.GetModelRotation(),
            PlayerMovementController.Local.IsMoving());
    }
}
