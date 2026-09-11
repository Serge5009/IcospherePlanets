using System;
using UnityEngine;

[Serializable]
public struct ReactionComponent
{
    public GasTemplate gas;
    [Tooltip("The coefficient in the chemical equation (e.g., 2 for 2H2O)")]
    public int moles;
}

[CreateAssetMenu(fileName = "New Atmospheric Reaction", menuName = "Strategy/Templates/Atmospheric Reaction")]
public class AtmosphericReaction : ScriptableObject
{
    public string reactionName;

    [Header("Chemical Equation")]
    public ReactionComponent[] inputs;
    public ReactionComponent[] outputs;

    [Header("Kinetics")]
    [Tooltip("Percentage of the limiting reagent that reacts per month at 100% concentration (0.0 to 1.0)")]
    public float baseReactionRate = 0.1f;
}