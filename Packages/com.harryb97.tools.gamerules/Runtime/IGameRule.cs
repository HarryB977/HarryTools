namespace HarryTools.GameRules {
    public interface IGameRule {
        void UpdateParameters(GameRuleParameters _properties);
        bool Validate();
    }
}