using UnityEngine;

namespace Kit.Editor
{
    /// <summary>Colours, icons and German words for node states, shared by the graph window and the Inspectors.</summary>
    public static class KitStyle
    {
        public static readonly Color Event = new Color32(0xf0, 0xa8, 0x30, 255);
        public static readonly Color Data = new Color32(0xc9, 0xa0, 0xff, 255);
        public static readonly Color Ref = new Color32(0x5f, 0xd0, 0xb5, 255);
        public static readonly Color Running = new Color32(0xf0, 0xe4, 0x42, 255);
        public static readonly Color Failed = new Color32(0xff, 0x6b, 0x5e, 255);
        public static readonly Color Blocked = new Color32(0x9a, 0xa0, 0xa6, 255);
        public static readonly Color Done = new Color32(0xe8, 0xe2, 0xd4, 255);
        public static readonly Color Board = new Color32(0x1c, 0x2a, 0x26, 255);   // Kurs-Tafel (Richtung A)
        public static readonly Color BoardLine = new Color32(0x26, 0x36, 0x31, 255);

        public static string Icon(NodeStatus s) => s switch
        {
            NodeStatus.Received => "○",
            NodeStatus.Running => "▬",
            NodeStatus.Completed => "✓",
            NodeStatus.Blocked => "⛔",
            NodeStatus.Failed => "!",
            _ => "·",
        };

        public static Color ColorOf(NodeStatus s) => s switch
        {
            NodeStatus.Running => Running,
            NodeStatus.Failed => Failed,
            NodeStatus.Blocked => Blocked,
            NodeStatus.Received => Color.white,
            _ => Done,
        };

        public static string German(NodeStatus s) => s switch
        {
            NodeStatus.Received => "empfangen",
            NodeStatus.Running => "läuft",
            NodeStatus.Completed => "fertig",
            NodeStatus.Blocked => "blockiert",
            NodeStatus.Failed => "Fehler",
            _ => "noch nicht erreicht",
        };
    }
}
