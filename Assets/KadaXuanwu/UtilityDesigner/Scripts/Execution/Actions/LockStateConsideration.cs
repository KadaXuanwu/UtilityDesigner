using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class LockStateConsideration : ActionNode
    {
        public override string Description => "Locks the state and returns running until the consideration reaches the required value, " +
                                              "after which it unlocks the state and returns success.";
        
        public string considerationSetName;
        public string considerationName;
        public float requiredValue;
        public bool addConsiderationValue;

        private ConsiderationSet _considerationSet;
        private bool _changePositive;


        protected override void RegisterSerializedVariables()
        {
            AddVariable(nameof(considerationName), considerationName);
            AddVariable(nameof(requiredValue), requiredValue);
        }

        protected override void OnEnable()
        {
            _considerationSet = GetConsiderationSet(considerationSetName);
            if (_considerationSet == null)
                return;
            
            UtilityDesigner.StateLocked = true;
            if (addConsiderationValue)
                requiredValue += _considerationSet.GetConsideration(considerationName, UtilityDesigner);

            _changePositive = requiredValue >= _considerationSet.GetConsideration(considerationName, UtilityDesigner);
        }

        protected override NodeState OnUpdate()
        {
            bool checkValue = _changePositive
                ? _considerationSet.GetConsideration(considerationName, UtilityDesigner) >= requiredValue
                : _considerationSet.GetConsideration(considerationName, UtilityDesigner) <= requiredValue;

            if (!checkValue)
                return NodeState.Running;
            
            UtilityDesigner.StateLocked = false;
            return NodeState.Success;

        }
    }
}