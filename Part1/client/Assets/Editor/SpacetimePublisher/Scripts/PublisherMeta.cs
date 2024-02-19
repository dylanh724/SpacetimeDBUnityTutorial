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
        public const string PUBLISHER_IDENTITY_CHOICES_EDITOR_KEY = "PublisherIdentityChoices";
        public static string PathToUxml => $"{PUBLISHER_DIR_PATH}/Publisher.uxml";
        public static string PathToUss => $"{PUBLISHER_DIR_PATH}/Publisher.uss";
        
        // Colors pulled from docs
        public const string ACTION_COLOR_HEX = "#FFEA30"; // Corn Yellow
        public const string ERROR_COLOR_HEX = "#FDBE01"; // Golden Orange
        public const string SUCCESS_COLOR_HEX = "#4CF490"; // Sea Green (from docs)
        public const string FALLBACK_TEXT_COLOR_HEX = "#B6C0CF"; // Hazel Grey
        
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