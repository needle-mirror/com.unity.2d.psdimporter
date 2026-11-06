using System;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
#if ENABLE_2D_ANIMATION
using UnityEditor.U2D.Animation;
#endif
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    enum ProducePrefabMode
    {
        None,
        Prefab,
        Paperdoll
    }

    /// <summary>
    /// Interface to provide import configuration data for PSDImporter.
    /// </summary>
    interface IImportConfigProvider
    {
        bool importFlatten { get; }
        IReadOnlyList<PSDLayerData> psdLayerData { get; }
        IReadOnlyList<IPSDLayerImportSettingRecord> layerImportSettings { get; }
        bool importHiddenLayers { get; }
        bool generatePhysicsShape { get; }
        float definitionScale { get; }
        float pixelsPerUnit { get; }
        ISpritePhysicsOutlineDataProvider physicsOutlineDataProvider { get; }
        Vector2Int canvasSize { get; }
        IReadOnlyList<SpriteMetaData> spriteMetaData { get; }
        IReadOnlyList<SpriteMetaData> importedSpriteMetaData { get; }
        PSDImporter.ELayerMappingOption layerMappingOption { get; }
        Action<LogType, string> logger { get; }
        SpriteAlignment singleModeSpriteAlignment { get; }
        Vector2 singleModeSpritePivot { get; }
        bool keepDuplicateSpriteName { get; }
        bool RequireSquarePOT(AssetImportContext ctx);
        ScriptableObject pipeline { get; }
        int padding { get; }
        ushort spriteSizeExpand { get; }
        ProducePrefabMode producePrefabMode { get; }

        IReadOnlyList<PSDExtractLayerData> extractedLayerData { get; }

        IReadOnlyList<PSDLayer> obsoletePsdLayers { get; }
        bool inCharacterMode { get; }

        bool generateGOHierarchy { get; }
#if ENABLE_2D_ANIMATION
        CharacterData characterData { get; }
        SpriteCategoryList spriteCategoryList { get; }
#endif
    }

    // PSDImporter class that implements the IImportConfigProvider interface to provide import configuration data for PSD files.
    public partial class PSDImporter : IImportConfigProvider
    {
        IReadOnlyList<PSDLayerData> IImportConfigProvider.psdLayerData => importData.psdLayerData;

        bool IImportConfigProvider.importFlatten
        {
            get
            {
                bool singleSpriteMode = m_TextureImporterSettings.textureType == TextureImporterType.Sprite && m_TextureImporterSettings.spriteMode != (int)SpriteImportMode.Multiple;
                return m_TextureImporterSettings.textureType != TextureImporterType.Sprite || m_MosaicLayers == false || singleSpriteMode;
            }
        }

        IReadOnlyList<IPSDLayerImportSettingRecord> IImportConfigProvider.layerImportSettings => m_PSDLayerImportSetting;
        bool IImportConfigProvider.importHiddenLayers => m_ImportHiddenLayers;
        bool IImportConfigProvider.generatePhysicsShape => m_GeneratePhysicsShape;
        ISpritePhysicsOutlineDataProvider IImportConfigProvider.physicsOutlineDataProvider => GetDataProvider<ISpritePhysicsOutlineDataProvider>();
        IReadOnlyList<SpriteMetaData> IImportConfigProvider.spriteMetaData => GetStoredSpriteRectData();
        IReadOnlyList<SpriteMetaData> IImportConfigProvider.importedSpriteMetaData => importData.spriteRects;
        Action<LogType, string> IImportConfigProvider.logger => Logger;
        SpriteAlignment IImportConfigProvider.singleModeSpriteAlignment => (SpriteAlignment)m_TextureImporterSettings.spriteAlignment;
        Vector2 IImportConfigProvider.singleModeSpritePivot => m_TextureImporterSettings.spritePivot;
        bool IImportConfigProvider.keepDuplicateSpriteName => m_KeepDuplicateSpriteName;

        bool IImportConfigProvider.RequireSquarePOT(AssetImportContext ctx)
        {

#pragma warning disable CS0618
            TextureImporterPlatformSettings platformSettings = TextureImporterUtilities.GetPlatformTextureSettings(ctx.selectedBuildTarget, in m_PlatformSettings);
            return (TextureImporterFormat.PVRTC_RGB2 <= platformSettings.format && platformSettings.format <= TextureImporterFormat.PVRTC_RGBA4);
#pragma warning restore CS0618

        }

        ScriptableObject IImportConfigProvider.pipeline => m_Pipeline;
        int IImportConfigProvider.padding => m_Padding;


        float IImportConfigProvider.definitionScale => this.definitionScale;
        float IImportConfigProvider.pixelsPerUnit => this.pixelsPerUnit;
        ushort IImportConfigProvider.spriteSizeExpand => m_SpriteSizeExpand;

        //PSDImportData ImportParameters.importData => this.importData;
        Vector2Int IImportConfigProvider.canvasSize => this.canvasSize;
        ELayerMappingOption IImportConfigProvider.layerMappingOption => this.layerMappingOption;

        ProducePrefabMode IImportConfigProvider.producePrefabMode
        {
            get
            {
                if (!shouldProduceGameObject)
                    return ProducePrefabMode.None;
                if (m_PaperDollMode)
                    return ProducePrefabMode.Paperdoll;
                return ProducePrefabMode.Prefab;
            }
        }

        IReadOnlyList<PSDExtractLayerData> IImportConfigProvider.extractedLayerData => m_ExtractData;

        IReadOnlyList<PSDLayer> IImportConfigProvider.obsoletePsdLayers => GetPSDLayers();
        bool IImportConfigProvider.inCharacterMode => inCharacterMode;
        bool IImportConfigProvider.generateGOHierarchy => m_GenerateGOHierarchy;
#if ENABLE_2D_ANIMATION
        CharacterData IImportConfigProvider.characterData => GetDataProvider<ICharacterDataProvider>().GetCharacterData();
        SpriteCategoryList IImportConfigProvider.spriteCategoryList => m_SpriteCategoryList;
#endif
    }
}
