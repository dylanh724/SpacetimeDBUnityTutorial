namespace SpacetimeDB.Editor
{
    /// Static metadata for PublisherWindow
    public static class PublisherMeta
    {
        private const string PUBLISHER_DIR_PATH = "Assets/Editor/SpacetimePublisher";
        public static string PathToUxml => $"{PUBLISHER_DIR_PATH}/Publisher.uxml";
        public static string PathToUss => $"{PUBLISHER_DIR_PATH}/Publisher.uss";
        
        public const string ACTION_COLOR_HEX = "#FFEA30"; // Corn Yellow
        public const string ERROR_COLOR_HEX = "#ED5E2F"; // Muted Red
        public const string SUCCESS_COLOR_HEX = "#58EA2C"; // Sea Green
    }
}