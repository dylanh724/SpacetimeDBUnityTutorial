using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class PlayerMovementController : MonoBehaviour
{
    [SerializeField] private Transform modelTransform;
    [SerializeField] private float movementSpeed;
    [SerializeField] private float modelTurnSpeed;
    [SerializeField] private Animator anim;

    public float RemoteTargetRotation;
    public Vector3 RemoteTargetPosition;
    private Vector2 localMoveDirection;

    private bool interacting;
    private bool isMoving;

    public static CallbackBool localMovementDisabled = new CallbackBool(CallbackBool.Mode.Or);
    private Rigidbody body;
    private static readonly int WalkingProperty = Animator.StringToHash("Walking");

    public static PlayerMovementController Local;

    protected void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = false;
        localMovementDisabled.Add(() => interacting);
        GetComponentInChildren<PlayerAnimator>(true).OnInteractionUpdate += OnInteractionUpdate;
    }

    private void OnDestroy()
    {
        GetComponentInChildren<PlayerAnimator>(true).OnInteractionUpdate -= OnInteractionUpdate;
    }
    
    public Vector3 GetModelPosition() => modelTransform.position;
    public float GetModelRotation() => modelTransform.rotation.eulerAngles.y;
    public bool IsMoving() => isMoving;


    public Transform GetModelTransform() => modelTransform;
    void OnInteractionUpdate(bool interacting) => this.interacting = interacting;

    private void FixedUpdate()
    {
        if (Local == this)
        {
            if (localMoveDirection.magnitude == 0 || localMovementDisabled.Invoke() || !CameraController.instance.GameCameraEnabled)
            {
                SetMoving(false);
                return;
            }
            
            SetMoving(true);
            var vec = new Vector3(localMoveDirection.x, 0.0f, localMoveDirection.y);
            var transformed = CameraController.instance.transform.TransformDirection(vec).normalized;
            body.MovePosition(body.position + new Vector3(transformed.x, 0.0f, transformed.z) * (Time.fixedDeltaTime * movementSpeed));
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, 
                Quaternion.LookRotation(transformed, Vector3.up), modelTurnSpeed * Time.fixedDeltaTime));
        }
        else
        {
            body.MovePosition(RemoteTargetPosition);
            body.rotation = Quaternion.Euler(0.0f, RemoteTargetRotation, 0.0f);
        }
    }
    

    public void SetMoving(bool moving)
    {
        isMoving = moving;
        anim.SetBool(WalkingProperty, moving);
    }

    public void LocalMove(Vector2 direction)
    {
        if (this != Local)
        {
            return;
        }

        localMoveDirection = direction;
    }
}
