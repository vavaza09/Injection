namespace Game.Tests.Fixtures
{
    public static class TestGameConfig
    {
        // Health
        public const float PLAYER_HEALTH_HITS = 3f;
        public const float ENEMY_HEALTH_HITS = 2f;
        public const float PLAYER_INVINCIBILITY_DURATION = 2f;
        public const float DELTA_TIME_STEP = 0.016f; // ~60 FPS frame

        // Movement
        public const float PLAYER_MAX_SPEED = 8f;
        public const float PLAYER_JUMP_SPEED = -105f;
        public const float WALL_JUMP_HORIZONTAL = 80f;
        public const float WALL_JUMP_VERTICAL = -105f;

        // Dash
        public const float DASH_SPEED = 240f;
        public const float DASH_DURATION = 0.15f;
        public const float DASH_COOLDOWN = 0.2f;
        public const int DASH_MAX_COUNT = 1;

        // Combat
        public const float DASH_BASE_DAMAGE = 15f;
        public const float MOMENTUM_MIN = 0.6f;
        public const float MOMENTUM_MAX = 1.0f;
    }
}
