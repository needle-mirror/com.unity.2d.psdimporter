using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEditor.U2D.Sprites;

#if ENABLE_2D_ANIMATION
using UnityEditor.U2D.Animation;
#endif

namespace UnityEditor.U2D.PSD
{
    public partial class PSDImporter : ScriptedImporter, ISpriteEditorDataProvider
    {
        SpriteImportMode ISpriteEditorDataProvider.spriteImportMode => spriteImportModeToUse;
        UnityEngine.Object ISpriteEditorDataProvider.targetObject => targetObject;
        internal UnityEngine.Object targetObject => this;

        /// <summary>
        /// Implementation for ISpriteEditorDataProvider.pixelsPerUnit.
        /// </summary>
        float ISpriteEditorDataProvider.pixelsPerUnit => pixelsPerUnit;
        internal float pixelsPerUnit => m_TextureImporterSettings.spritePixelsPerUnit;

        /// <summary>
        /// Implementation for ISpriteEditorDataProvider.GetDataProvider.
        /// </summary>
        /// <typeparam name="T">Data provider type to retrieve.</typeparam>
        /// <returns></returns>
        T ISpriteEditorDataProvider.GetDataProvider<T>()
        {
            return GetDataProvider<T>();
        }

        internal T GetDataProvider<T>() where T : class
        {
            if (typeof(T) == typeof(ISpriteBoneDataProvider))
            {
                return new SpriteBoneDataProvider { dataProvider = this } as T;
            }
            if (typeof(T) == typeof(ISpriteMeshDataProvider))
            {
                return new SpriteMeshDataProvider { dataProvider = this } as T;
            }
            if (typeof(T) == typeof(ISpriteOutlineDataProvider))
            {
                return new SpriteOutlineDataProvider { dataProvider = this } as T;
            }
            if (typeof(T) == typeof(ISpritePhysicsOutlineDataProvider))
            {
                return new SpritePhysicsOutlineProvider { dataProvider = this } as T;
            }
            if (typeof(T) == typeof(ITextureDataProvider))
            {
                return new TextureDataProvider { dataProvider = this } as T;
            }
            if (typeof(T) == typeof(ISecondaryTextureDataProvider))
            {
                return new SecondaryTextureDataProvider() { dataProvider = this } as T;
            }
#if USE_SPRITE_FRAME_CAPABILITY
            if (typeof(T) == typeof(ISpriteFrameEditCapability))
            {
                return new SpriteFrameEditCapabilityDataProvider() { dataProvider = this } as T;
            }
#endif
#if ENABLE_2D_ANIMATION
            if (typeof(T) == typeof(ICharacterDataProvider))
            {
                return inCharacterMode ? new CharacterDataProvider { dataProvider = this } as T : null;
            }
            if (typeof(T) == typeof(IMainSkeletonDataProvider))
            {
                return inCharacterMode && skeletonAsset != null ? new MainSkeletonDataProvider() { dataProvider = this } as T : null;
            }
#endif
            else
                return this as T;
        }

        /// <summary>
        /// Implementation for ISpriteEditorDataProvider.HasDataProvider.
        /// </summary>
        /// <param name="type">Data provider type to query.</param>
        /// <returns>True if data provider is supported, false otherwise.</returns>
        bool ISpriteEditorDataProvider.HasDataProvider(Type type)
        {
            return HasDataProvider(type);
        }

        internal bool HasDataProvider(Type type)
        {
#if ENABLE_2D_ANIMATION
            if (inCharacterMode)
            {
                if (type == typeof(ICharacterDataProvider))
                    return true;
                if (type == typeof(IMainSkeletonDataProvider) && skeletonAsset != null)
                    return true;
            }
#endif
            if (type == typeof(ISpriteBoneDataProvider) ||
                type == typeof(ISpriteMeshDataProvider) ||
                type == typeof(ISpriteOutlineDataProvider) ||
                type == typeof(ISpritePhysicsOutlineDataProvider) ||
                type == typeof(ITextureDataProvider) ||
                type == typeof(ISecondaryTextureDataProvider))
            {
                return true;
            }
            else
                return type.IsAssignableFrom(GetType());
        }

        /// <summary>
        /// Implementation for ISpriteEditorDataProvider.Apply.
        /// </summary>
        void ISpriteEditorDataProvider.Apply()
        {
            Apply();
        }

        /// <summary>
        /// Implementation for ISpriteEditorDataProvider.InitSpriteEditorDataProvider.
        /// </summary>
        void ISpriteEditorDataProvider.InitSpriteEditorDataProvider()
        {
            InitSpriteEditorDataProvider();
        }

        void InitSpriteEditorDataProvider() { }

        /// <summary>
        /// Implementation for ISpriteEditorDataProvider.GetSpriteRects.
        /// </summary>
        /// <returns>An array of SpriteRect for the current import mode.</returns>
        SpriteRect[] ISpriteEditorDataProvider.GetSpriteRects()
        {
            return GetSpriteRects();
        }

        internal SpriteRect[] GetSpriteRects()
        {
            if (spriteImportModeToUse == SpriteImportMode.Multiple)
                return GetSpriteImportData().Select(x => new SpriteMetaData(x) as SpriteRect).ToArray();

            return new[] { GetSingleSpriteImportData() };
        }

        /// <summary>
        /// Implementation for ISpriteEditorDataProvider.SetSpriteRects.
        /// </summary>
        /// <param name="spriteRects">Set the SpriteRect data for the current import mode.</param>
        void ISpriteEditorDataProvider.SetSpriteRects(SpriteRect[] spriteRects)
        {
            SetSpriteRects(spriteRects);
        }

        internal void SetSpriteRects(SpriteRect[] spriteRects)
        {
            if (spriteImportModeToUse == SpriteImportMode.Multiple)
            {
                IReadOnlyList<SpriteMetaData> spriteImportData = GetSpriteImportData();
                List<SpriteMetaData> newSpriteImportData = new List<SpriteMetaData>(spriteRects.Length);
                foreach (SpriteRect sr in spriteRects)
                {
                    SpriteMetaData importData = spriteImportData.FirstOrDefault(x => x.spriteID == sr.spriteID);
                    if (importData == null)
                    {
                        // In mosaic mode the layers decide which Sprites exist.
                        if (inMosaicMode)
                            continue;
                        importData = new SpriteMetaData(sr);
                    }
                    else
                    {
                        // In mosaic mode the name and the rect come from the layer (see ISpriteFrameEditCapability).
                        if (!inMosaicMode)
                        {
                            importData.name = sr.name;
                            importData.rect = sr.rect;
                        }
                        importData.alignment = sr.alignment;
                        importData.border = sr.border;
                        importData.pivot = sr.pivot;
                    }
                    newSpriteImportData.Add(importData);
                }
                // The rects cover only imported layers; keep the data of layers that are hidden or removed for now.
                WriteToSpriteImportData(newSpriteImportData, !inMosaicMode);
            }
            else if (spriteRects.Length == 1 && (spriteImportModeToUse == SpriteImportMode.Single || spriteImportModeToUse == SpriteImportMode.Polygon))
            {
                // The single Sprite takes its name and rect from the asset, so only the texture settings change.
                if (GetSingleSpriteImportData().spriteID == spriteRects[0].spriteID)
                {
                    RecordLastImport();
                    m_TextureImporterSettings.spriteAlignment = (int)spriteRects[0].alignment;
                    m_TextureImporterSettings.spriteBorder = spriteRects[0].border;
                    m_TextureImporterSettings.spritePivot = spriteRects[0].pivot;
                }
            }
        }
    }
}
