using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KadaXuanwu.UtilityDesigner.Scripts.Evaluation
{
    [Serializable]
    public class StateSet
    {
        [SerializeField] internal List<State> states = new();
        [SerializeField] internal string designation;
        
        
        internal State DecideNextState(State currentlyExecutedState)
        {
            List<State> availableStates = new(states);
            State lastFailedState = null;

            availableStates = availableStates
                .Select(state => new { State = state, Score = state.GetScore(currentlyExecutedState == state) })
                .Where(x => x.Score >= 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.State)
                .ToList();

            foreach (var state in availableStates)
            {
                if (state == currentlyExecutedState)
                    return currentlyExecutedState;

                if (Random.Range(0f, 1f) > state.failChance)
                    return state;

                lastFailedState = state;
            }

            return lastFailedState;
        }
    };
}
