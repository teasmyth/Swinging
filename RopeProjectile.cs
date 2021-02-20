using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;


namespace MoreMountains.CorgiEngine
{
    public class RopeProjectile : MonoBehaviour
    {
        protected LineRenderer lineRenderer;
        protected CharacterSwinging grappling;
        protected List<RopeSegment> ropeSegments = new List<RopeSegment>();

        public string playerTag = "Player";

        public float ropeSegLen = 0.1f; //distance between two segments
        public int segmentLength = 35;
        public float lineWidth = 0.1f;
        public int constrainSimulationNumber = 50;

        protected enum mode { Hook, Rope, Error }
        protected mode currentMode;

        protected GameObject weapon;
        protected RopeSegment endSegment;
        [HideInInspector] public GameObject endSegmentObject;
        [HideInInspector] public bool hit;

        protected GameObject controllerObj;

        //TODO there is a bug that displayes the hook mode's last positions for a frame before redrawing.

        protected void OnEnable()
        {
            lineRenderer = GetComponent<LineRenderer>();
            controllerObj = GameObject.FindGameObjectWithTag(playerTag);
            grappling = controllerObj.GetComponent<CharacterSwinging>();
            CheckMode();
            hit = false;
            if (currentMode == mode.Error)
            {
                //Error debug
            }
            if (currentMode == mode.Hook)
            {

            }
            if (currentMode == mode.Rope)
            {
                SegmentObject();
            }
            Vector3 ropeStartPoint = transform.position;
            for (int i = 0; i < segmentLength; i++)
            {
                ropeSegments.Add(new RopeSegment(ropeStartPoint));
            }
        }

        protected void CheckMode()
        {
            if (gameObject.CompareTag(grappling.hookTag))
            {
                currentMode = mode.Hook;
            }
            else if (gameObject.CompareTag(grappling.ropeTag))
            {
                currentMode = mode.Rope;
            }
            else
            {
                currentMode = mode.Error;
            }
        }

        /// <summary>
        /// Triggers attachement
        /// </summary>
        protected void SegmentObject()
        {
            endSegmentObject = new GameObject();
            endSegmentObject.transform.SetParent(gameObject.transform);
            endSegmentObject.AddComponent<BoxCollider2D>();
            endSegmentObject.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 0.5f);
            endSegmentObject.GetComponent<BoxCollider2D>().isTrigger = true;
            endSegmentObject.tag = "Rope";
            endSegmentObject.gameObject.name = "Last Segment Trigger";
            endSegmentObject.transform.position = endSegment.posNow;
        }

        #region Updates
        protected void Update()
        {
            if (currentMode == mode.Error)
            {
                Debug.Log("Error");
                return;
            }
            RopeAnimation(); //Need a function that sets the segments accordingly, whether it is projectile or not
            DrawRope();
        }

        #endregion

        //protected void CheckLifeTime() // life time changes only affect the next projectile's lifetime, need a workaround, until then, using fix value
        //{
        //    if (Input.GetKey(KeyCode.Mouse0))
        //    {
        //        float timePassed;
        //        timePassed = Time.deltaTime;
        //        gameObject.GetComponent<Projectile>().LifeTime += timePassed;
        //    }
        //    else
        //    {
        //        gameObject.GetComponent<Projectile>().LifeTime = 0.5f;
        //    }
        //}

        protected void OnTriggerEnter2D(Collider2D collider)
        {
            if (currentMode == mode.Hook)
            {
                gameObject.GetComponent<Projectile>().Speed = 0;
                hit = true;
            }
        }

        protected void OnDisable()
        {
            hit = false;
            ropeSegments.Clear();
        }

        protected void DrawRope()
        {
            if (currentMode == mode.Hook)
            {
                lineRenderer.startWidth = lineWidth;
                lineRenderer.endWidth = lineWidth;
                Vector3[] ropePositions = new Vector3[2]; //depending whether projectile or not
                for (int i = 0; i < 2; i++)
                {
                    ropePositions[i] = ropeSegments[i].posNow;
                }
                lineRenderer.positionCount = ropePositions.Length;
                lineRenderer.SetPositions(ropePositions);
            }
        }
        protected void RopeAnimation()
        {
            if (currentMode == mode.Hook)
            {
                RopeSegment first = ropeSegments[0];
                first.posNow = transform.position;
                ropeSegments[0] = first;

                RopeSegment last = ropeSegments[1];
                last.posNow = controllerObj.transform.position;
                ropeSegments[1] = last;
            }
            else if (currentMode == mode.Rope)
            {

            }
        }

        public struct RopeSegment
        {
            public Vector2 posOld;
            public Vector2 posNow;

            public RopeSegment(Vector2 pos)
            {
                posOld = pos;
                posNow = pos;
            }
        }

    }
}