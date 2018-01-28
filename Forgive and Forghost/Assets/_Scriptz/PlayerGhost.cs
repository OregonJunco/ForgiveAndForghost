﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerGhost : MonoBehaviour {

    /*# Scene References #*/
    [SerializeField] private Rail _startingRail_c;

    /*# Config #*/
    [SerializeField] private float _maxSpeed_c = 30f;
    [SerializeField] private float _acceleration_c = 10f;
    /// In degrees per second the rate of angular change in my _twistAngle along a rail given an input
    [SerializeField] private float _twistAcceleration_c = 270f;
    [SerializeField] private float _twistMaxVelocity_c = 270f;

    /// Must be at this distance or less to be arrived at the node
    [SerializeField] private float _arrivedAtNodeTolerance_c = 0.5f;

    /*# Physical State #*/
    private float _currentSpeed = 0f;
    private float _angularVelocity = 0f;
    /// Current angle around rail axis, in degrees
    private float _twistAngle;

    /*# Object Reference State #*/
    private Node _fromNode;
    private Node _toNode;
    private Rail _currentlySelectedRail;
    
    /*# Cache #*/
    private ParticleSystem _speedLineParticleSystem;
    private float _fastSpeedLineEmissionRate;

    
    private void Start () {
        this._fromNode = this._startingRail_c.originNode;
        this._toNode   = this._startingRail_c.endNode;
        this.transform.position = this._fromNode.transform.position;
        
        // Initialize speed lines:
        this._speedLineParticleSystem = this.GetComponentInChildren<ParticleSystem>();
        this._fastSpeedLineEmissionRate = this._speedLineParticleSystem.emission.rateOverTime.constant;
	}
	
	private void Update () {
        // Get some values baked out:
	    var fromPos = this._fromNode.transform.position;
	    var toPos = this._toNode.transform.position;
	    var railAxis = (toPos - fromPos).normalized;
	    
	    // Apply acceleration:
        this._currentSpeed = Mathf.Clamp(this._currentSpeed + Time.deltaTime * this._acceleration_c, 0f, this._maxSpeed_c);

        // Rotate:
        var twistInput = -Input.GetAxis("Horizontal");
        if (Mathf.Abs(twistInput) > 0.001f)
            this._angularVelocity = Mathf.Clamp(this._angularVelocity + twistInput * this._twistAcceleration_c * Time.deltaTime, -this._twistMaxVelocity_c, this._twistMaxVelocity_c);
        else
            this._angularVelocity = 0f;
        this._twistAngle = (this._twistAngle + twistInput * this._twistAcceleration_c * Time.deltaTime) % 360f;
        // Update my rotation along the current rail:
        this.transform.rotation = Quaternion.AngleAxis(this._twistAngle, railAxis) * Quaternion.LookRotation(railAxis);

        // Move along rail:
        this.transform.position += railAxis * this._currentSpeed * Time.deltaTime;

	    // Calculate which rail is our next rail:
	    var newSelectedRail = this.calculateWhichRailIsSelected();
	    
	    if (this._currentlySelectedRail != newSelectedRail) {
	        this._currentlySelectedRail?.setIsSelected(false);
	        newSelectedRail?.setIsSelected(true);
	        this._currentlySelectedRail = newSelectedRail;
	    }
	    
        // Check to see if I've passed the end of my current rail:
//        var mPos = this.transform.position;
//        if(Vector3.Dot(toPos - mPos, toPos - fromPos) < 0f) {
//            // If so, move to the next rail:
//            this.changeRail(this._currentRail.endNode.getNextRailOnArrive(this._currentRail));
//        }
    }

    private Rail calculateWhichRailIsSelected()
    {
        // Get all the nodes that my "to" node connects to, with the exception of the one I'm already coming from
        var potentialTargetNodes = this._toNode.getPossibleNodesForJunction(this._fromNode);
        
        // If there are no potential targets, then I'm heading to a dead end:
        if (potentialTargetNodes.Length == 0)
            return null;
        
        // If there's only one potential target, then that's automatically the target we'll be going to: 
        else if (potentialTargetNodes.Length < 2)
            return this._toNode.getRailByDestinationNode(potentialTargetNodes[0]);
        
        // Otherwise, we have multiple tarets that we need to sift through!

        // Get some values baked out:
        var fromPos = this._fromNode.transform.position;
        var toPos = this._toNode.transform.position;
        var railAxis = (toPos - fromPos).normalized;
        var myUp = this.transform.up;  // We use a Quaternion.AngleAxis to align our up with our twist angle 

        var selectedNode = potentialTargetNodes.Select(node => {
            // Project the vector from my current "to" node to each potential target node upon the plane of my current rail axis
            var perpendicular = Vector3.ProjectOnPlane((node.transform.position - fromPos).normalized, railAxis);
            // Get the shortest angle between my current twist and these other angles
            return new System.Tuple<Node, float>(node, Vector3.Angle(myUp, perpendicular));
        })
            // Sort by smallest angle and return that node:
         .OrderBy(nodeAngleTuple => nodeAngleTuple.Item2)
         .ToList()[0].Item1;

        return this._toNode.getRailByDestinationNode(selectedNode);
    }
    
    private void updateWindParticles()
    {
        var lerpAmount = Mathf.InverseLerp(0, this._maxSpeed_c, this._currentSpeed);
        var emiss = this._speedLineParticleSystem.emission;
        var rate = emiss.rateOverTime;
        rate.constant = Mathf.Lerp(0, this._fastSpeedLineEmissionRate, lerpAmount);
        emiss.rateOverTime = rate;
    }
}
