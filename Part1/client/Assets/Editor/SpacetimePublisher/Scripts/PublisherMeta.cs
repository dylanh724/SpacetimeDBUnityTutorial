namespace SpacetimeDB.Editor
{
    /// Static metadata for PublisherWindow
    public static class PublisherMeta
    {
        public enum StringStyle
        {
            Action,
            Error,
            Success,
        }

        public const string TOP_BANNER_CLICK_LINK = "https://spacetimedb.com/docs/modules";
        public const string DOCS_URL = "https://spacetimedb.com/install";
        public const string PUBLISHER_DIR_PATH = "Assets/Editor/SpacetimePublisher";
        public static string PathToUxml => $"{PUBLISHER_DIR_PATH}/Publisher.uxml";
        public static string PathToUss => $"{PUBLISHER_DIR_PATH}/Publisher.uss";
        
        public const string ACTION_COLOR_HEX = "#FFEA30"; // Corn Yellow
        public const string ERROR_COLOR_HEX = "#ed8536"; // Muted Orange-Red
        public const string SUCCESS_COLOR_HEX = "#4CF490"; // Sea Green (from docs)

        
        public static string GetStyledStr(StringStyle style, string str)
        {
            return style switch
            {
                StringStyle.Action => $"<color={ACTION_COLOR_HEX}><i>{str}</i></color>",
                StringStyle.Error => $"<color={ERROR_COLOR_HEX}>{str}</color>",
                StringStyle.Success => $"<color={SUCCESS_COLOR_HEX}>{str}</color>",
            };
        }
    }
}