using System;
using System.Collections.Generic;
using PDNWrapper;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    // This is to provide a readonly data without modifilying the original code that wil affect serialization.
    interface IPSDLayerImportSettingRecord : IPSDLayerMappingStrategyComparable
    {
        string Name { get; }
        int LayerId { get; }
        bool Flatten { get; }
        bool IsGroup { get; }
        bool ImportLayer { get; }
    }

    /// <summary>
    /// Custom hidden asset to store meta information of the last import state
    /// </summary>
    internal class PSDImportData : ScriptableObject
    {
        [SerializeField]
        int m_ImportedTextureWidth;
        public int importedTextureWidth
        {
            get => m_ImportedTextureWidth;
            set => m_ImportedTextureWidth = value;
        }

        [SerializeField]
        int m_ImportedTextureHeight;
        public int importedTextureHeight
        {
            get => m_ImportedTextureHeight;
            set => m_ImportedTextureHeight = value;
        }

        [SerializeField]
        Vector2Int m_DocumentSize;
        public Vector2Int documentSize
        {
            get => m_DocumentSize;
            set => m_DocumentSize = value;
        }

        [SerializeField]
        int m_TextureActualHeight;
        public int textureActualHeight
        {
            get => m_TextureActualHeight;
            set => m_TextureActualHeight = value;
        }

        [SerializeField]
        int m_TextureActualWidth;
        public int textureActualWidth
        {
            get => m_TextureActualWidth;
            set => m_TextureActualWidth = value;
        }

        [SerializeField]
        TextureImporterSettings m_SingleSpriteTextureImporterSettings;
        public TextureImporterSettings singleSpriteTextureImporterSettings
        {
            get => m_SingleSpriteTextureImporterSettings;
            set => m_SingleSpriteTextureImporterSettings = value;
        }

        [SerializeField]
        PSDImporter.ELayerMappingOption m_MappingOption = PSDImporter.ELayerMappingOption.Unknown;

        public PSDImporter.ELayerMappingOption mappingOption
        {
            get => m_MappingOption;
            private set => m_MappingOption = value;
        }

        [SerializeField]
        PSDLayerData[] m_PsdLayerData;
        public PSDLayerData[] psdLayerData => m_PsdLayerData;

        public void CreatePSDLayerData(List<BitmapLayer> bitmapLayer)
        {
            List<PSDLayerData> layerData = new List<PSDLayerData>();
            foreach (BitmapLayer fileLayer in bitmapLayer)
            {
                CreatePSDLayerData(fileLayer, layerData);
            }
            m_PsdLayerData = layerData.ToArray();
        }

        public void MapLayerToPreviousImport(PSDImporter.ELayerMappingOption layerMappingOption, IReadOnlyList<PSDLayer> previousPSDLayer)
        {
            mappingOption = layerMappingOption;
            if (mappingOption == PSDImporter.ELayerMappingOption.Unknown)
            {
                // determine the best mapping option.
                // if layer id is unique, we use UseLayerId
                // else use UseLayerNameCaseSensitive
                IPSDLayerMappingStrategy useIdStrategy = ImportUtilities.GetLayerMappingStrategy(PSDImporter.ELayerMappingOption.UseLayerId);
                if (string.IsNullOrEmpty(useIdStrategy.LayersUnique(m_PsdLayerData)))
                    mappingOption = PSDImporter.ELayerMappingOption.UseLayerId;
                else
                    mappingOption = PSDImporter.ELayerMappingOption.UseLayerNameCaseSensitive;

            }

            this.mappingOption = mappingOption;
            IPSDLayerMappingStrategy mappingStrategy = ImportUtilities.GetLayerMappingStrategy(mappingOption);
            // Layers sharing an identifier, e.g. duplicated layer names, pair one to one with the
            // previous import in document order so that each layer keeps its own Sprite ID. Generated
            // IDs stay deterministic but are salted by document order so they never collide.
            HashSet<PSDLayer> mappedPreviousLayers = new HashSet<PSDLayer>();
            HashSet<GUID> assignedSpriteIds = new HashSet<GUID>();
            foreach (PSDLayerData layer in m_PsdLayerData)
            {
                GUID spriteId;
                int index = previousPSDLayer.FindIndex(x => !mappedPreviousLayers.Contains(x) && mappingStrategy.Compare(x, layer));
                if (index >= 0)
                {
                    mappedPreviousLayers.Add(previousPSDLayer[index]);
                    spriteId = previousPSDLayer[index].spriteID;
                }
                else
                {
                    spriteId = mappingStrategy.GenerateGUID(layer);
                    while (assignedSpriteIds.Contains(spriteId))
                        spriteId = LayerMappingUseLayerName.StringToGUID(spriteId.ToString());
                }
                assignedSpriteIds.Add(spriteId);
                layer.spriteID = spriteId;
            }
        }

        void CreatePSDLayerData(BitmapLayer layer, List<PSDLayerData> layerData, int parentIndex = -1)
        {
            layerData.Add(new PSDLayerData()
            {
                isGroup = layer.IsGroup,
                isVisible = layer.Visible,
                layerID = layer.LayerID,
                name = layer.Name,
                parentIndex = parentIndex,
                layerSizeOnFile = new Vector2Int(layer.documentRect.Width, layer.documentRect.Height)
            });
            parentIndex = layerData.Count - 1;
            foreach (BitmapLayer fileLayer in layer.ChildLayer)
            {
                CreatePSDLayerData(fileLayer, layerData, parentIndex);
            }
        }

        [SerializeField]
        SpriteMetaData[] m_SpriteRects = Array.Empty<SpriteMetaData>();

        public IReadOnlyList<SpriteMetaData> spriteRects
        {
            get => m_SpriteRects;
            set
            {
                m_SpriteRects = new SpriteMetaData[value.Count];
                for (int i = 0; i < m_SpriteRects.Length; ++i)
                    m_SpriteRects[i] = value[i];
            }
        }
    }

    // Struct to keep track of GOs and bone
    internal struct BoneGO
    {
        public GameObject go;
        public int index;
    }

    /// <summary>
    /// Capture per layer import settings
    /// </summary>
    [Serializable]
    class PSDLayerImportSetting : IPSDLayerImportSettingRecord
    {
        public string name;
        public int layerId;
        public bool flatten;
        public bool isGroup;
        public bool importLayer;
        public int layerID => layerId;
        string IPSDLayerMappingStrategyComparable.name => name;
        bool IPSDLayerMappingStrategyComparable.isGroup => isGroup;

        public string Name => name;
        public int LayerId => layerId;
        public bool Flatten => flatten;
        public bool IsGroup => isGroup;
        public bool ImportLayer => importLayer;

        public PSDLayerImportSetting() { }

        public PSDLayerImportSetting(IPSDLayerImportSettingRecord record)
        {
            name = record.Name;
            layerId = record.LayerId;
            flatten = record.Flatten;
            isGroup = record.IsGroup;
            importLayer = record.ImportLayer;
        }
    }

    /// <summary>
    /// PSDLayer data for PSDImportData for last import state
    /// </summary>
    [Serializable]
    class PSDLayerData : IPSDLayerMappingStrategyComparable
    {
        [SerializeField]
        string m_Name;
        public string name
        {
            get => m_Name;
            set => m_Name = value;
        }

        [SerializeField]
        int m_ParentIndex;
        public int parentIndex
        {
            get => m_ParentIndex;
            set => m_ParentIndex = value;
        }

        [SerializeField]
        int m_LayerID;
        public int layerID
        {
            get => m_LayerID;
            set => m_LayerID = value;
        }

        [SerializeField]
        bool m_IsVisible;
        public bool isVisible
        {
            get => m_IsVisible;
            set => m_IsVisible = value;
        }

        [SerializeField]
        bool m_IsGroup;
        public bool isGroup
        {
            get => m_IsGroup;
            set => m_IsGroup = value;
        }

        [SerializeField]
        bool m_IsImported;
        public bool isImported
        {
            get => m_IsImported;
            set => m_IsImported = value;
        }

        [SerializeField]
        Vector2Int m_LayerSizeOnFile;
        public Vector2Int layerSizeOnFile
        {
            get => m_LayerSizeOnFile;
            set => m_LayerSizeOnFile = value;
        }

        // This is to store the spriteid that was assigned to this layer.
        [SerializeField]
        GUID m_SpriteID;
        public GUID spriteID
        {
            get => m_SpriteID;
            set => m_SpriteID = value;
        }
        public bool IsEmpty => !(layerSizeOnFile.x > 0 && layerSizeOnFile.y > 0);
    }

    /// <summary>
    /// Data for extracting layers and colors from PSD
    /// </summary>
    class PSDExtractLayerData
    {
        public BitmapLayer bitmapLayer;
        public PSDLayerImportSetting importSetting;
        public GUID spriteId;
        public PSDExtractLayerData[] children;
    }
}
