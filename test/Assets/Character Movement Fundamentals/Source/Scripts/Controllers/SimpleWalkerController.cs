using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CMF
{
    //A very simplified controller script;
    //This script is an example of a very simple walker controller that covers only the basics of character movement;
    public class SimpleWalkerController : Controller
    {
        //References to attached components;
        protected Transform tr;
        protected Mover mover;
        protected CharacterInput characterInput;
        protected CeilingDetector ceilingDetector;

        //Jump key variables;
        bool jumpInputIsLocked = false;
        bool jumpKeyWasPressed = false;
        bool jumpKeyWasLetGo = false;
        bool jumpKeyIsPressed = false;

        //Movement speed;
        public float movementSpeed = 7f;

        //How fast the controller can change direction while in the air;
        //Higher values result in more air control;
        public float airControlRate = 2f;

        //Jump speed;
        public float jumpSpeed = 10f;

        //Jump duration variables;
        public float jumpDuration = 0.2f;
        float currentJumpStartTime = 0f;

        //'AirFriction' determines how fast the controller loses its momentum while in the air;
        //'GroundFriction' is used instead, if the controller is grounded;
        public float airFriction = 0.5f;
        public float groundFriction = 100f;

        //Current momentum;
        protected Vector3 momentum = Vector3.zero;

        //Saved velocity from last frame;
        Vector3 savedVelocity = Vector3.zero;

        //Saved horizontal movement velocity from last frame;
        Vector3 savedMovementVelocity = Vector3.zero;

        //Amount of downward gravity;
        public float gravity = 30f;
        [Tooltip("How fast the character will slide down steep slopes.")]
        public float slideGravity = 5f;

        //Acceptable slope angle limit;
        public float slopeLimit = 80f;

        //Enum describing basic controller states; 
        public enum ControllerState
        {
            Grounded,
            Sliding,
            Falling,
            Rising,
            Jumping
        }

        ControllerState currentControllerState = ControllerState.Falling;

        [Tooltip("Optional camera transform used for calculating movement direction. If assigned, character movement will take camera view into account.")]
        public Transform cameraTransform;

        //Get references to all necessary components;
        void Awake()
        {
            mover = GetComponent<Mover>();
            tr = transform;
            characterInput = GetComponent<CharacterInput>();
            ceilingDetector = GetComponent<CeilingDetector>();

            if (characterInput == null)
                Debug.LogWarning("No character input script has been attached to this gameobject", this.gameObject);

            Setup();
        }

        //This function is called right after Awake(); It can be overridden by inheriting scripts;
        protected virtual void Setup()
        {
        }

        void Update()
        {
            HandleJumpKeyInput();
        }

        //Handle jump booleans for later use in FixedUpdate;
        void HandleJumpKeyInput()
        {
            bool _newJumpKeyPressedState = IsJumpKeyPressed();

            if (jumpKeyIsPressed == false && _newJumpKeyPressedState == true)
                jumpKeyWasPressed = true;

            if (jumpKeyIsPressed == true && _newJumpKeyPressedState == false)
            {
                jumpKeyWasLetGo = true;
                jumpInputIsLocked = false;
            }

            jumpKeyIsPressed = _newJumpKeyPressedState;
        }

        void FixedUpdate()
        {
            ControllerUpdate();
        }

        //Update controller;
        //This function must be called every fixed update, in order for the controller to work correctly;
        void ControllerUpdate()
        {
            //Check if mover is grounded;
            mover.CheckForGround();

            //Determine controller state;
            currentControllerState = DetermineControllerState();

            //Apply friction and gravity to 'momentum';
            HandleMomentum();

            //Check if the player has initiated a jump;
            HandleJumping();

            //Calculate movement velocity;
            Vector3 _velocity = Vector3.zero;
            if (currentControllerState == ControllerState.Grounded)
                _velocity = CalculateMovementVelocity();

            //If local momentum is used, transform momentum into world space first;
            Vector3 _worldMomentum = momentum;

            //Add current momentum to velocity;
            _velocity += _worldMomentum;

            //If player is grounded or sliding on a slope, extend mover's sensor range;
            //This enables the player to walk up/down stairs and slopes without losing ground contact;
            mover.SetExtendSensorRange(IsGrounded());

            //Set mover velocity;		
            mover.SetVelocity(_velocity);

            //Store velocity for next frame;
            savedVelocity = _velocity;

            //Save controller movement velocity;
            savedMovementVelocity = CalculateMovementVelocity();

            //Reset jump key booleans;
            jumpKeyWasLetGo = false;
            jumpKeyWasPressed = false;

            //Reset ceiling detector, if one is attached to this gameobject;
            if (ceilingDetector != null)
                ceilingDetector.ResetFlags();
        }

        //Calculate and return movement direction based on player input;
        //This function can be overridden by inheriting scripts to implement different player controls;
        protected virtual Vector3 CalculateMovementDirection()
        {
            //If no character input script is attached to this object, return;
            if (characterInput == null)
                return Vector3.zero;

            Vector3 _velocity = Vector3.zero;

            //If no camera transform has been assigned, use the character's transform axes to calculate the movement direction;
            if (cameraTransform == null)
            {
                _velocity += tr.right * characterInput.GetHorizontalMovementInput();
                _velocity += tr.forward * characterInput.GetVerticalMovementInput();
            }
            else
            {
                //If a camera transform has been assigned, use the assigned transform's axes for movement direction;
                //Project movement direction so movement stays parallel to the ground;
                _velocity += Vector3.ProjectOnPlane(cameraTransform.right, tr.up).normalized * characterInput.GetHorizontalMovementInput();
                _velocity += Vector3.ProjectOnPlane(cameraTransform.forward, tr.up).normalized * characterInput.GetVerticalMovementInput();
            }

            //If necessary, clamp movement vector to magnitude of 1f;
            if (_velocity.magnitude > 1f)
                _velocity.Normalize();

            return _velocity;
        }

        //Calculate and return movement velocity based on player input, controller state, ground normal [...];
        protected virtual Vector3 CalculateMovementVelocity()
        {
            //Calculate (normalized) movement direction;
            Vector3 _velocity = CalculateMovementDirection();

            //Multiply (normalized) velocity with movement speed;
            _velocity *= movementSpeed;

            return _velocity;
        }

        //Returns 'true' if the player presses the jump key;
        protected virtual bool IsJumpKeyPressed()
        {
            //If no character input script is attached to this object, return;
            if (characterInput == null)
                return false;

            return characterInput.IsJumpKeyPressed();
        }

        //Determine current controller state based on current momentum and whether the controller is grounded (or not);
        //Handle state transitions;
        ControllerState DetermineControllerState()
        {
            //Check if vertical momentum is pointing upwards;
            bool _isRising = IsRisingOrFalling() && (VectorMath.GetDotProduct(GetMomentum(), tr.up) > 0f);
            //Check if controller is sliding;
            bool _isSliding = mover.IsGrounded() && IsGroundTooSteep();

            //Grounded;
            if (currentControllerState == ControllerState.Grounded)
            {
                if (_isRising)
                {
                    OnGroundContactLost();
                    return ControllerState.Rising;
                }
                if (!mover.IsGrounded())
                {
                    OnGroundContactLost();
                    return ControllerState.Falling;
                }
                if (_isSliding)
                {
                    OnGroundContactLost();
                    return ControllerState.Sliding;
                }
                return ControllerState.Grounded;
            }

            //Falling;
            if (currentControllerState == ControllerState.Falling)
            {
                if (_isRising)
                {
                    return ControllerState.Rising;
                }
                if (mover.IsGrounded() && !_isSliding)
                {
                    OnGroundContactRegained();
                    return ControllerState.Grounded;
                }
                if (_isSliding)
                {
                    return ControllerState.Sliding;
                }
                return ControllerState.Falling;
            }

            //Sliding;
            if (currentControllerState == ControllerState.Sliding)
            {
                if (_isRising)
                {
                    OnGroundContactLost();
                    return ControllerState.Rising;
                }
                if (!mover.IsGrounded())
                {
                    OnGroundContactLost();
                    return ControllerState.Falling;
                }
                if (mover.IsGrounded() && !_isSliding)
                {
                    OnGroundContactRegained();
                    return ControllerState.Grounded;
                }
                return ControllerState.Sliding;
            }

            //Rising;
            if (currentControllerState == ControllerState.Rising)
            {
                if (!_isRising)
                {
                    if (mover.IsGrounded() && !_isSliding)
                    {
                        OnGroundContactRegained();
                        return ControllerState.Grounded;
                    }
                    if (_isSliding)
                    {
                        return ControllerState.Sliding;
                    }
                    if (!mover.IsGrounded())
                    {
                        return ControllerState.Falling;
                    }
                }

                //If a ceiling detector has been attached to this gameobject, check for ceiling hits;
                if (ceilingDetector != null)
                {
                    if (ceilingDetector.HitCeiling())
                    {
                        OnCeilingContact();
                        return ControllerState.Falling;
                    }
                }
                return ControllerState.Rising;
            }

            //Jumping;
            if (currentControllerState == ControllerState.Jumping)
            {
                //Check for jump timeout;
                if ((Time.time - currentJumpStartTime) > jumpDuration)
                    return ControllerState.Rising;

                //Check if jump key was let go;
                if (jumpKeyWasLetGo)
                    return ControllerState.Rising;

                //If a ceiling detector has been attached to this gameobject, check for ceiling hits;
                if (ceilingDetector != null)
                {
                    if (ceilingDetector.HitCeiling())
                    {
                        OnCeilingContact();
                        return ControllerState.Falling;
                    }
                }
                return ControllerState.Jumping;
            }

            return ControllerState.Falling;
        }

        //Check if player has initiated a jump;
        void HandleJumping()
        {
            if (currentControllerState == ControllerState.Grounded)
            {
                if ((jumpKeyIsPressed == true || jumpKeyWasPressed) && !jumpInputIsLocked)
                {
                    //Call events;
                    OnGroundContactLost();
                    OnJumpStart();

                    currentControllerState = ControllerState.Jumping;
                }
            }
        }

        //Apply friction to both vertical and horizontal momentum based on 'friction' and 'gravity';
        //Handle movement in the air;
        //Handle sliding down steep slopes;
        void HandleMomentum()
        {
            
            Vector3 _verticalMomentum = Vector3.zero;
            Vector3 _horizontalMomentum = Vector3.zero;

            //Split momentum into vertical and horizontal components;
            if (momentum != Vector3.zero)
            {
                _verticalMomentum = VectorMath.ExtractDotVector(momentum, tr.up);
                _horizontalMomentum = momentum - _verticalMomentum;
            }

            //Add gravity to vertical momentum;
            _verticalMomentum -= tr.up * gravity * Time.deltaTime;

            //Remove any downward force if the controller is grounded;
            if (currentControllerState == ControllerState.Grounded && VectorMath.GetDotProduct(_verticalMomentum, tr.up) < 0f)
                _verticalMomentum = Vector3.zero;

            //Manipulate momentum to steer controller in the air (if controller is not grounded or sliding);
            if (!IsGrounded())
            {
                Vector3 _movementVelocity = CalculateMovementVelocity();

                //If controller has received additional momentum from somewhere else;
                if (_horizontalMomentum.magnitude > movementSpeed)
                {
                    //Prevent unwanted accumulation of speed in the direction of the current momentum;
                    if (VectorMath.GetDotProduct(_movementVelocity, _horizontalMomentum.normalized) > 0f)
                        _movementVelocity = VectorMath.RemoveDotVector(_movementVelocity, _horizontalMomentum.normalized);

                    //Lower air control slightly with a multiplier to add some 'weight' to any momentum applied to the controller;
                    float _airControlMultiplier = 0.25f;
                    _horizontalMomentum += _movementVelocity * Time.deltaTime * airControlRate * _airControlMultiplier;
                }
                //If controller has not received additional momentum;
                else
                {
                    //Clamp _horizontal velocity to prevent accumulation of speed;
                    _horizontalMomentum += _movementVelocity * Time.deltaTime * airControlRate;
                    _horizontalMomentum = Vector3.ClampMagnitude(_horizontalMomentum, movementSpeed);
                }
            }

            //Steer controller on slopes;
            if (currentControllerState == ControllerState.Sliding)
            {
                //Calculate vector pointing away from slope;
                Vector3 _pointDownVector = Vector3.ProjectOnPlane(mover.GetGroundNormal(), tr.up).normalized;

                //Calculate movement velocity;
                Vector3 _slopeMovementVelocity = CalculateMovementVelocity();
                //Remove all velocity that is pointing up the slope;
                _slopeMovementVelocity = VectorMath.RemoveDotVector(_slopeMovementVelocity, _pointDownVector);

                //Add movement velocity to momentum;
                _horizontalMomentum += _slopeMovementVelocity * Time.fixedDeltaTime;
            }

            //Apply friction to horizontal momentum based on whether the controller is grounded;
            if (currentControllerState == ControllerState.Grounded)
                _horizontalMomentum = VectorMath.IncrementVectorTowardTargetVector(_horizontalMomentum, groundFriction, Time.deltaTime, Vector3.zero);
            else
                _horizontalMomentum = VectorMath.IncrementVectorTowardTargetVector(_horizontalMomentum, airFriction, Time.deltaTime, Vector3.zero);

            //Add horizontal and vertical momentum back together;
            momentum = _horizontalMomentum + _verticalMomentum;

            //Additional momentum calculations for sliding;
            if (currentControllerState == ControllerState.Sliding)
            {
                //Project the current momentum onto the current ground normal if the controller is sliding down a slope;
                momentum = Vector3.ProjectOnPlane(momentum, mover.GetGroundNormal());

                //Remove any upwards momentum when sliding;
                if (VectorMath.GetDotProduct(momentum, tr.up) > 0f)
                    momentum = VectorMath.RemoveDotVector(momentum, tr.up);

                //Apply additional slide gravity;
                Vector3 _slideDirection = Vector3.ProjectOnPlane(-tr.up, mover.GetGroundNormal()).normalized;
                momentum += _slideDirection * slideGravity * Time.deltaTime;
            }

            //If controller is jumping, override vertical velocity with jumpSpeed;
            if (currentControllerState == ControllerState.Jumping)
            {
                momentum = VectorMath.RemoveDotVector(momentum, tr.up);
                momentum += tr.up * jumpSpeed;
            }

        }

        //Events;

        //This function is called when the player has initiated a jump;
        void OnJumpStart()
        {
            
            //Add jump force to momentum;
            momentum += tr.up * jumpSpeed;

            //Set jump start time;
            currentJumpStartTime = Time.time;

            //Lock jump input until jump key is released again;
            jumpInputIsLocked = true;

            //Call event;
            if (OnJump != null)
                OnJump(momentum);

        }

        //This function is called when the controller has lost ground contact, i.e. is either falling or rising, or generally in the air;
        void OnGroundContactLost()
        {

            //Get current movement velocity;
            Vector3 _velocity = GetMovementVelocity();

            //Check if the controller has both momentum and a current movement velocity;
            if (_velocity.sqrMagnitude >= 0f && momentum.sqrMagnitude > 0f)
            {
                //Project momentum onto movement direction;
                Vector3 _projectedMomentum = Vector3.Project(momentum, _velocity.normalized);
                //Calculate dot product to determine whether momentum and movement are aligned;
                float _dot = VectorMath.GetDotProduct(_projectedMomentum.normalized, _velocity.normalized);

                //If current momentum is already pointing in the same direction as movement velocity,
                //Don't add further momentum (or limit movement velocity) to prevent unwanted speed accumulation;
                if (_projectedMomentum.sqrMagnitude >= _velocity.sqrMagnitude && _dot > 0f)
                    _velocity = Vector3.zero;
                else if (_dot > 0f)
                    _velocity -= _projectedMomentum;
            }

            //Add movement velocity to momentum;
            momentum += _velocity;

        }

        //This function is called when the controller has landed on a surface after being in the air;
        void OnGroundContactRegained()
        {
            //Call 'OnLand' event;
            if (OnLand != null)
            {
                Vector3 _collisionVelocity = momentum;
      

                OnLand(_collisionVelocity);
            }

        }

        //This function is called when the controller has collided with a ceiling while jumping or moving upwards;
        void OnCeilingContact()
        {

            //Remove all vertical parts of momentum;
            momentum = VectorMath.RemoveDotVector(momentum, tr.up);

        }

        //Helper functions;

        //Returns 'true' if vertical momentum is above a small threshold;
        private bool IsRisingOrFalling()
        {
            //Calculate current vertical momentum;
            Vector3 _verticalMomentum = VectorMath.ExtractDotVector(GetMomentum(), tr.up);

            //Setup threshold to check against;
            //For most applications, a value of '0.001f' is recommended;
            float _limit = 0.001f;

            //Return true if vertical momentum is above '_limit';
            return (_verticalMomentum.magnitude > _limit);
        }

        //Returns true if angle between controller and ground normal is too big (> slope limit), i.e. ground is too steep;
        private bool IsGroundTooSteep()
        {
            if (!mover.IsGrounded())
                return true;

            return (Vector3.Angle(mover.GetGroundNormal(), tr.up) > slopeLimit);
        }

        //Getters;

        //Get last frame's velocity;
        public override Vector3 GetVelocity()
        {
            return savedVelocity;
        }

        //Get last frame's movement velocity (momentum is ignored);
        public override Vector3 GetMovementVelocity()
        {
            return savedMovementVelocity;
        }

        //Get current momentum;
        public Vector3 GetMomentum()
        {
            Vector3 _worldMomentum = momentum;
            
            return _worldMomentum;
        }

        //Returns 'true' if controller is grounded (or sliding down a slope);
        public override bool IsGrounded()
        {
            return (currentControllerState == ControllerState.Grounded || currentControllerState == ControllerState.Sliding);
        }

        //Returns 'true' if controller is sliding;
        public bool IsSliding()
        {
            return (currentControllerState == ControllerState.Sliding);
        }

        //Add momentum to controller;
        public void AddMomentum(Vector3 _momentum)
        {
  
            momentum += _momentum;
        }

        //Set controller momentum directly;
        public void SetMomentum(Vector3 _newMomentum)
        {
            
                momentum = _newMomentum;
        }
    }

}

/**
 private Mover mover;
        float currentVerticalSpeed = 0f;
        bool isGrounded;
        public float movementSpeed = 7f;
        public float jumpSpeed = 10f;
        public float gravity = 10f;

		Vector3 lastVelocity = Vector3.zero;

		public Transform cameraTransform;
        CharacterInput characterInput;
        Transform tr;

        // Use this for initialization
        void Start()
        {
            tr = transform;
            mover = GetComponent<Mover>();
            characterInput = GetComponent<CharacterInput>();
        }

        void FixedUpdate()
        {
            //Run initial mover ground check;
            mover.CheckForGround();

            //If character was not grounded int the last frame and is now grounded, call 'OnGroundContactRegained' function;
            if(isGrounded == false && mover.IsGrounded() == true)
                OnGroundContactRegained(lastVelocity);

            //Check whether the character is grounded and store result;
            isGrounded = mover.IsGrounded();

            Vector3 _velocity = Vector3.zero;

            //Add player movement to velocity;
            _velocity += CalculateMovementDirection() * movementSpeed;
            
            //Handle gravity;
            if (!isGrounded)
            {
                currentVerticalSpeed -= gravity * Time.deltaTime;
            }
            else
            {
                if (currentVerticalSpeed <= 0f)
                    currentVerticalSpeed = 0f;
            }

            //Handle jumping;
            if ((characterInput != null) && isGrounded && characterInput.IsJumpKeyPressed())
            {
                OnJumpStart();
                currentVerticalSpeed = jumpSpeed;
                isGrounded = false;
            }

            //Add vertical velocity;
            _velocity += tr.up * currentVerticalSpeed;

			//Save current velocity for next frame;
			lastVelocity = _velocity;

            mover.SetExtendSensorRange(isGrounded);
            mover.SetVelocity(_velocity);
        }

        private Vector3 CalculateMovementDirection()
        {
            //If no character input script is attached to this object, return no input;
			if(characterInput == null)
				return Vector3.zero;

			Vector3 _direction = Vector3.zero;

			//If no camera transform has been assigned, use the character's transform axes to calculate the movement direction;
			if(cameraTransform == null)
			{
				_direction += tr.right * characterInput.GetHorizontalMovementInput();
				_direction += tr.forward * characterInput.GetVerticalMovementInput();
			}
			else
			{
				//If a camera transform has been assigned, use the assigned transform's axes for movement direction;
				//Project movement direction so movement stays parallel to the ground;
				_direction += Vector3.ProjectOnPlane(cameraTransform.right, tr.up).normalized * characterInput.GetHorizontalMovementInput();
				_direction += Vector3.ProjectOnPlane(cameraTransform.forward, tr.up).normalized * characterInput.GetVerticalMovementInput();
			}

			//If necessary, clamp movement vector to magnitude of 1f;
			if(_direction.magnitude > 1f)
				_direction.Normalize();

			return _direction;
        }

        //This function is called when the controller has landed on a surface after being in the air;
		void OnGroundContactRegained(Vector3 _collisionVelocity)
		{
			//Call 'OnLand' delegate function;
			if(OnLand != null)
				OnLand(_collisionVelocity);
		}

        //This function is called when the controller has started a jump;
        void OnJumpStart()
        {
            //Call 'OnJump' delegate function;
            if(OnJump != null)
                OnJump(lastVelocity);
        }

        //Return the current velocity of the character;
        public override Vector3 GetVelocity()
        {
            return lastVelocity;
        }

        //Return only the current movement velocity (without any vertical velocity);
        public override Vector3 GetMovementVelocity()
        {
            return lastVelocity;
        }

        //Return whether the character is currently grounded;
        public override bool IsGrounded()
        {
            return isGrounded;
        }

    }
**/