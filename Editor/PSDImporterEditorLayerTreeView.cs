using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Experimental;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.PSD
{
    internal class PSDLayerImportSettingSerializedPropertyWrapper : IPSDLayerMappingStrategyComparable
    {
        PSDLayerData m_Layer;
        SerializedProperty m_Array;
        SerializedProperty m_Element;
        SerializedProperty m_NameProperty;
        SerializedProperty m_LayerIdProperty;
        SerializedProperty m_FlattenProperty;
        SerializedProperty m_IsGroup;
        SerializedProperty m_ImportLayerProperty;
        bool m_WasLayerImported;

        public string name
        {
            get => m_NameProperty.stringValue;
            set
            {
                CheckAndAddElement();
                m_NameProperty.stringValue = value;
            }
        }

        public bool isGroup
        {
            get => m_IsGroup.boolValue;
            set
            {
                CheckAndAddElement();
                m_IsGroup.boolValue = value;
            }
        }

        public int layerID
        {
            get => m_LayerIdProperty.intValue;
            set
            {
                CheckAndAddElement();
                m_LayerIdProperty.intValue = value;
            }
        }

        public bool flatten
        {
            get => m_FlattenProperty == null ? false : m_FlattenProperty.boolValue;
            set
            {
                CheckAndAddElement();
                m_FlattenProperty.boolValue = value;
            }
        }

        public bool wasLayerImported
        {
            get => m_WasLayerImported;
            set => m_WasLayerImported = value;
        }

        public bool importLayer
        {
            get => m_ImportLayerProperty == null ? wasLayerImported : m_ImportLayerProperty.boolValue;
            set
            {
                CheckAndAddElement();
                m_ImportLayerProperty.boolValue = value;
            }
        }
        
        void CheckAndAddElement()
        {
            if (m_Element == null)
            {
                var arraySize = m_Array.arraySize;
                m_Array.arraySize = arraySize + 1;
                m_Element = m_Array.GetArrayElementAtIndex(arraySize);
                CacheProperty(m_Element);
                flatten = false;
                name = m_Layer.name;
                layerID = m_Layer.layerID;
                isGroup = m_Layer.isGroup;
                importLayer = wasLayerImported;
            }
        }

        void CacheProperty(SerializedProperty property)
        {
            m_NameProperty = property.FindPropertyRelative("name");
            m_LayerIdProperty = property.FindPropertyRelative("layerId");
            m_FlattenProperty = property.FindPropertyRelative("flatten");
            m_IsGroup = property.FindPropertyRelative("isGroup");
            m_ImportLayerProperty = property.FindPropertyRelative("importLayer");

        }

        public PSDLayerImportSettingSerializedPropertyWrapper(SerializedProperty sp, SerializedProperty array, PSDLayerData layer)
        {
            if (sp != null)
            {
                m_Element = sp;
                CacheProperty(sp);
            }
            
            m_Array = array;
            m_Layer = layer;
        }
    }

    class PSDNode : TreeViewItem
    {
        NodeStateChange m_ImportState = new NodeStateChange();
        PSDLayerData m_Layer;
        bool m_Disable = false;
        public PSDLayerData layer => m_Layer;

        PSDLayerImportSettingSerializedPropertyWrapper m_Property;

        public bool disable
        {
            get => m_Disable;
            set => m_Disable = value;
        }

        public PSDNode()
        {
            id = 1;
            displayName = "";
            m_ImportState.state = true;
        }

        public PSDNode(PSDLayerData layer, int id, PSDLayerImportSettingSerializedPropertyWrapper importSetting)
        {
            m_Layer = layer;
            displayName = layer.name;
            this.id = id;
            m_Property = importSetting;
            m_ImportState.state = importLayer;
        }

        public NodeStateChange importState => m_ImportState;
        
        public PSDLayerImportSettingSerializedPropertyWrapper property => m_Property;

        public virtual bool importLayer
        {
            get => property.importLayer;
            set => property.importLayer = value;
        }
    }

    class NodeStateChange
    {
        public int childNodeStateCount = 0;
        public NodeStateChange parent;
        public bool state;
        
        public void ChangeState(bool newState)
        {
            state = newState;
            parent?.NotifyParentStateChange(newState);
        }

        void NotifyParentStateChange(bool newState)
        {
            childNodeStateCount +=  newState ? 1 : -1;
            parent?.NotifyParentStateChange(newState);
        }
    }
    
    class PSDCollapsableNode :PSDNode
    {
        NodeStateChange m_CollapseStateChange = new NodeStateChange();

        public virtual bool flatten
        {
            get => property.flatten;
            set => property.flatten = value;
        }

        public PSDCollapsableNode()
            : base()
        {
            m_CollapseStateChange.state = false;
        }

        public PSDCollapsableNode(PSDLayerData layer, int id, PSDLayerImportSettingSerializedPropertyWrapper property)
            : base(layer, id, property)
        {
            if(property != null)
                m_CollapseStateChange.state = flatten;
        }

        public NodeStateChange collapseStateChange => m_CollapseStateChange;
    }

    class PSDFileNode : PSDCollapsableNode
    {
        SerializedProperty m_MosaicProperty;
        SerializedProperty m_ImportFileNodeState;

        public PSDFileNode(SerializedProperty mosaicProperty, SerializedProperty importNodeState)
        {
            m_MosaicProperty = mosaicProperty;
            m_ImportFileNodeState = importNodeState;
        }
        public override bool flatten
        {
            get => !m_MosaicProperty.boolValue;
            set
            {
                collapseStateChange.state = value;
                m_MosaicProperty.boolValue = !value;   
            }
        }
        
        public override bool importLayer
        {
            get => m_ImportFileNodeState.boolValue;
            set => m_ImportFileNodeState.boolValue = value;
        }
    }
    
    class PSDLayerNode : PSDNode
    {
        public PSDLayerNode(PSDLayerData layer, int id, PSDLayerImportSettingSerializedPropertyWrapper property):base(layer, id, property)
        { }
    }

    class PSDLayerGroupNode : PSDCollapsableNode
    {
        public PSDLayerGroupNode(PSDLayerData layer, int id, PSDLayerImportSettingSerializedPropertyWrapper property)
            : base(layer, id, property)
        {
            this.icon = EditorGUIUtility.FindTexture(EditorResources.folderIconName);
        }
    }

    static class Style
    {
        static public GUIStyle hoverLine = "TV Selection";
        static public GUIStyle flattenToggleStyle = "MultiColumnHeaderCenter";
        static public readonly string k_LightIconResourcePath = "Icons/Light";
        static public readonly string k_DarkIconResourcePath = "Icons/Dark";
        static public readonly string k_SelectedIconResourcePath = "Icons/Selected";
        public static readonly GUIContent layerHiddenToolTip = EditorGUIUtility.TrTextContent("", "The layer is hidden in the source file.");
        public static readonly GUIContent hiddenLayerNotImportWarning = EditorGUIUtility.TrIconContent("console.warnicon", "Layer will not be imported because hidden layers are excluded from import.");
        public static readonly GUIContent[] mergedIcon =
        {
            new GUIContent(LoadIconResource("Layers Separated", k_LightIconResourcePath, k_DarkIconResourcePath), L10n.Tr("Layers Separated. Click to merge them.")),
            new GUIContent(LoadIconResource("Layers Separated", k_SelectedIconResourcePath, k_SelectedIconResourcePath), L10n.Tr("Layers Separated. Click to merge them."))
        };
        public static readonly GUIContent[] separateIcon =
        {
            new GUIContent(LoadIconResource("Layers Collapsed", k_LightIconResourcePath, k_DarkIconResourcePath), L10n.Tr("Layers merged. Click to separate them.")),
            new GUIContent(LoadIconResource("Layers Collapsed", k_SelectedIconResourcePath, k_SelectedIconResourcePath), L10n.Tr("Layers merged. Click to separate them."))
        };
        public static readonly GUIContent[] mergedMix =
        {
            new GUIContent(LoadIconResource("Layers Mixed", k_LightIconResourcePath, k_DarkIconResourcePath), L10n.Tr("Group contains child groups that are merged.")),
            new GUIContent(LoadIconResource("Layers Mixed", k_SelectedIconResourcePath, k_SelectedIconResourcePath), L10n.Tr("Group contains child groups that are merged."))
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
    
    internal class PSDImporterEditorLayerTreeView : IMGUI.Controls.TreeView
    {
        PSDLayerData[] m_Layers;
        string m_RootName;
        SerializedProperty m_PSDLayerImportSetting;
        IPSDLayerMappingStrategy m_MappingStrategy;
        int m_LastArraySize;
        SerializedProperty m_MosaicProperty;
        SerializedProperty m_FileNodeImportState;
        SerializedProperty m_ImportHidden;
        const int k_LeftMargin = 15;
        public PSDImporterEditorLayerTreeView(string rootName, TreeViewState treeViewState, PSDLayerData[] layers, SerializedProperty psdLayerImportSetting, IPSDLayerMappingStrategy mappingStrategy, SerializedProperty mosaicProperty, SerializedProperty importHidden, SerializedProperty fileNodeImportState)
            : base(treeViewState)
        {
            m_Layers = layers;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            baseIndent = 32 + k_LeftMargin;
            useScrollView = true;
            m_RootName = rootName;
            m_PSDLayerImportSetting = psdLayerImportSetting;
            m_MappingStrategy = mappingStrategy;
            m_MosaicProperty = mosaicProperty;
            m_ImportHidden = importHidden;
            m_FileNodeImportState = fileNodeImportState;
            Reload();
        }
        
        public override void OnGUI(Rect rect)
        {
            if(m_PSDLayerImportSetting.arraySize != m_LastArraySize)
                Reload();
            base.OnGUI(rect);
            m_LastArraySize = m_PSDLayerImportSetting.arraySize;
        }
        
        protected override TreeViewItem BuildRoot()
        {
            m_PSDLayerImportSetting.serializedObject.Update();
            m_LastArraySize = m_PSDLayerImportSetting.arraySize;
            var root = new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
            var fileRoot = new PSDFileNode(m_MosaicProperty, m_FileNodeImportState)
            {
                id = -2, displayName = m_RootName
            };
            
            //fileRoot.icon = EditorGUIUtility.IconContent("Texture Icon").image as Texture2D;
            root.AddChild(fileRoot);
            var spWrapper = new List<PSDLayerImportSettingSerializedPropertyWrapper>();
            if (m_PSDLayerImportSetting.arraySize > 0)
            {
                var firstElement = m_PSDLayerImportSetting.GetArrayElementAtIndex(0);
                for (int i = 0; i < m_PSDLayerImportSetting.arraySize; ++i)
                {
                    spWrapper.Add(new PSDLayerImportSettingSerializedPropertyWrapper(firstElement, m_PSDLayerImportSetting, null));
                    firstElement.Next(false);
                }
            }
            if (m_Layers != null)
            {
                PSDNode[] nodes = new PSDNode[m_Layers.Length];
                for(int i = 0; i < m_Layers.Length; ++i)
                {
                    var l = m_Layers[i];
                    var importSettingIndex = spWrapper.FindIndex(x => m_MappingStrategy.Compare(x, l));
                    PSDLayerImportSettingSerializedPropertyWrapper importSetting = null;
                    if (importSettingIndex < 0)
                    {
                        importSetting = new PSDLayerImportSettingSerializedPropertyWrapper(null, m_PSDLayerImportSetting, l)
                        {
                            wasLayerImported = l.isVisible || m_ImportHidden.boolValue
                        };
                    }
                    else
                    {
                        importSetting = spWrapper[importSettingIndex];
                        spWrapper.RemoveAt(importSettingIndex);
                    }
                         
                    if (l!= null && l.isGroup)
                        nodes[i] = new PSDLayerGroupNode(l, i, importSetting);
                    else
                        nodes[i] = new PSDLayerNode(l, i, importSetting);
                    var node = nodes[i];

                    node.disable = !node.layer.isVisible;
                    while (node.layer.parentIndex != -1 && nodes[i].disable == false)
                    {
                        if (!node.layer.isVisible || !nodes[node.layer.parentIndex].layer.isVisible)
                        {
                            nodes[i].disable = true;
                        }

                        node = nodes[node.layer.parentIndex];
                    }
                }
                foreach (var node in nodes)
                {
                    TreeViewItem rootNode = null;
                    if (node.layer.parentIndex == -1)
                    {
                        rootNode = fileRoot;
                    }
                    else
                    {
                        rootNode = nodes[node.layer.parentIndex];
                    }
                    rootNode.AddChild(node);
                    if (node is PSDCollapsableNode)
                    {
                        var nodeCollapsable = (PSDCollapsableNode)node;
                        var parentCollapsableNode = node.layer.parentIndex < 0 ? fileRoot : nodes[node.layer.parentIndex] as PSDCollapsableNode;
                        nodeCollapsable.collapseStateChange.parent = parentCollapsableNode?.collapseStateChange;
                        if(nodeCollapsable.flatten)
                            parentCollapsableNode?.collapseStateChange.ChangeState(nodeCollapsable.flatten);
                    }
                }    
            }
            SetupDepthsFromParentsAndChildren(root);
            SetExpanded(fileRoot.id, true);
            return root;
        }
        
        protected override void RowGUI(RowGUIArgs args)
        {
            var node = (PSDNode)args.item;
            var rowRect = args.rowRect;
            var hover = rowRect.Contains(Event.current.mousePosition);
            var a1 = args.focused;
            var a2 = args.selected;
            if (hover && Event.current.type == EventType.Repaint)
            {
                args.selected = args.focused = true;
                Style.hoverLine.Draw(rowRect, false, false, a2, true);
            }
            
            rowRect.x += k_LeftMargin;
            using (new EditorGUI.DisabledScope(node.disable))
            {
                if (node.disable)
                {
                    var r = new Rect(rowRect.x + args.item.depth * this.depthIndentWidth + this.foldoutWidth + 32, rowRect.y, rowRect.width, rowRect.height);
                    GUI.Label(r, Style.layerHiddenToolTip);
                }
                base.RowGUI(args);
            }
            
            args.focused = a1;
            args.selected = a2;

            if (node.disable && !m_ImportHidden.boolValue && node.importLayer)
            {
                GUI.Box(new Rect(rowRect.x + Style.iconSize, rowRect.y, Style.iconSize, Style.iconSize), Style.hiddenLayerNotImportWarning, Style.flattenToggleStyle);
            }
            
            var psdNode = args.item as PSDNode;
            Rect toggleRect = new Rect(rowRect.x , rowRect.y, Style.iconSize, Style.iconSize);
            
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = psdNode.importState.childNodeStateCount > 0;
            var importLayer = EditorGUI.Toggle(toggleRect, psdNode.importLayer);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                SetChildrenNodeImport(psdNode, importLayer);
                var parent = psdNode.parent as PSDCollapsableNode;
                if (importLayer)
                {
                    while (parent != null)
                    {
                        parent.importLayer = true;
                        parent = parent.parent as PSDCollapsableNode;
                    }
                }
                else
                {
                    // if parent's children are all off, we turn off parent
                    while (parent != null)
                    {
                        var import = false;
                        foreach(var c in parent.children)
                        {
                            var n = (PSDNode)c;
                            if (n.importLayer)
                            {
                                import = true;
                                break;
                            }
                        }
                        parent.importLayer = import;
                        parent = parent.parent as PSDCollapsableNode;
                    }
                }
            }

            if (args.item is PSDCollapsableNode )
            {
                var group = (PSDCollapsableNode)args.item;
                toggleRect = new Rect(rowRect.x + args.item.depth * this.depthIndentWidth + Style.iconSize, rowRect.y, Style.iconSize, Style.iconSize);
                GUIContent[] icon = null;
                if (group.collapseStateChange.childNodeStateCount != 0)
                    icon = Style.mergedMix;
                if (hover)
                {
                    if(group.flatten)
                        icon = Style.separateIcon;
                    else
                        icon = Style.mergedIcon;
                }
                else if(group.flatten)
                    icon = Style.mergedIcon;
                    
                if (icon != null)
                {
                    hover = toggleRect.Contains(Event.current.mousePosition);
                    var iconIndex = args.selected | hover ? 1 : 0;
                    EditorGUI.BeginChangeCheck();
                    var flatten = GUI.Toggle(toggleRect, group.flatten, icon[iconIndex], Style.flattenToggleStyle);
                    var flattenNotSame = flatten != group.collapseStateChange.state;
                    if (EditorGUI.EndChangeCheck() || flattenNotSame)
                    {
                        group.flatten = flatten;
                        group.collapseStateChange.ChangeState(flatten);
                    }

                    if (flattenNotSame)
                        Repaint();
                }
            }
        }

        void SetChildrenNodeImport(PSDNode node, bool value)
        {
            node.importLayer = value;
            node.importState.childNodeStateCount = 0;
            if (node.children != null)
            {
                foreach (var c in node.children)
                {
                    var p = (PSDNode)c;
                    p.importLayer = value;
                    SetChildrenNodeImport(p, value);
                }    
            }
        }
    }
}

