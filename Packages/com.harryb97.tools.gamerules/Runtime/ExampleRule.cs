using System;

namespace HarryTools.GameRules {
    [Serializable]
    public class ExampleRule : IGameRule {
        void IGameRule.UpdateParameters(GameRuleParameters _properties) {}
        bool IGameRule.Validate() => true;
    }
}