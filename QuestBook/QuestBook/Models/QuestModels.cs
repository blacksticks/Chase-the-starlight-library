using System.Collections.Generic;

namespace QuestBook.Models
{
    internal class QuestBookData
    {
        public string Version;
        public string Author;
        public string Locale;
        public List<Chapter> Chapters = new List<Chapter>();
    }

    internal class Chapter
    {
        public string Id;
        public string Name;
        public int Order;
        public bool VisibleByDefault;
        public List<Node> Nodes = new List<Node>();
        public List<Edge> Edges = new List<Edge>();
    }

    internal class Node
    {
        public string Id;
        public string Title;
        public string Description;
        public string Icon;
        public float PosX;
        public float PosY;
        public bool Resettable;
        public string VisibleRule;
        public string UnlockRule;
        public List<Condition> Conditions = new List<Condition>();
        public List<Reward> Rewards = new List<Reward>();
    }

    internal class Edge
    {
        public string From;
        public string To;
        public bool RequireComplete;
        public bool HideUntilComplete;
    }

    internal class Condition
    {
        public string Type;
        public Dictionary<string, string> Params = new Dictionary<string, string>();
    }

    internal class Reward
    {
        public string Type;
        public Dictionary<string, string> Params = new Dictionary<string, string>();
    }
}
