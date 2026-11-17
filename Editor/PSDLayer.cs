using System;
using Unity.Collections;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    // The layer fields only an import can compute, kept by both the importer record and the last import's data.
    interface IPSDLayerImportResult
    {
        bool isImported { get; set; }
        bool flatten { get; set; }
        Vector2Int mosaicPosition { get; set; }
        string spriteName { get; set; }
    }

    [Serializable]
    class PSDLayer : IPSDLayerMappingStrategyComparable, IPSDLayerImportResult
    {
        [SerializeField]
        string m_Name;
        [SerializeField]
        string m_SpriteName;
        [SerializeField]
        bool m_IsGroup;
        [SerializeField]
        int m_ParentIndex;
        [SerializeField]
        string m_SpriteID;
        [SerializeField]
        int m_LayerID;
        [SerializeField]
        Vector2Int m_MosaicPosition;
        [SerializeField]
        bool m_Flatten;
        [SerializeField]
        bool m_IsImported;
        [SerializeField]
        bool m_IsVisible;

        [NonSerialized]
        Vector2 m_LayerPosition;
        [NonSerialized]
        GameObject m_GameObject;

        public PSDLayer(NativeArray<Color32> tex, int parent, bool group, string layerName, int width, int height, int id, bool hidden)
        {
            isGroup = group;
            parentIndex = parent;
            texture = tex;
            name = layerName;
            this.width = width;
            this.height = height;
            layerID = id;
            m_Flatten = false;
            m_IsImported = false;
            m_IsVisible = hidden;
            m_SpriteID = new GUID().ToString();
        }

        public PSDLayer(PSDLayer layer)
        {
            m_Name = layer.m_Name;
            m_SpriteName = layer.m_SpriteName;
            m_IsGroup = layer.m_IsGroup;
            m_ParentIndex = layer.m_ParentIndex;
            m_SpriteID = layer.m_SpriteID;
            m_LayerID = layer.m_LayerID;
            m_MosaicPosition = layer.m_MosaicPosition;
            m_Flatten = layer.m_Flatten;
            m_IsImported = layer.m_IsImported;
            m_IsVisible = layer.m_IsVisible;
            m_LayerPosition = layer.m_LayerPosition;
            m_GameObject = layer.m_GameObject;
            width = layer.width;
            height = layer.height;
            texture = layer.texture;
        }

        public PSDLayer(PSDLayerData data)
        {
            m_Name = data.name;
            m_IsGroup = data.isGroup;
            m_ParentIndex = data.parentIndex;
            m_SpriteID = data.spriteID.ToString();
            m_LayerID = data.layerID;
            m_IsVisible = data.isVisible;
            CopyImportResult(data, this);
        }

        public static void CopyImportResult(IPSDLayerImportResult source, IPSDLayerImportResult destination)
        {
            destination.isImported = source.isImported;
            destination.flatten = source.flatten;
            destination.mosaicPosition = source.mosaicPosition;
            destination.spriteName = source.spriteName;
        }

        // Compares every field the .meta keeps for a layer.
        public bool IsSameRecord(PSDLayer other)
        {
            return spriteID == other.spriteID && m_LayerID == other.m_LayerID && m_Name == other.m_Name &&
                m_IsGroup == other.m_IsGroup && m_ParentIndex == other.m_ParentIndex && m_IsVisible == other.m_IsVisible &&
                m_IsImported == other.m_IsImported && m_Flatten == other.m_Flatten &&
                m_MosaicPosition == other.m_MosaicPosition && m_SpriteName == other.m_SpriteName;
        }

        public void AppendRecord(ref Hash128 hash)
        {
            hash.Append(m_SpriteID ?? string.Empty);
            hash.Append(m_LayerID);
            hash.Append(m_Name ?? string.Empty);
            hash.Append(m_MosaicPosition.x);
            hash.Append(m_MosaicPosition.y);
            hash.Append(m_SpriteName ?? string.Empty);
            hash.Append(m_ParentIndex);
            hash.Append(m_IsGroup ? 1 : 0);
            hash.Append(m_IsVisible ? 1 : 0);
            hash.Append(m_IsImported ? 1 : 0);
            hash.Append(m_Flatten ? 1 : 0);
        }

        public bool isVisible => m_IsVisible;
        public int layerID { get { return m_LayerID; } private set { m_LayerID = value; } }

        public string name { get { return m_Name; } private set { m_Name = value; } }
        public string spriteName { get { return m_SpriteName; } set { m_SpriteName = value; } }
        public bool isGroup { get { return m_IsGroup; } private set { m_IsGroup = value; } }
        public int parentIndex { get { return m_ParentIndex; } private set { m_ParentIndex = value; } }
        public Vector2Int mosaicPosition { get { return m_MosaicPosition; } set { m_MosaicPosition = value; } }
        public GUID spriteID { get { return new GUID(m_SpriteID); } set { m_SpriteID = value.ToString(); } }
        public Vector2 layerPosition { get => m_LayerPosition; set => m_LayerPosition = value; }
        public GameObject gameObject { get { return m_GameObject; } set { m_GameObject = value; } }

        public bool flatten
        {
            get => m_Flatten;
            set => m_Flatten = value;
        }

        public bool isImported
        {
            get => m_IsImported;
            set => m_IsImported = value;
        }

        public NativeArray<Color32> texture { get; set; }
        public int width { get; set; }
        public int height { get; set; }

        public void Dispose()
        {
            if (texture.IsCreated)
                texture.Dispose();
        }
    }
}
