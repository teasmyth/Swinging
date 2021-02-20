using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    // To make the script easier to understand, it has been divided into regions according to their task. To ease it further, here is a little timeline of how the script operates:
    // 1. Initial check if the tags had been set correctly in the inspector.
    // 2. Early Process Ability - determining whether we are using hook or rope and setting the parameters and references accordingly. Sets permission.
    // 3. Process Ability - if permitted, enables the ability.
    // 3a. Translate input into swing force to set on the controller.
    // 3b. All the safety functions, to prevent anything going wrong. Except collision protection because it gets direction from swing force.
    // 4. Handle Input - Detects the input for leaving the swinging state, and detaches the players accordingly
    // 4a. Setting the character back to its original state.
    // 4b. Depending on the mode, determine the rope/hook's "fate".
    // 4c. Removing swinging permissions.
    // Repeat

    // Because enums cannot be extended, for this case only, adding "Swinging" state is important as a state, so other abilities cannot be used while swinging. However, it is not mandatory, just comment out those lines that change states.

    // Wonderfurl world of TODOs:
    //TODO make truephysics, the pendulum, a bit faster, reaches 0 very slowly, possibly never.
    //TODO projectile drawing glitch, possibly only for hooks.
    //TODO auto swing mode.
    //TODO ropes in general LOL.
    //TODO spider mode, although works in theory, cant test due to lack of ropes, lmao.
    //TODO swing threshold basically not working. not even used anywhere wtf. Don't remember why I took it out and what it was, lazy and lack the time to redo it. Maybe a 'virtual' collision protection could do it.
    //TODO tidy up the mode detection part, dont need enums here, mode is only used for target setting. Could be a simple if else in the earlyprocess().
    //TODO swing correction right now freezes the character, maybe should revert it back to pos -= v3 changedir. !! but manually setting position is bad and dangerous!!!!


    /// <summary>
    /// Swinging ability, whether by graplling hook or swinging on a rope.
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Abilities/Character Swinging")]
    public class CharacterSwinging : CharacterAbility
    {
        public override string HelpBoxText() { return "Requires a grappling hook projectile & two tags to work."; }
        [Space(5)]
        [Header("Tags")]
        [Tooltip("Grappling hook's projectile's tag to look for.")]
        public string hookTag = "Hook";
        [Tooltip("Ropes' tags to look for, to correctly identify ropes.")]
        public string ropeTag = "Rope";

        [Space(5)]
        [Header("Physics")]
        public float gravity = 30f;
        [Tooltip("Due to calculation, gravity acts weak when swinging is idle. This compensates it by multiplying it when there is no input")]
        public float gravityMultiplier = 8f;
        protected float gravityMultiplied { get { return gravity * gravityMultiplier; } }
        [Tooltip("If enabled, when there is no player input, the character swings and slow downs like a pentulum, instead of stopping at the maximum distance to the target object.")]
        public bool truePhysics = true; // Gets disabled when the target is too close (distance less than 3x the character's height). Remove that line in CollisionTriggered if you wanna see a sonic speed swinging.
        [Tooltip("Maximum swing speed.")]
        public float swingSpeed = 30f;
        [Tooltip("How much time should it take to reach the maximum swing speed.")]
        public float accelerationTime = 1.5f;

        [Space(5)]
        [Header("Collision Protection")]
        [Tooltip("Defines how high can the character swing relative to the hook's height. Going higher causes bugs, so a small negative number is strongly recommended.")]
        public float swingHeightThreshold = -0.5f;
        [Tooltip("Preventing character to swing into stuff with full force - but looks funny combined with high swing speed if you disable it :D")]
        public bool collisionSafety = true;
        [Tooltip("Distance (in float) to the potential collider where collision safety gets triggered and start slowing down the swing. Cannot be negative.")]
        [Range(0, Mathf.Infinity)] public float collisionSensitivity = 1f;
        [Tooltip("The character needs a Health component. Prevents death if collision safety fails, making the character invulnerable when touching the collider.")]
        public bool TemporaryInvulnerability;
        [Tooltip("Tries to prevent getting stuck, sticking to a wall. Normally, should be impossible to trigger due to collision protection preventing collision and this only triggers if there is collision.")]
        public bool stuckPrevention = true;
        [Tooltip("If for some reason another force is set on the character for example from another ability that should be disabled is enabled, it will *try* to correct any extraordinary forces.")]
        public bool swingCorrection;

        //Variables
        protected bool attached; // THE ULTIMATE PERMISSION TO SWING.
        protected bool hit;
        protected bool axisModeSwitched;
        protected bool modeSet;

        protected float distanceToTarget;
        protected float runTime;
        protected float counterTimeRight;
        protected float counterTimeLeft;
        protected float runTimeAxis;

        protected int collisionSafetyCounter;

        protected CharacterStates.MovementStates previousState;
        protected Vector2 swingForceNow;
        protected CharacterPosition charpos;
        protected GameObject target;
        protected Direction dirAxis;
        protected enum mode { Hook, Rope };
        protected mode currentMode;

        ///
        //[Space(10)]
        //[Header("Experimental")]
        //[Tooltip("If enabled, character leaves the rope behind after leaving the swinging state, which will act as a rope until its projectile lifetime reaches 0, only then disappear.")]
        //public bool spiderMode = false;
        //public int ropeStorage;
        //protected bool autoSwing; //For later, especially for spider mode.

        protected override void Initialization()
        {
            base.Initialization();
            if (hookTag == "" || ropeTag == "")
            {
                MMDebug.DebugLogTime("Grappling Ability: No rope or hook tag found.");
                PermitAbility(false);
            }
            modeSet = false;
            attached = false;
        }

        #region Mode setting && Permissions
        /// <summary>
        /// Basically EarlyUpdate(). Check Character() for more information.
        /// </summary>
        public override void EarlyProcessAbility()
        {
            base.EarlyProcessAbility();
            if (AbilityAuthorized)
            {
                runTimeAxis = RunTimeAxis();
                DetectMode();
                SwitchMode(currentMode);
            }
        }
        /// <summary>
        /// Detects which mode to use.
        /// </summary>
        protected void DetectMode() //might just do 'return'
        {
            if (!modeSet)
            {
                if (gameObject.transform.parent != null)
                {
                    if (gameObject.transform.parent.CompareTag("Rope"))
                    {
                        currentMode = mode.Rope;
                    }
                }
                else currentMode = mode.Hook;
                modeSet = true;
            }
        }
        /// <summary>
        /// Switches to mode detected by DetectMode() passed as <paramref name="mode"/> and sets the target object to get references from.
        /// </summary>
        protected void SwitchMode(mode mode)
        {
            if (!attached)
            {
                if (mode == mode.Hook)
                {
                    if (GameObject.FindGameObjectWithTag(hookTag) != null)
                    {
                        target = GameObject.FindGameObjectWithTag(hookTag);
                        hit = target.GetComponent<RopeProjectile>().hit;
                        if (!_controller.State.IsGrounded && hit)
                        {
                            CollisionTriggered();
                        }
                    }
                }
                else if (mode == mode.Rope)
                {
                    if (!_controller.State.IsGrounded)
                    {
                        target = gameObject.transform.parent.gameObject;
                        CollisionTriggered();
                    }
                }
            }
        }
        /// <summary>
        /// Triggered once when hook hits & swing is permitted. Sets initial direction, so we can compare it with the previous direction, to reset acceleration.
        /// </summary>
        protected void CollisionTriggered()
        {
            previousState = _movement.CurrentState;
            distanceToTarget = Vector2.Distance(target.transform.position, gameObject.transform.position);
            truePhysics = distanceToTarget < _controller.Height() * 3 ? false : true;
            _controller.GravityActive(false);
            _characterHorizontalMovement.AbilityPermitted = false;
            _movement.ChangeState(CharacterStates.MovementStates.Swinging);
            attached = true;
        }
        #endregion
        #region Input & Core
        /// <summary>
        /// Basically Update(), the core of the script. Check Character() for more information.
        /// </summary>
        public override void ProcessAbility()
        {
            base.ProcessAbility();
            if (attached)
            {
                InternalPermitAbility(true);
            }
        }
        /// <summary>
        /// Sets how should the ability function if it is permitted, and how when it is not. Pass in a bool to set the behaviour.
        /// </summary>
        /// Kind of acted weird when used the built-in permit ability, in case you wonder why I did not use that. 
        public void InternalPermitAbility(bool abilityPermitted)
        {
            if (abilityPermitted)
            {
                //need 'isSwinging' state, also bools whether it counts as a jump/flying/ etc
                _controller.SetForce(Motion(SwingInput(runTimeAxis), gameObject.transform.position, target.transform.position, distanceToTarget, _controller));
                SwingCorrection();
                StuckPrevention();
            }
            else
            {
                if (/*!spiderMode && */target != null)
                {
                    target.SetActive(false);
                    _movement.ChangeState(previousState);
                }
                //else if (target != null)
                //{
                //    target.tag = ropeTag;
                //    _movement.ChangeState(previousState);
                //}
                _characterHorizontalMovement.AbilityPermitted = true;
                _controller.GravityActive(true);
                modeSet = false;
                attached = false;
            }
        }
        /// <summary>
        /// Handling input, modify it any way to stop swinging. By default, it is mapped to Jump button. Add anything else inside 'attached'.
        /// </summary>
        protected override void HandleInput()
        {
            base.HandleInput();
            if (attached)
            {
                if (_inputManager.JumpButton.State.CurrentState == MMInput.ButtonStates.ButtonDown)
                {
                    InternalPermitAbility(false);
                }
                //else if (another condition)
                //{
                //    InternalPermitAbility(false);
                //}
            }
        }
        /// <summary>
        /// Automatically called when the character gets killed, in anticipation for its respawn.
        /// </summary>
        public override void ResetAbility()
        {
            base.ResetAbility();
            InternalPermitAbility(false);
        }
        #endregion
        #region Protection & Other safety measures.
        /// <summary>
        /// If stuck due to some reason, pushes the character just enough so it can move again. However should be impossible to reach this point due to other safety scripts.
        /// </summary>
        protected void StuckPrevention()
        {
            if (stuckPrevention)
            {
                charpos.now = gameObject.transform.position;
                Vector2 velocity = charpos.now - charpos.old;
                charpos.old = charpos.now;
                if (swingForceNow != Vector2.zero && velocity == Vector2.zero && _controller.State.HasCollisions)
                {
                    if (_controller.State.IsCollidingAbove)
                    {
                        _controller.SetForce(new Vector2(0, -1));
                    }
                    else if (_controller.State.IsCollidingBelow)
                    {
                        _controller.SetForce(new Vector2(0, 1));
                    }
                    else if (_controller.State.IsCollidingLeft)
                    {
                        _controller.SetForce(new Vector2(1, 0));
                    }
                    else if (_controller.State.IsCollidingRight)
                    {
                        _controller.SetForce(new Vector2(-1, 0));
                    }
                }
            }
        }
        /// <summary>
        ///If enabled, given a <paramref name="collisionSensitivity"/>, returns the distance to the potential collider as a multiplier to slow down the character as it gets closer. It only checks the direction the character is headed to. Also provides temporary invulnerability when very close to the collider, if that is enabled.
        /// </summary>
        ///<param name="direction"> Which direction to check. </param>
        protected float CollisionProtection(Vector2 direction)
        {
            if (collisionSafety)
            {
                BoxCollider2D col = _controller.GetComponent<BoxCollider2D>();
                RaycastHit2D[] hits = new RaycastHit2D[1];
                col.Cast(direction, hits, collisionSensitivity);
                bool invulSetPrior = _controller.GetComponent<Health>().TemporaryInvulnerable == true ? true : false; //If something else makes the character invulnerable at the moment before the script, then it doesn't mess with regargdless whether TemporaryInvulnerability checked or not.
                if (hits[0].collider != null)
                {
                    float hitdist = hits[0].distance;
                    if (hitdist <= collisionSensitivity)
                    {
                        if (collisionSafetyCounter == 0)
                        {
                            _controller.SetForce(Vector2.zero);
                        }
                        collisionSafetyCounter++;
                        if (hitdist <= .1f)
                        {
                            if (TemporaryInvulnerability && !invulSetPrior)
                            {
                                _controller.GetComponent<Health>().TemporaryInvulnerable = true;
                            }
                            hitdist = 0;
                        }
                        return hitdist;
                    }
                }
                else
                {
                    if (TemporaryInvulnerability && !invulSetPrior)
                    {
                        _controller.GetComponent<Health>().TemporaryInvulnerable = false;
                    }
                    collisionSafetyCounter = 0;
                    return 1;
                }
            }
            return 1;
        }
        /// <summary>
        /// In case unexpected force (such as dash) is set on the character, corrects the rope back to where it should be.
        /// </summary>
        /// <param name="targetObject"></param>
        /// 
        protected void SwingCorrection()
        {
            if (swingCorrection)
            {
                Vector3 charPos = gameObject.transform.position;
                Vector3 hookPos = target.transform.position;

                float distance = (charPos - hookPos).magnitude; //distance between two segments NOW
                float error = Mathf.Abs(distance - distanceToTarget); // absolute difference between the actual distance and the variable distance (ropeSegLen) of the segments
                Vector2 changeDir = Vector2.zero;

                if (distance > distanceToTarget)
                {
                    changeDir = (charPos - hookPos).normalized; // changeDir is positive (1) if the actual distance is greater than preset distance
                }
                else if (distance < distanceToTarget)
                {
                    changeDir = (hookPos - charPos).normalized; // changeDir is negative (-1) if preset distance is greater than actual distance
                }

                Vector2 changeAmount = changeDir * error;
                _controller.SetForce(changeAmount);
                //gameObject.transform.position -= (Vector3)changeDir;
            }
        }
        #endregion
        #region Motion & Swinging

        /// <summary>
        /// Compute a force that will redirect a character's speed into movement along a circle.
        /// </summary>
        /// <param name="characterPosition">The current position of the character in world space.</param>
        /// <param name="swingTarget">The position of the center of the swinging circle in world space.</param>
        /// <param name="swingDistance">The intended fixed distance between the character and the hook target.</param>
        /// <param name="controller">The character's controller.</param>
        /// <returns>A force that, if set on the character, will move them to a new position along the swinging circle relative to their current velocity. Slowed down by collision protection if enabled</returns>
        public virtual Vector2 Motion(float currentSpeed, Vector3 characterPosition, Vector3 swingTarget, float swingDistance, CorgiController controller)
        {
            // Direction vector from our character position to the swing target
            Vector3 swingTargetDirection = swingTarget - characterPosition;
            // Direction vector from the swing target to our current character position
            Vector3 characterDirection = characterPosition - swingTarget;

            // Swapping gravities if swinging is idle. If there is a multiplier of course.
            float internalGravity = runTimeAxis == 0 && !truePhysics ? gravityMultiplied : gravity;

            // Estimate what the character's velocity would have been this frame, after gravity is applied
            Vector3 characterVelocity = new Vector2(currentSpeed, internalGravity * Time.fixedDeltaTime);

            // The absolute angle of our character, relative to the swing target
            float characterAngle = Vector2.SignedAngle(Vector2.right, characterDirection.normalized) * Mathf.Deg2Rad;

            // angle between the character's current velocity and the target direction
            float ropeAngle = Vector2.SignedAngle(swingTargetDirection.normalized, characterVelocity.normalized) * Mathf.Deg2Rad;

            // compute a swingForce that will push the character towards the target direction such that redirects the character's current velocity to be tangential to the circle
            Vector3 swingForce = Mathf.Max(0, -Mathf.Cos(ropeAngle) * characterVelocity.magnitude) * swingTargetDirection.normalized;

            // What the character's velocity will be after we redirect it
            Vector3 adjustedVelocity = characterVelocity + swingForce;

            // Just using adjustedVelocity would work OK, but your ending position won't actually be on the circle
            // anymore, which causes small errors to accumulate as you swing, which gradually lengthen the "rope"

            // Instead, we can calculate how far around the circle we would have rotated, given the magnitude
            // of the adjusted velocity, and compute a new target position somewhere on the circle 

            // How many radians around the circle would we swing, if we used the magnitude of our adjusted velocity as an arc length?
            float adjustedArcAngle = (adjustedVelocity.magnitude * Time.deltaTime) / swingDistance;

            // Since the magnitude has no sign, we don't know whether to swing clockwise or counter-clockwise around the circle.
            // We can use our earlier adjusted velocity to predict where we would have ended up, and use that to guess the correct way to rotate.

            // Where we would have been, if we had just used our adjusted velocity...
            Vector3 adjustedPosition = characterPosition + adjustedVelocity * Time.deltaTime;
            // The direction vector pointing from the swing target to our newly adjusted position
            Vector3 adjustedDirection = adjustedPosition - swingTarget;
            // The angle value of the character's current position
            float adjustedAngle = Vector2.SignedAngle(Vector2.right, adjustedDirection.normalized) * Mathf.Deg2Rad;

            // The difference in angle around the circle between our original and adjusted positions
            float adjustedAngleDiff = adjustedAngle - characterAngle;

            // If the difference in angle between our adjusted and current position is negative, then we want to swing clockwise, otherwise we want to swing counter-clockwise
            if (adjustedAngleDiff < 0)
            {
                adjustedArcAngle *= -1;
            }

            // The current angle value, plus our offset angle from our projected velocity
            float targetAngle = characterAngle + adjustedArcAngle;

            // Direction vector from the hook target to the new position, based on our target angle value
            Vector3 targetDirection = Quaternion.AngleAxis(targetAngle * Mathf.Rad2Deg, Vector3.forward) * Vector3.right;

            // The new target position, which is the result of traveling along the target vector by our fixed hook distance
            Vector3 targetPosition = swingTarget + (targetDirection.normalized * swingDistance);

            // our target velocity, which is simply the difference between our target position and our current position, adjusted for the duration of time since last frame
            Vector3 finalVelocity = (targetPosition - characterPosition) / Time.deltaTime;

            // Adding the collision protection, it will return 1 without doing any calculations if it is disabled;
            swingForceNow = (Vector2)(CollisionProtection(finalVelocity) * finalVelocity);
            return CollisionProtection(finalVelocity) * finalVelocity;
        }
        /// <summary>
        /// Basic movement calculations with the input. Requires duration of the character's movement to work.
        /// </summary>
        /// <returns></returns>
        protected float SwingInput(float timeAxis)
        {
            float acceleration = swingSpeed / accelerationTime;
            float currentSpeed = acceleration * timeAxis;
            if (currentSpeed >= swingSpeed)
            {
                currentSpeed = swingSpeed;
            }
            else if (currentSpeed <= -swingSpeed)
            {
                currentSpeed = -swingSpeed;
            }
            return currentSpeed;
        }

        protected IEnumerator Hold(float time)
        {
            yield return new WaitForSeconds(time);
        }

        /// <summary>
        /// Resets acceleration every time direction is changed and multiplies it with direction, which it returns.
        /// </summary>
        protected float RunTimeAxis() 
        {
            if (truePhysics && Input.GetAxisRaw("Horizontal") == 0)
            {
                if (axisModeSwitched)
                {
                    counterTimeLeft = 0;
                    counterTimeRight = 0;
                    StartCoroutine(Hold(.08f)); //Instant switch did not feel right, so I added a very short delay but feel free to remove.
                }
                axisModeSwitched = false;
                float distance;
                if (target != null)
                {
                    distance = gameObject.transform.position.x - target.transform.position.x;
                }
                else distance = charpos.now.x - charpos.old.x;

                if (distance > 0) // We are right to the target
                {
                    dirAxis.dirNow = -1;
                    if (dirAxis.dirNow == dirAxis.dirOld)
                    {
                        counterTimeLeft += Time.deltaTime;
                    }
                }
                else if (distance < 0) // We are left to the target
                {
                    dirAxis.dirNow = 1;
                    if (dirAxis.dirNow == dirAxis.dirOld)
                    {
                        counterTimeRight += Time.deltaTime;
                    }
                }
                else if (distance == 0) // We are below the target, so we push the same as last time
                {
                    dirAxis.dirNow = dirAxis.dirOld;
                }
                dirAxis.dirOld = dirAxis.dirNow;

                if (counterTimeRight == counterTimeLeft) // When we reach an equilibrium, we reset the forces to reaccelerate.
                {
                    counterTimeLeft = 0;
                    counterTimeRight = 0;
                }
                runTime = Mathf.Abs(counterTimeLeft - counterTimeRight); // When we switch to 'manual' swinging, we pass the runtime to it so it doesn't have to count from 0, aka reaccelerate.
                return -1 * (counterTimeLeft - counterTimeRight);
            }
            else
            {
                axisModeSwitched = true;
                dirAxis.dirNow = Input.GetAxisRaw("Horizontal");
                if (dirAxis.dirNow == dirAxis.dirOld && dirAxis.dirNow != 0)
                {
                    runTime += Time.deltaTime;
                }
                else
                {
                    runTime = 0;
                }
                dirAxis.dirOld = dirAxis.dirNow;

                return dirAxis.dirNow * runTime;
            }
        }
        #endregion
        #region Data storing
        public struct Direction
        {
            public float dirOld;
            public float dirNow;

            public Direction(float dir)
            {
                dirOld = dir;
                dirNow = dir;
            }
        }
        public struct CharacterPosition
        {
            public Vector3 old;
            public Vector3 now;

            public CharacterPosition(Vector3 pos)
            {
                old = pos;
                now = pos;
            }
        }
        #endregion 
    }
}

