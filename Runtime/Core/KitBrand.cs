namespace Kit
{
    /// <summary>
    /// The kit's name as students see it in menus and messages: "Besteck" (Jonas, 23.10.2026: cutlery, and a set of
    /// instruments). Renaming it means changing <see cref="Name"/> and nothing else. Never write a brand into a menu
    /// path or message by hand; use these. (The C# namespace stays Kit.)
    /// </summary>
    public static class KitBrand
    {
        public const string Name = "Besteck";

        /// <summary>Add Component › Brand › … (a category like Unity's own Physics › Rigidbody).</summary>
        public const string ComponentMenu = Name + "/";
        public const string ToolsMenu = "Tools/" + Name + "/";
        public const string GameObjectMenu = "GameObject/" + Name + "/";
        public const string CreateMenu = "Assets/Create/" + Name + "/";
        /// <summary>How the menus read in German prose: "Tools › Besteck › …".</summary>
        public const string ToolsPath = "Tools › " + Name + " › ";
        /// <summary>Prefix of every console line the kit writes.</summary>
        public const string Log = "[" + Name + "] ";
    }
}
