namespace Kit
{
    /// <summary>
    /// Palette groups (the second argument of [NodeInfo]) and their order in "+ Knoten". Use these constants, so a
    /// typo cannot open a new group. A lane that needs another group asks Kern on the board thread gec1-studio.
    /// </summary>
    public static class KitGroups
    {
        public const string Ausloeser = "Auslöser", Ablauf = "Ablauf", Werte = "Werte", Aktion = "Aktion", Szene = "Szene",
            Verhalten = "Verhalten", Atmosphaere = "Atmosphäre", Kamera = "Kamera", Spielsysteme = "Spielsysteme",
            Erweiterung = "Erweiterung", Eigene = "Eigene";
        /// <summary>v0.3 name of the Werte group; stays until the Werte lane has moved its nodes.</summary>
        public const string Zustand = "Zustand";

        public static readonly string[] Order =
            { Ausloeser, Ablauf, Werte, Zustand, Aktion, Szene, Verhalten, Atmosphaere, Kamera, Spielsysteme, Erweiterung, Eigene };

        /// <summary>Sort key of a group; unknown groups go last.</summary>
        public static int IndexOf(string group)
        {
            int i = System.Array.IndexOf(Order, group);
            return i >= 0 ? i : Order.Length;
        }
    }
}
