using KadaXuanwu.UtilityDesigner.Scripts.Evaluation;
using KadaXuanwu.UtilityDesigner.Scripts.Execution.Runtime;

namespace KadaXuanwu.UtilityDesigner.Scripts.Execution.Actions
{
    public class SetConsideration : ActionNode
    {
        public override string Description => "Sets the value of a consideration and returns success if the consideration exists, otherwise failure.";
        
        public string considerationSetName;
        public string considerationName;
        public float newValue;

        private bool _success;
        private ConsiderationSet _considerationSet;


        protected override void RegisterSerializedVariables()
        {
            AddVariable(nameof(considerationName), considerationName);
            AddVariable(nameof(newValue), newValue);
        }

        protected override void OnAwake()
        {
            _considerationSet = GetConsiderationSet(considerationSetName);
        }

        protected override void OnEnable()
        {
            if (_considerationSet != null)
                _success = _considerationSet.SetConsideration(considerationName, newValue, UtilityDesigner);
        }

        protected override NodeState OnUpdate()
        {
            return _success ? NodeState.Success : NodeState.Failure;
        }
    }
}