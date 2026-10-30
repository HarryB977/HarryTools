namespace HarryTools.GameRules {
    public interface IWeightedGameRule {
        // ==================== VARIABLES ===================
        int Weight { get; }

        // ==================== METHODS ====================
        void UpdateParameters(GameRuleParameters _properties);
        bool Validate();
    }
}