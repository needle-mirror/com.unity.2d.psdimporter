using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Experimental;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.PSD
{
    class PSDNode : TreeViewItem
    {
        public PSDLayer layer;
        bool m_Disable = false;

        public bool disable
        {
            get => m_Disable;
            set => m_Disable = value;
        }
        
        public PSDNode()
        {
            id = 1;
            displayName = "";
        }
        
        public PSDNode(PSDLayer layer, int id)
        {
            this.layer = layer;
            displayName = layer.name;
            this.id = id;
        }

        public virtual void ChildGroupFlatten(bool flatten) { }
        public virtual void FlattenStateChange() { }
        public virtual void NotifyParentOnFlattenChange() { }
    }

    class PSDLayerNode : PSDNode
    {
        public PSDLayerNode(PSDLayer layer, int id):base(layer, id)
        { }
    }

    class PSDLayerGroupNode : PSDNode
    {
        int m_ChildFlattenCount;
        public PSDLayerGroupNode(PSDLayer layer, int id)
            : base(layer, id)
        {
            this.icon = EditorGUIUtility.FindTexture(EditorResources.folderIconName);
            m_ChildFlattenCount = 0;
        }

        public int childFlattenCount => m_ChildFlattenCount;

        public override void NotifyParentOnFlattenChange()
        {
            var pp = parent as PSDNode;
            if(pp != null)
                pp.ChildGroupFlatten(layer.flatten);
        }
        
        public override void ChildGroupFlatten(bool flatten)
        {
            m_ChildFlattenCount += flatten ? 1 : -1;
            var pp = parent as PSDNode;
            if(pp != null)
                pp.ChildGroupFlatten(flatten);
        }

        public override void FlattenStateChange()
        {
            if (children != null)
            {
                foreach (var child in children)
                {
                    var p = ((PSDNode)child);
                    if (p != null)
                    {
                        p.disable = layer.flatten || this.disable;
                        p.FlattenStateChange();   
                    }
                }   
            }
        }
    }

    static class Style
    {
        static public GUIStyle hoverLine = "TV Selection";
        static public GUIStyle flattenToggleStyle = "MultiColumnHeaderCenter";
        static public readonly string k_LightIconResourcePath = "Icons/Light";
        static public readonly string k_DarkIconResourcePath = "Icons/Dark";
        static public readonly string k_SelectedIconResourcePath = "Icons/Selected";
        static public GUIContent visibilityIcon = EditorGUIUtility.IconContent("animationvisibilitytoggleoff", L10n.Tr("|Layer is not visible in source file"));
        public static readonly GUIContent[] collapsedIcon =
        {
            new GUIContent(LoadIconResource("Layers Separated", k_LightIconResourcePath, k_DarkIconResourcePath), L10n.Tr("Layers Separated. Click to collapse them.")),
            new GUIContent(LoadIconResource("Layers Separated", k_SelectedIconResourcePath, k_SelectedIconResourcePath), L10n.Tr("Layers Separated. Click to collapse them."))
        };
        public static readonly GUIContent[] separateIcon =
        {
            new GUIContent(LoadIconResource("Layers Collapsed", k_LightIconResourcePath, k_DarkIconResourcePath), L10n.Tr("Layers collapsed. Click to separate them.")),
            new GUIContent(LoadIconResource("Layers Collapsed", k_SelectedIconResourcePath, k_SelectedIconResourcePath), L10n.Tr("Layers collapsed. Click to separate them."))
        };
        public static readonly GUIContent[] collapseMix =
        {
            new GUIContent(LoadIconResource("Layers Mixed", k_LightIconResourcePath, k_DarkIconResourcePath), L10n.Tr("Group contains child groups that are collapsed.")),
            new GUIContent(LoadIconResource("Layers Mixed", k_SelectedIconResourcePath, k_SelectedIconResourcePath), L10n.Tr("Group contains child groups that are collapsed."))
        };

        const string k_ResourcePath = "Packages/com.unity.2d.psdimporter/Editor/Assets";
        
        public static int iconSize = 16;
        public static int iconPadding = 6;
        public static Texture2D LoadIconResource(string name, string personalPath, string proPath)
        {
            string iconPath = "";

            if (EditorGUIUtility.isProSkin && !string.IsNullOrEmpty(proPath))
                iconPath = Path.Combine(proPath, name);
            else
                iconPath = Path.Combine(personalPath, name);
            if (EditorGUIUtility.pixelsPerPoint > 1.0f)
            {
                var icon2x = Load<Texture2D>(iconPath + "@4x.png");
                if (icon2x != null)
                    return icon2x;
            }

            return Load<Texture2D>(iconPath+"@2x.png");
        }

        internal static T Load<T>(string path) where T : Object
        {
            var assetPath = Path.Combine(k_ResourcePath, path);
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset;
        }

        static Style()
        {
            flattenToggleStyle.border = new RectOffset();
            flattenToggleStyle.margin = new RectOffset();
            flattenToggleStyle.padding = new RectOffset();
        }
    }
    
    internal class PSDImporterEditorLayerTreeView : TreeView
    {
        List<PSDLayer> m_Layers;
        bool m_ShowHidden;
        bool m_HasChanged;
        public PSDImporterEditorLayerTreeView(TreeViewState treeViewState, List<PSDLayer> layers, bool showHidden)
            : base(treeViewState)
        {
            m_Layers = layers;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            m_ShowHidden = showHidden;
            m_HasChanged = false;
            baseIndent = 32;
            useScrollView = true;
            Reload();
        }

        public bool showHidden
        {
            get => m_ShowHidden;
            set
            {
                if (m_ShowHidden != value)
                {
                    m_ShowHidden = value;
                    Reload();
                }
            }
        }
        
        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (m_Layers != null)
            {
                PSDNode[] nodes = new PSDNode[m_Layers.Count];
                for(int i = 0; i < m_Layers.Count; ++i)
                {
                    var l = m_Layers[i];
                    if (l!= null && l.isGroup)
                        nodes[i] = new PSDLayerGroupNode(l, i);
                    else
                        nodes[i] = new PSDLayerNode(l, i);
                }
                foreach (var node in nodes)
                {
                    if (showHidden || node.layer.isVisible)
                    {
                        if (node.layer.parentIndex == -1)
                        {
                            root.AddChild(node);
                        }
                        else
                        {
                            nodes[node.layer.parentIndex].AddChild(node);
                            node.disable = nodes[node.layer.parentIndex].layer.flatten || nodes[node.layer.parentIndex].disable;
                            if(node.layer.flatten)
                                nodes[node.layer.parentIndex].ChildGroupFlatten(node.layer.flatten);
                        }
                    }
                }    
            }
            if(root.children == null)
                root.children = new List<TreeViewItem>();
            SetupDepthsFromParentsAndChildren(root);

            return root;
        }


        protected override void RowGUI(RowGUIArgs args)
        {
            var node = (PSDNode)args.item;
            var rowRect = args.rowRect;
            using (new EditorGUI.DisabledScope(node.disable))
            {
                var hover = rowRect.Contains(Event.current.mousePosition);
                var a1 = args.focused;
                var a2 = args.selected;
                if (hover && Event.current.type == EventType.Repaint)
                {
                    args.selected = args.focused = true;
                    Style.hoverLine.Draw(rowRect, false, false, a2, true);
                }

                base.RowGUI(args);
                args.focused = a1;
                args.selected = a2;
                
                if (node.layer != null && !node.layer.isVisible)
                {
                    GUI.Box(new Rect(rowRect.x, rowRect.y, Style.iconSize, Style.iconSize), Style.visibilityIcon, Style.flattenToggleStyle);
                }
                
                if (args.item is PSDLayerGroupNode)
                {
                    var group = (PSDLayerGroupNode)args.item;
                    Rect toggleRect = new Rect(rowRect.x + foldoutWidth + Style.iconPadding + args.item.depth * this.depthIndentWidth, rowRect.y, Style.iconSize, Style.iconSize);
                    EditorGUI.BeginChangeCheck();
                    GUIContent[] icon = null;
                    if (group.childFlattenCount != 0)
                        icon = Style.collapseMix;
                    if (hover)
                    {
                        if(group.layer.flatten)
                            icon = Style.separateIcon;
                        else
                            icon = Style.collapsedIcon;
                    }
                    else if(group.layer.flatten)
                        icon = Style.collapsedIcon;
                        
                    if (icon != null)
                    {
                        var iconIndex = args.selected ? 1 : 0;
                        group.layer.flatten = GUI.Toggle(toggleRect, group.layer.flatten, icon[iconIndex], Style.flattenToggleStyle);
                    }
                        

                    if (EditorGUI.EndChangeCheck())
                    {
                        group.FlattenStateChange();
                        group.NotifyParentOnFlattenChange();
                        m_HasChanged = true;
                    }
                }
            }
        }
        
        public bool GetHasChangeAndClear()
        {
            var v = m_HasChanged;
            m_HasChanged = false;
            return v;
        }
    }
}

