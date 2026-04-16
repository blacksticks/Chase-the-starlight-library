using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using QuestBook.Models;

namespace QuestBook.Config
{
    internal static class QuestConfigLoader
    {
        internal static bool TryLoad(string path, out QuestBookData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                var doc = new XmlDocument();
                doc.Load(path);
                var root = doc.DocumentElement;
                if (root == null || root.Name != "QuestBook")
                    return false;

                var qb = new QuestBookData
                {
                    Version = GetAttr(root, "version"),
                    Author = GetAttr(root, "author"),
                    Locale = GetAttr(root, "locale")
                };

                var chapters = root.SelectSingleNode("Chapters");
                if (chapters != null)
                {
                    foreach (XmlNode ch in chapters.ChildNodes)
                    {
                        if (ch.NodeType != XmlNodeType.Element || ch.Name != "Chapter") continue;
                        var chapter = new Chapter
                        {
                            Id = GetAttr(ch, "id"),
                            Name = GetAttr(ch, "name"),
                            Order = ParseInt(GetAttr(ch, "order"), 0),
                            VisibleByDefault = ParseBool(GetAttr(ch, "visibleByDefault"), true)
                        };

                        var nodes = ch.SelectSingleNode("Nodes");
                        if (nodes != null)
                        {
                            foreach (XmlNode n in nodes.ChildNodes)
                            {
                                if (n.NodeType != XmlNodeType.Element || n.Name != "Node") continue;
                                var node = new Node
                                {
                                    Id = GetAttr(n, "id"),
                                    Title = GetAttr(n, "title"),
                                    Description = GetAttr(n, "description"),
                                    Icon = GetAttr(n, "icon"),
                                    Resettable = ParseBool(GetAttr(n, "resettable"), true)
                                };

                                var pos = GetAttr(n, "pos");
                                if (!string.IsNullOrEmpty(pos))
                                {
                                    var parts = pos.Split(',');
                                    if (parts.Length == 2)
                                    {
                                        node.PosX = ParseFloat(parts[0], 0);
                                        node.PosY = ParseFloat(parts[1], 0);
                                    }
                                }

                                var visible = n.SelectSingleNode("Visible") as XmlElement;
                                if (visible != null)
                                {
                                    node.VisibleRule = GetAttr(visible, "rule");
                                }

                                var unlock = n.SelectSingleNode("Unlock") as XmlElement;
                                if (unlock != null)
                                {
                                    node.UnlockRule = GetAttr(unlock, "rule");
                                }

                                var conds = n.SelectSingleNode("Conditions");
                                if (conds != null)
                                {
                                    foreach (XmlNode c in conds.ChildNodes)
                                    {
                                        if (c.NodeType != XmlNodeType.Element || c.Name != "Condition") continue;
                                        var cond = new Condition { Type = GetAttr(c, "type") };
                                        AddOtherAttributes(c as XmlElement, cond.Params, new[] { "type" });
                                        node.Conditions.Add(cond);
                                    }
                                }

                                var rewards = n.SelectSingleNode("Rewards");
                                if (rewards != null)
                                {
                                    foreach (XmlNode r in rewards.ChildNodes)
                                    {
                                        if (r.NodeType != XmlNodeType.Element || r.Name != "Reward") continue;
                                        var rew = new Reward { Type = GetAttr(r, "type") };
                                        AddOtherAttributes(r as XmlElement, rew.Params, new[] { "type" });
                                        node.Rewards.Add(rew);
                                    }
                                }

                                chapter.Nodes.Add(node);
                            }
                        }

                        var edges = ch.SelectSingleNode("Edges");
                        if (edges != null)
                        {
                            foreach (XmlNode e in edges.ChildNodes)
                            {
                                if (e.NodeType != XmlNodeType.Element || e.Name != "Edge") continue;
                                var edge = new Edge
                                {
                                    From = GetAttr(e, "from"),
                                    To = GetAttr(e, "to"),
                                    RequireComplete = ParseBool(GetAttr(e, "requireComplete"), true),
                                    HideUntilComplete = ParseBool(GetAttr(e, "hideUntilComplete"), false)
                                };
                                chapter.Edges.Add(edge);
                            }
                        }

                        qb.Chapters.Add(chapter);
                    }
                }

                qb.Chapters.Sort((a, b) => a.Order.CompareTo(b.Order));
                data = qb;
                return true;
            }
            catch (Exception ex)
            {
                Mod.Log?.LogError($"Config load error: {ex}");
                data = null;
                return false;
            }
        }

        private static string GetAttr(XmlNode node, string name)
        {
            if (node is XmlElement el)
                return el.HasAttribute(name) ? el.GetAttribute(name) : null;
            return null;
        }

        private static int ParseInt(string s, int def)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
            return def;
        }

        private static float ParseFloat(string s, float def)
        {
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            return def;
        }

        private static bool ParseBool(string s, bool def)
        {
            if (bool.TryParse(s, out var v)) return v;
            return def;
        }

        private static void AddOtherAttributes(XmlElement el, Dictionary<string, string> dict, string[] exclude)
        {
            if (el == null) return;
            var set = new HashSet<string>(exclude);
            foreach (XmlAttribute a in el.Attributes)
            {
                if (set.Contains(a.Name)) continue;
                dict[a.Name] = a.Value;
            }
        }
    }
}
