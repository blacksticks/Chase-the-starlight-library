using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
 
using UnityEngine;
using UnityEngine.UI;
using JustEnoughItems.Config;

namespace JustEnoughItems.UI
{
    // 仅做“数据填充”，不创建层级。层级完全来自 Unity 预制体。
    // 预制体要求：
    // JEI_Overview
    //   └─ Scroll View (ScrollRect)
    //       └─ Viewport (Image[透明 RaycastTarget=true] + RectMask2D)
    //           └─ Content (RectTransform + VerticalLayoutGroup + ContentSizeFitter)
    //               └─ GroupTemplate (GameObject, inactive)
    //                   ├─ Title (Text)
    //                   └─ Grid (RectTransform + GridLayoutGroup)
    //                       └─ ItemCellTemplate (GameObject, inactive)
    //                           ├─ Icon (Image)
    //                           └─ Name (Text)
    public class JeiOverviewBuilder : MonoBehaviour
    {
        [Header("Bind from prefab")]
        public RectTransform Content;          // 必填：ScrollView/Viewport/Content
        public GameObject GroupTemplate;       // 必填：Content/GroupTemplate (inactive)
        public GameObject ItemCellTemplate;    // 必填：Content/GroupTemplate/Grid/ItemCellTemplate (inactive)

        private static UnityEngine.AssetBundle _iconsBundle;
        private static readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static Sprite _transparent;
        private bool _builtOnce;
        private static Dictionary<string, string> _iconPathByFileName; // lower filename -> asset path
        private static bool _iconIndexBuilt;

        private UnityEngine.UI.InputField _searchInput;
        private UnityEngine.UI.Button _searchButton;
        private readonly List<GameObject> _itemCells = new List<GameObject>();
        private readonly List<string> _itemIds = new List<string>();
        private readonly List<string> _itemNames = new List<string>();
        private readonly List<Transform> _itemGroups = new List<Transform>();
        private readonly List<GameObject> _groupRoots = new List<GameObject>();

        


        public void Build()
        {
            if (_builtOnce) return;
            // 运行期自动绑定（容错）：若未在 Inspector 赋值，则按约定路径尝试查找
            try
            {
                if (Content == null)
                {
                    var contentTf = transform.Find("Scroll View/Viewport/Content") as RectTransform;
                    if (contentTf != null) Content = contentTf;
                    if (Content == null)
                    {
                        var direct = transform.Find("Content") as RectTransform;
                        if (direct != null) Content = direct;
                    }
                    if (Content == null)
                    {
                        // 深搜一个名为 Content 的 RectTransform（容错命名/层级变动）
                        Content = FindDeepRect(transform, "Content");
                    }
                }
                
                if (GroupTemplate == null && Content != null)
                {
                    var g = Content.transform.Find("GroupTemplate");
                    if (g == null) g = FindDeep(Content, "GroupTemplate");
                    if (g != null) GroupTemplate = g.gameObject;
                }

                if (ItemCellTemplate == null && GroupTemplate != null)
                {
                    var grid = GroupTemplate.transform.Find("Grid");
                    var cell = grid != null ? grid.Find("ItemCellTemplate") : null;
                    if (cell == null) cell = FindDeep(GroupTemplate.transform, "ItemCellTemplate");
                    if (cell != null) ItemCellTemplate = cell.gameObject;
                }
                // 仅确保模板本体保持 Inactive，避免在网格中显示模板占位
                try { if (GroupTemplate != null) GroupTemplate.SetActive(false); } catch { }
                try { if (ItemCellTemplate != null) ItemCellTemplate.SetActive(false); } catch { }
                
            }
            catch { }

            // 对齐详情页：在构建前确保一次性初始化图标缓存
            try { JustEnoughItems.IconCache.InitializeOnce(); } catch { }

            // 绑定检索控件（不改动样式/层级/背景，仅接线事件）
            try
            {
                var searchRoot = transform.Find("Search") ?? FindDeep(transform, "Search");
                if (_searchInput == null)
                {
                    var t = transform.Find("Search/InputField") ?? FindDeep(transform, "SearchInput") ?? FindDeep(transform, "InputField");
                    if (t != null) _searchInput = t.GetComponent<UnityEngine.UI.InputField>();
                    if (_searchInput == null)
                    {
                        var candidates = GetComponentsInChildren<UnityEngine.UI.InputField>(true);
                        if (candidates != null && candidates.Length > 0) _searchInput = candidates[0];
                    }
                }
                if (_searchButton == null)
                {
                    var tb = transform.Find("Search/Button") ?? FindDeep(transform, "SearchButton") ?? FindDeep(transform, "Search");
                    if (tb != null) _searchButton = tb.GetComponent<UnityEngine.UI.Button>();
                    if (_searchButton == null)
                    {
                        var btns = GetComponentsInChildren<UnityEngine.UI.Button>(true);
                        foreach (var b in btns)
                        {
                            if (b != null && (string.Equals(b.gameObject.name, "Search", StringComparison.OrdinalIgnoreCase) || string.Equals(b.gameObject.name, "SearchButton", StringComparison.OrdinalIgnoreCase))) { _searchButton = b; break; }
                        }
                    }
                }
                if (_searchInput != null)
                {
                    _searchInput.onValueChanged.RemoveListener(OnSearchChanged);
                    _searchInput.onValueChanged.AddListener(OnSearchChanged);
                }
                if (_searchButton != null)
                {
                    _searchButton.onClick.RemoveAllListeners();
                    _searchButton.onClick.AddListener(() => { try { ApplyFilter(_searchInput != null ? _searchInput.text : null); } catch { } });
                }
            }
            catch { }

            if (Content == null || GroupTemplate == null || ItemCellTemplate == null)
            {
                Debug.LogError("[JEI][Overview] Prefab references missing: Content/GroupTemplate/ItemCellTemplate");
                return;
            }

            

            // 清空 Content 下的运行时子项（保留模板）
            _itemCells.Clear();
            _itemIds.Clear();
            _itemNames.Clear();
            _itemGroups.Clear();
            _groupRoots.Clear();
            var keep = new HashSet<Transform> { GroupTemplate.transform };
            for (int i = Content.childCount - 1; i >= 0; i--)
            {
                var ch = Content.GetChild(i);
                if (ch == GroupTemplate.transform) continue;
                Destroy(ch.gameObject);
            }

            // 数据就绪
            try { JeiDataStore.BuildIfNeeded(); } catch { }
            try { ConfigService.EnsureNamesLoaded(); } catch { }
            try { JustEnoughItems.Config.FabricatorOverridesService.Reload(); } catch { }

            var groups = FabricatorOverridesService.Current ?? new List<FabricatorOverride>();
            foreach (var g in groups)
            {
                var go = Instantiate(GroupTemplate, Content);
                go.SetActive(true);

                var groupRt = go.GetComponent<RectTransform>();
                var titleTf = go.transform.Find("Title") ?? FindDeep(go.transform, "Title");
                var titleText = titleTf != null ? titleTf.GetComponent<Text>() : null;
                var gridRt = go.transform.Find("Grid") as RectTransform;
                var grid = gridRt != null ? gridRt.GetComponent<GridLayoutGroup>() : null;
                if (titleText != null)
                {
                    titleText.text = ResolveFabricatorTitle(g);
                }
                _groupRoots.Add(go);

                var itemIds = (g?.IncludeItems ?? new List<string>())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(NormalizeId)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                int visibleCount = 0;
                int noIconCount = 0;
                foreach (var id in itemIds)
                {
                    var sprite = GetItemSprite(id);

                    var cell = Instantiate(ItemCellTemplate, gridRt);
                    cell.SetActive(true);
                    var icon = cell.transform.Find("Icon")?.GetComponent<Image>();
                    var nameText2 = cell.transform.Find("Name")?.GetComponent<Text>();
                    if (icon != null)
                    {
                        if (sprite != null)
                        {
                            icon.sprite = sprite;
                        }
                        else
                        {
                            // 无图标：仅显示名称，图标置为透明
                            icon.sprite = null;
                            icon.color = new Color(1f, 1f, 1f, 0f);
                            noIconCount++;
                        }
                    }
                    if (nameText2 != null)
                    {
                        nameText2.text = GetDisplayName(id);
                    }

                    _itemCells.Add(cell);
                    _itemIds.Add(id);
                    _itemNames.Add(GetDisplayName(id));
                    _itemGroups.Add(go.transform);

                    // 点击进入已有详情页（与背包一致）
                    var btn = cell.GetComponent<Button>() ?? cell.AddComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    var captured = id;
                    btn.onClick.AddListener(() => {
                        try { JustEnoughItems.IconCache.InitializeOnce(); } catch { }
                        try { JeiManager.ShowForItem(captured); } catch { }
                    });

                    // 保留左键打开详情，未接入右键钉选

                    // 右键钉蓝图可后续在此扩展 EventTrigger（TODO）
                    visibleCount++;
                }
                
            }

            

            ApplyFilter(_searchInput != null ? _searchInput.text : null);
            // 标记首次构建完成，避免后续重复构建
            _builtOnce = true;
        }


        private RectTransform FindDeepRect(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur != null && cur.name == name)
                {
                    var rt = cur as RectTransform;
                    if (rt != null) return rt;
                }
                for (int i = 0; i < cur.childCount; i++) stack.Push(cur.GetChild(i));
            }
            return null;
        }

        private Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur != null && cur.name == name) return cur;
                for (int i = 0; i < cur.childCount; i++) stack.Push(cur.GetChild(i));
            }
            return null;
        }

        private string NormalizeId(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var t = s.Trim();
            if (t.StartsWith("TechType.", StringComparison.OrdinalIgnoreCase)) t = t.Substring("TechType.".Length);
            return t.Trim();
        }

        private string GetDisplayName(string id)
        {
            try
            {
                var norm = NormalizeId(id);
                var dict = ConfigService.ChineseNames;
                if (dict != null && dict.TryGetValue(norm, out var name) && !string.IsNullOrEmpty(name)) return name;
            }
            catch { }
            return id;
        }

        private Sprite GetItemSprite(string idOrTechType)
        {
            try
            {
                if (string.IsNullOrEmpty(idOrTechType)) return null;
                var token = NormalizeId(idOrTechType);
                var spr = JustEnoughItems.IconCache.GetById(token);
                return spr ?? GetTransparentPlaceholder();
            }
            catch { return null; }
        }

        private void OnSearchChanged(string text)
        {
            try { ApplyFilter(text); } catch { }
        }

        private void ApplyFilter(string term)
        {
            string t = term == null ? string.Empty : term.Trim();
            bool showAll = string.IsNullOrEmpty(t);
            var groupVisible = new Dictionary<Transform, int>();
            for (int i = 0; i < _itemCells.Count; i++)
            {
                var cell = _itemCells[i];
                var id = _itemIds[i] ?? string.Empty;
                var name = _itemNames[i] ?? string.Empty;
                bool match = showAll || id.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;
                if (cell != null) cell.SetActive(match);
                var grp = _itemGroups[i];
                if (grp != null)
                {
                    groupVisible[grp] = groupVisible.TryGetValue(grp, out var c) ? (c + (match ? 1 : 0)) : (match ? 1 : 0);
                }
            }
            foreach (var gr in _groupRoots)
            {
                if (gr == null) continue;
                var tr = gr.transform;
                int cnt = groupVisible.TryGetValue(tr, out var v) ? v : 0;
                gr.SetActive(cnt > 0 || showAll);
            }
        }

        

        private static string ResolveIconsBundlePath()
        {
            try
            {
                var dir = ConfigService.PluginsAssetBundlesDirectory;
                if (string.IsNullOrEmpty(dir)) return null;
                var win = System.IO.Path.Combine(dir, "Windows");
                var expected = System.IO.Path.Combine(win, "jei-icons");
                if (System.IO.File.Exists(expected)) return expected;
            }
            catch { }
            return null;
        }

 

        private static Sprite TryGetIconFromIconsBundle(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return null;
                if (_iconCache.TryGetValue(id, out var sp) && sp != null) return sp;
                if (_iconsBundle == null)
                {
                    var path = ResolveIconsBundlePath();
                    if (!string.IsNullOrEmpty(path))
                    {
                        _iconsBundle = UnityEngine.AssetBundle.LoadFromFile(path);
                        _iconIndexBuilt = false;
                    }
                }
                if (_iconsBundle == null) return null;

                // 建立一次性索引：文件名(含后缀) -> 资产路径
                if (!_iconIndexBuilt)
                {
                    try
                    {
                        _iconPathByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var names = _iconsBundle.GetAllAssetNames();
                        if (names != null)
                        {
                            foreach (var n in names)
                            {
                                var ln = n.Replace("\\", "/");
                                var slash = ln.LastIndexOf('/');
                                var file = slash >= 0 ? ln.Substring(slash + 1) : ln;
                                if (!string.IsNullOrEmpty(file))
                                {
                                    if (!_iconPathByFileName.ContainsKey(file)) _iconPathByFileName[file] = n;
                                }
                            }
                        }
                    }
                    catch { }
                    _iconIndexBuilt = true;
                }

                var key = (id + ".png").ToLowerInvariant();
                string hit = null;
                if (_iconPathByFileName != null)
                {
                    _iconPathByFileName.TryGetValue(key, out hit);
                }
                if (string.IsNullOrEmpty(hit)) return null;

                Sprite sprite = null;
                try { sprite = _iconsBundle.LoadAsset<Sprite>(hit); } catch { }
                if (sprite == null)
                {
                    try
                    {
                        var tex = _iconsBundle.LoadAsset<Texture2D>(hit);
                        if (tex != null)
                        {
                            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        }
                    }
                    catch { }
                }
                if (sprite != null)
                {
                    _iconCache[id] = sprite;
                    return sprite;
                }
            }
            catch { }
            return null;
        }

        private static Sprite GetTransparentPlaceholder()
        {
            if (_transparent != null) return _transparent;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                var cols = new Color32[4] { new Color32(0,0,0,0), new Color32(0,0,0,0), new Color32(0,0,0,0), new Color32(0,0,0,0) };
                tex.SetPixels32(cols);
                tex.Apply(false, true);
                _transparent = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch { }
            return _transparent;
        }

        private string ResolveFabricatorTitle(FabricatorOverride fo)
        {
            try
            {
                if (fo != null && !string.IsNullOrEmpty(fo.DisplayName)) return fo.DisplayName;
                var id = fo?.Id ?? "Fabricator";
                var dn = GetDisplayName(id);
                return string.IsNullOrEmpty(dn) ? id : dn;
            }
            catch { return "Fabricator"; }
        }
    }
}
