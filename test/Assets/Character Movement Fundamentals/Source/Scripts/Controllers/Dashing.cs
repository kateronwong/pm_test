using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using CMF;

namespace CMF
{

    public class Dashing : MonoBehaviour
    {
        [Header("Dashing")]
        [SerializeField] private float dashForce;
        [SerializeField] private float dashDuration;
        private Vector3 forceToApply;

        [Header("Cooldown")]
        [SerializeField] private float dashCD;
        private float dashCDTimer;
        private bool inCD;

        [Header("References")]
        [SerializeField] private Transform playerCamera;
        private Rigidbody rb;
        private SimpleWalkerController simpleWalkerController;

        private bool DashPressed;
        public bool dashing;

        public KeyCode dashKey = KeyCode.LeftShift;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            simpleWalkerController = GetComponent<SimpleWalkerController>();
        }

        private void Start()
        {

        }

        private void Update()
        {

            if (dashCDTimer > 0)
            {
                inCD = true;
                dashCDTimer -= Time.deltaTime;
            }
            else
            {
                inCD = false;
            }


            if (IsDashKeyPressed() && !inCD)
            {
                Dash();
            }
        }

        private void Dash()
        {
            if (dashCDTimer > 0) return;
            else dashCDTimer = dashCD;

            dashing = true;


            Vector3 direction = CaculateDirection();

            forceToApply = direction * dashForce;

            Invoke(nameof(DelayDash), 0.025f);

            Invoke(nameof(ResetDash), dashDuration);
        }

        private void ResetDash()
        {
            dashing = false;

            DashPressed = false;
        }

        private void DelayDash()
        {
            simpleWalkerController.AddMomentum(forceToApply);
            
        }

        private Vector3 CaculateDirection()
        {
            Vector3 direction = new Vector3();

            direction = playerCamera.forward;



            return direction.normalized;
        }

        public void OnDash(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                if (!DashPressed)
                    DashPressed = true;
            }
            else if (context.canceled)
            {
                DashPressed = false;
            }
        }

        public bool IsDashKeyPressed()
        {
            return Input.GetKey(dashKey);
        }
    }

}