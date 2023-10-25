using System;
using System.Collections.Generic;
using System.IO;
using PDNWrapper;
using UnityEngine;
using Unity.Collections;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEditor.U2D.Animation;
using UnityEditor.U2D.Common;
using UnityEditor.U2D.Sprites;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEditor.U2D.PSD
{
    /// <summary>
    /// ScriptedImporter to import Photoshop files
    /// </summary>
    // Version using unity release + 5 digit padding for future upgrade. Eg 2021.2 -> 21200000
    [ScriptedImporter(22200003, new string[]{"psb"}, new []{"psd"}, AllowCaching = true)]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.2d.psdimporter@latest")]
    [MovedFrom("UnityEditor.Experimental.AssetImporters")]
    public partial class PSDImporter : ScriptedImporter, ISpriteEditorDataProvider
    {
        internal enum ELayerMappingOption
        {
            UseLayerName,
            UseLayerNameCaseSensitive,
            UseLayerId
        }

        IPSDLayerMappingStrategy[] m_MappingCompare =
        {
            new LayerMappingUseLayerName(),
            new LayerMappingUseLayerNameCaseSensitive(),
            new LayerMappingUserLayerID(),
        };

        [SerializeField]
        TextureImporterSettings m_TextureImporterSettings = new TextureImporterSettings()
        {
            mipmapEnabled = true,
            mipmapFilter = TextureImporterMipFilter.BoxFilter,
            sRGBTexture = true,
            borderMipmap = false,
            mipMapsPreserveCoverage = false,
            alphaTestReferenceValue = 0.5f,
            readable = false,

#if ENABLE_TEXTURE_STREAMING
            streamingMipmaps = true,
#endif

            fadeOut = false,
            mipmapFadeDistanceStart = 1,
            mipmapFadeDistanceEnd = 3,

            convertToNormalMap = false,
            heightmapScale = 0.25F,
            normalMapFilter = 0,

            generateCubemap = TextureImporterGenerateCubemap.AutoCubemap,
            cubemapConvolution = 0,

            seamlessCubemap = false,

            npotScale = TextureImporterNPOTScale.ToNearest,

            spriteMode = (int)SpriteImportMode.Multiple,
            spriteExtrude = 1,
            spriteMeshType = SpriteMeshType.Tight,
            spriteAlignment = (int)SpriteAlignment.Center,
            spritePivot = new Vector2(0.5f, 0.5f),
            spritePixelsPerUnit = 100.0f,
            spriteBorder = new Vector4(0.0f, 0.0f, 0.0f, 0.0f),

            alphaSource = TextureImporterAlphaSource.FromInput,
            alphaIsTransparency = true,
            spriteTessellationDetail = -1.0f,

            textureType = TextureImporterType.Sprite,
            textureShape = TextureImporterShape.Texture2D,

            filterMode = FilterMode.Bilinear,
            aniso = 1,
            mipmapBias = 0.0f,
            wrapModeU = TextureWrapMode.Repeat,
            wrapModeV = TextureWrapMode.Repeat,
            wrapModeW = TextureWrapMode.Repeat,
            swizzleR = TextureImporterSwizzle.R,
            swizzleG = TextureImporterSwizzle.G,
            swizzleB = TextureImporterSwizzle.B,
            swizzleA = TextureImporterSwizzle.A,
        };
        
        [SerializeField]
        // SpriteData for both single and multiple mode
        List<SpriteMetaData> m_SpriteImportData = new List<SpriteMetaData>(); // we use index 0 for single sprite and the rest for multiple sprites
        [SerializeField]
        // SpriteData for Mosaic mode
        List<SpriteMetaData> m_MosaicSpriteImportData = new List<SpriteMetaData>();
        [SerializeField]
        // SpriteData for Rig mode
        List<SpriteMetaData> m_RigSpriteImportData = new List<SpriteMetaData>();
        [SerializeField]
        // CharacterData for Rig mode
        CharacterData m_CharacterData = new CharacterData();
        [SerializeField]
        // SpriteData for shared rig mode
        List<SpriteMetaData> m_SharedRigSpriteImportData = new List<SpriteMetaData>();
        [SerializeField]
        // CharacterData for shared rig mode
        CharacterData m_SharedRigCharacterData = new CharacterData();

        [SerializeField]
        List<TextureImporterPlatformSettings> m_PlatformSettings = new List<TextureImporterPlatformSettings>();
        [SerializeField]
        bool m_MosaicLayers = true;
        [SerializeField]
        bool m_CharacterMode = true;
        [SerializeField]
        Vector2 m_DocumentPivot = Vector2.zero;
        [SerializeField]
        SpriteAlignment m_DocumentAlignment = SpriteAlignment.BottomCenter;
        [SerializeField]
        bool m_ImportHiddenLayers = false;
        [SerializeField]
        ELayerMappingOption m_LayerMappingOption = ELayerMappingOption.UseLayerId;
        [SerializeField]
        bool  m_GeneratePhysicsShape = false;

        [SerializeField]
        bool m_PaperDollMode = false;

        [SerializeField]
        bool m_KeepDupilcateSpriteName = true;
        
        [SerializeField] 
        string m_SkeletonAssetReferenceID = null;

        [SerializeField]
        int m_Padding = 4;
        
        [SerializeField]
        ushort m_SpriteSizeExpand = 0;

        [SerializeField]
        SpriteCategoryList m_SpriteCategoryList = new SpriteCategoryList() {categories = new List<SpriteCategory>()};
        GameObjectCreationFactory m_GameObjectFactory = new GameObjectCreationFactory(null);

        PSDImportData m_ImportData;

        internal PSDImportData importData
        {
            get
            {
                var returnValue = m_ImportData;
                if (returnValue == null && !PSDImporterAssetPostProcessor.ContainsImporter(this))
                    // Using LoadAllAssetsAtPath because PSDImportData is hidden
                    returnValue = AssetDatabase.LoadAllAssetsAtPath(assetPath).FirstOrDefault(x => x is PSDImportData) as PSDImportData;
                    
                if (returnValue == null)
                    returnValue = ScriptableObject.CreateInstance<PSDImportData>();
                    
                m_ImportData = returnValue;
                return returnValue;
            }
        }

        internal int textureActualWidth
        {
            get => importData.textureActualWidth;
            private set =>importData.textureActualWidth = value; 
        }

        internal int textureActualHeight
        {
            get => importData.textureActualHeight;
            private set =>importData.textureActualHeight = value;
        }

        [SerializeField]
        string m_SpritePackingTag = "";

        [SerializeField]
        bool m_ResliceFromLayer = false;

        [SerializeField]
        List<PSDLayer> m_MosaicPSDLayers = new List<PSDLayer>();
        [SerializeField]
        List<PSDLayer> m_RigPSDLayers = new List<PSDLayer>();
        [SerializeField]
        List<PSDLayer> m_SharedRigPSDLayers = new List<PSDLayer>();
        [SerializeField]
        PSDLayerImportSetting[] m_PSDLayerImportSetting;
        
        // Use for inspector to check if the file node is checked
        [SerializeField]
#pragma warning disable 169, 414
        bool m_ImportFileNodeState = true;
        
        // Used by platform settings to mark it dirty so that it will trigger a reimport
        [SerializeField]
#pragma warning disable 169, 414
        long m_PlatformSettingsDirtyTick;

        [SerializeField]
        bool m_SpriteSizeExpandChanged = false;
        
        [SerializeField]
        bool m_GenerateGOHierarchy = false;

        [SerializeField]
        string m_TextureAssetName = null;

        [SerializeField]
        string m_PrefabAssetName = null;

        [SerializeField]
        string m_SpriteLibAssetName = null;
        
        [SerializeField]
        string m_SkeletonAssetName = null;

        [SerializeField]
        SecondarySpriteTexture[] m_SecondarySpriteTextures;
        
        PSDExtractLayerData[] m_ExtractData;
        
        internal bool isNPOT => Mathf.IsPowerOfTwo(importData.textureActualWidth) && Mathf.IsPowerOfTwo(importData.textureActualHeight);
        
        bool shouldProduceGameObject => m_CharacterMode && m_MosaicLayers && spriteImportModeToUse == SpriteImportMode.Multiple;
        bool shouldResliceFromLayer => m_ResliceFromLayer && m_MosaicLayers && spriteImportModeToUse == SpriteImportMode.Multiple;
        bool inCharacterMode => inMosaicMode && m_CharacterMode;
        
        float definitionScale
        {
            get
            {
                var definitionScaleW = importData.importedTextureWidth / (float)textureActualWidth;
                var definitionScaleH = importData.importedTextureHeight / (float)textureActualHeight;
                return Mathf.Min(definitionScaleW, definitionScaleH);
            }
        }   
        
        internal SecondarySpriteTexture[] secondaryTextures
        {
            get => m_SecondarySpriteTextures;
            set => m_SecondarySpriteTextures = value;
        }

        internal SpriteBone[] mainSkeletonBones
        {
            get
            {
                var skeleton = skeletonAsset;
                return skeleton != null ? skeleton.GetSpriteBones() : null;
            }
        }        

        /// <summary>
        /// PSDImporter constructor.
        /// </summary>
        public PSDImporter()
        {
            m_TextureImporterSettings.swizzleA = TextureImporterSwizzle.A;
            m_TextureImporterSettings.swizzleR = TextureImporterSwizzle.R;
            m_TextureImporterSettings.swizzleG = TextureImporterSwizzle.G;
            m_TextureImporterSettings.swizzleB = TextureImporterSwizzle.B;
        }

        /// <summary>
        /// Implementation of ScriptedImporter.OnImportAsset
        /// </summary>
        /// <param name="ctx">
        /// This argument contains all the contextual information needed to process the import
        /// event and is also used by the custom importer to store the resulting Unity Asset.
        /// </param>
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var fileStream = new FileStream(ctx.assetPath, FileMode.Open, FileAccess.Read);
            Document doc = null;
            
            if(m_ImportData == null)
                m_ImportData = ScriptableObject.CreateInstance<PSDImportData>();
            m_ImportData.hideFlags = HideFlags.HideInHierarchy;
            
            try
            {
                UnityEngine.Profiling.Profiler.BeginSample("OnImportAsset");

                UnityEngine.Profiling.Profiler.BeginSample("PsdLoad");
                doc = PaintDotNet.Data.PhotoshopFileType.PsdLoad.Load(fileStream);
                UnityEngine.Profiling.Profiler.EndSample();
                
                m_ImportData.CreatePSDLayerData(doc.Layers);
                
                ValidatePSDLayerId(doc, m_LayerMappingOption);
                SetDocumentImportData(doc);

                importData.documentSize = new Vector2Int(doc.width, doc.height);
                var singleSpriteMode = m_TextureImporterSettings.textureType == TextureImporterType.Sprite && m_TextureImporterSettings.spriteMode != (int)SpriteImportMode.Multiple;
                EnsureSingleSpriteExist();

                TextureGenerationOutput output;
                if (m_TextureImporterSettings.textureType != TextureImporterType.Sprite ||
                    m_MosaicLayers == false || singleSpriteMode)
                {
                    output = ImportFlattenImage(doc, ctx, singleSpriteMode);
                }
                else
                {
                    output = ImportFromLayers(ctx);
                }
                
                if (output.texture != null && output.sprites != null)
                    SetPhysicsOutline(GetDataProvider<ISpritePhysicsOutlineDataProvider>(), output.sprites, definitionScale, pixelsPerUnit, m_GeneratePhysicsShape);

                RegisterAssets(ctx, output);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import file {assetPath}. Ex:{e.Message}");
            }
            finally
            {
                fileStream.Close();
                if (doc != null)
                    doc.Dispose();
                UnityEngine.Profiling.Profiler.EndSample();
                EditorUtility.SetDirty(this);
            }

        }

        void ValidatePSDLayerId(Document doc, ELayerMappingOption layerMappingOption)
        {
            if (layerMappingOption == ELayerMappingOption.UseLayerId)
            {
                var uniqueNameGenerator = new UniqueNameGenerator();
                ImportUtilities.ValidatePSDLayerId(GetPSDLayers(), doc.Layers, uniqueNameGenerator);
            }
        }
        
        TextureGenerationOutput ImportTexture(AssetImportContext ctx, NativeArray<Color32> imageData, int textureWidth, int textureHeight, SpriteMetaData[] sprites)
        {
            if (!imageData.IsCreated || imageData.Length == 0)
                return new TextureGenerationOutput();

            TextureGenerationOutput output = new TextureGenerationOutput();
            UnityEngine.Profiling.Profiler.BeginSample("ImportTexture");
            try
            {
                var platformSettings = TextureImporterUtilities.GetPlatformTextureSettings(ctx.selectedBuildTarget, in m_PlatformSettings);

                var textureSettings = m_TextureImporterSettings.ExtractTextureSettings();
                textureSettings.assetPath = ctx.assetPath;
                textureSettings.enablePostProcessor = true;
                textureSettings.containsAlpha = true;
                textureSettings.hdr = false;

                var textureAlphaSettings = m_TextureImporterSettings.ExtractTextureAlphaSettings();
                var textureMipmapSettings = m_TextureImporterSettings.ExtractTextureMipmapSettings();
                var textureCubemapSettings = m_TextureImporterSettings.ExtractTextureCubemapSettings();
                var textureWrapSettings = m_TextureImporterSettings.ExtractTextureWrapSettings();
                
                switch (m_TextureImporterSettings.textureType)
                {
                    case TextureImporterType.Default:
                        output = TextureGeneratorHelper.GenerateTextureDefault(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureAlphaSettings, textureMipmapSettings, textureCubemapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.NormalMap:
                        var textureNormalSettings = m_TextureImporterSettings.ExtractTextureNormalSettings();
                        output = TextureGeneratorHelper.GenerateNormalMap(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureNormalSettings, textureMipmapSettings, textureCubemapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.GUI:
                        output = TextureGeneratorHelper.GenerateTextureGUI(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureAlphaSettings, textureMipmapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.Sprite:
                        var textureSpriteSettings = m_TextureImporterSettings.ExtractTextureSpriteSettings();
                        textureSpriteSettings.packingTag = m_SpritePackingTag;
                        textureSpriteSettings.qualifyForPacking = !string.IsNullOrEmpty(m_SpritePackingTag);
                        textureSpriteSettings.spriteSheetData = new SpriteImportData[sprites.Length];
                        textureSettings.npotScale = TextureImporterNPOTScale.None;
                        textureSettings.secondaryTextures = secondaryTextures;
                        
                        for (int i = 0; i < sprites.Length; ++i)
                            textureSpriteSettings.spriteSheetData[i] = sprites[i];
                        
                        output = TextureGeneratorHelper.GenerateTextureSprite(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureSpriteSettings, textureAlphaSettings, textureMipmapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.Cursor:
                        output = TextureGeneratorHelper.GenerateTextureCursor(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureAlphaSettings, textureMipmapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.Cookie:
                        output = TextureGeneratorHelper.GenerateCookie(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureAlphaSettings, textureMipmapSettings, textureCubemapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.Lightmap:
                        output = TextureGeneratorHelper.GenerateLightmap(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureMipmapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.SingleChannel:
                        output = TextureGeneratorHelper.GenerateTextureSingleChannel(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureAlphaSettings, textureMipmapSettings, textureCubemapSettings, textureWrapSettings);
                        break;
                    default:
                        Debug.LogAssertion("Unknown texture type for import");
                        output = default(TextureGenerationOutput);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Unable to generate Texture2D. Possibly texture size is too big to be generated. ex:"+e.ToString(), this);
            }
            finally
            {
                UnityEngine.Profiling.Profiler.EndSample();    
            }
            
            return output;
        }

        void SetDocumentImportData(IEnumerable<BitmapLayer> layers, PSDExtractLayerData[] extractData, IPSDLayerMappingStrategy mappingStrategy, List<PSDLayer> psdLayers, PSDExtractLayerData parent = null)
        {
            for (var i = 0; i < layers.Count(); ++i)
            {
                var layer = layers.ElementAt(i);
                PSDLayerImportSetting importSetting = null;
                if (m_PSDLayerImportSetting != null && m_PSDLayerImportSetting.Length > 0)
                {
                    importSetting = m_PSDLayerImportSetting.FirstOrDefault(x => mappingStrategy.Compare(x, layer));
                }
                var c = psdLayers?.FirstOrDefault(x => mappingStrategy.Compare(x, layer));
                if (c != null)
                {
                    if(c.spriteID.Empty())
                        c.spriteID = importSetting != null ? importSetting.spriteId : GUID.Generate();
                    if (importSetting == null)
                    {
                        importSetting = new PSDLayerImportSetting()
                        {
                            flatten = c.flatten,
                            importLayer = layer.Visible || m_ImportHiddenLayers,
                        };    
                    }

                    importSetting.spriteId = c.spriteID;
                }
                
                if (importSetting == null)
                {
                    importSetting = new PSDLayerImportSetting()
                    {
                        flatten = false,
                        importLayer = layer.Visible || m_ImportHiddenLayers,
                    };    
                }
                
                extractData[i] = new PSDExtractLayerData()
                {
                    bitmapLayer = layer,
                    importSetting = importSetting,
                };
                
                PSDExtractLayerData[] childrenExtractData = null;
                if (layer.ChildLayer != null)
                {
                    childrenExtractData = new PSDExtractLayerData[layer.ChildLayer.Count()];
                    SetDocumentImportData(layer.ChildLayer, childrenExtractData, mappingStrategy, psdLayers, extractData[i]);
                }

                extractData[i].children = childrenExtractData;
            }
        }
        
        void SetDocumentImportData(Document doc)
        {
            var oldPsdLayers = GetPSDLayers();
            var mappingStrategy = GetLayerMappingStrategy();
            m_ExtractData = new PSDExtractLayerData[doc.Layers.Count];
            SetDocumentImportData(doc.Layers, m_ExtractData, mappingStrategy, oldPsdLayers);
        }
        
        TextureGenerationOutput ImportFlattenImage(Document doc, AssetImportContext ctx, bool singleSpriteMode)
        {
            TextureGenerationOutput output;
            var outputImageBuffer = new NativeArray<Color32>(doc.width * doc.height, Allocator.Persistent);
            try
            {
                FlattenImageTask.Execute(m_ExtractData, ref outputImageBuffer, m_ImportHiddenLayers, canvasSize);
             
                var spriteImportData = GetSpriteImportData();
                if (spriteImportData.Count <= 0 || spriteImportData[0] == null)
                {
                    spriteImportData.Add(new SpriteMetaData());
                }

                spriteImportData[0].name = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath) + "_1";
                spriteImportData[0].alignment = (SpriteAlignment)m_TextureImporterSettings.spriteAlignment;
                spriteImportData[0].border = m_TextureImporterSettings.spriteBorder;
                spriteImportData[0].pivot = m_TextureImporterSettings.spritePivot;
                spriteImportData[0].rect = new Rect(0, 0, doc.width, doc.height);
                
                importData.importedTextureWidth = textureActualWidth = doc.width;
                importData.importedTextureHeight = textureActualHeight = doc.height;
                
                var spriteRects = new SpriteMetaData[0];
                if (m_TextureImporterSettings.textureType == TextureImporterType.Sprite)
                {
                    if (singleSpriteMode)
                        spriteRects = new[] { spriteImportData[0] };
                    else if (spriteImportData.Count > 1)
                        spriteRects = spriteImportData.GetRange(1, spriteDataCount).ToArray();
                }
                    
                output = ImportTexture(ctx, outputImageBuffer, doc.width, doc.height, spriteRects);
                importData.importedTextureWidth = output.texture.width;
                importData.importedTextureHeight = output.texture.height;
            }
            finally
            {
                outputImageBuffer.Dispose();
            }

            return output;
        }
        
        TextureGenerationOutput ImportFromLayers(AssetImportContext ctx)
        {
            TextureGenerationOutput output;
            var outputImageBuffer = default(NativeArray<Color32>);

            var layerIndex = new List<int>();
            var spriteNameHash = new UniqueNameGenerator();

            var platformSettings = TextureImporterUtilities.GetPlatformTextureSettings(ctx.selectedBuildTarget, in m_PlatformSettings);
            bool requireSquarePOT = (TextureImporterFormat.PVRTC_RGB2 <= platformSettings.format && platformSettings.format <= TextureImporterFormat.PVRTC_RGBA4);

            var oldPsdLayers = GetPSDLayers();
            List<PSDLayer> psdLayers = null;
            try
            {
                ExtractLayerTask.Execute(in m_ExtractData, out psdLayers, m_ImportHiddenLayers, canvasSize);
                
                var mappingStrategy = GetLayerMappingStrategy();
                var layerUnique = mappingStrategy.LayersUnique(psdLayers.ConvertAll(x => (IPSDLayerMappingStrategyComparable)x));
                if (!string.IsNullOrEmpty(layerUnique))
                {
                    Debug.LogWarning(layerUnique,this);
                }
                var removedLayersSprite = oldPsdLayers.Where(x => psdLayers.FirstOrDefault(y => mappingStrategy.Compare(y, x)) == null).Select(z => z.spriteID).ToArray();
                var hasNewLayer = false;
                for (var i = 0; i < psdLayers.Count; ++i)
                {
                    var j = 0;
                    var psdLayer = psdLayers[i];
                    for (; j < oldPsdLayers.Count; ++j)
                    {
                        if (mappingStrategy.Compare(psdLayer, oldPsdLayers[j]))
                        {
                            psdLayer.spriteName = oldPsdLayers[j].spriteName;
                            psdLayer.mosaicPosition = oldPsdLayers[j].mosaicPosition;
                            if (psdLayer.isImported != oldPsdLayers[j].isImported)
                                hasNewLayer = true;
                            break;
                        }
                    }

                    if(j >= oldPsdLayers.Count)
                        hasNewLayer = true;
                }

                var layerBuffers = new List<NativeArray<Color32>>();
                var layerWidth = new List<int>();
                var layerHeight = new List<int>();
                for (var i = 0; i < psdLayers.Count; ++i)
                {
                    var l = psdLayers[i];
                    var expectedBufferLength = l.width * l.height;
                    if (l.texture.IsCreated && l.texture.Length == expectedBufferLength && l.isImported)
                    {
                        layerBuffers.Add(l.texture);
                        layerIndex.Add(i);
                        layerWidth.Add(l.width);
                        layerHeight.Add(l.height);
                    }
                }

                ImagePacker.Pack(layerBuffers.ToArray(), layerWidth.ToArray(), layerHeight.ToArray(), m_Padding, m_SpriteSizeExpand, out outputImageBuffer, out int width, out int height, out RectInt[] spriteData, out Vector2Int[] uvTransform, requireSquarePOT);
                
                var packOffsets = new Vector2[spriteData.Length];
                for (var i = 0; i < packOffsets.Length; ++i)
                    packOffsets[i] = new Vector2((uvTransform[i].x - spriteData[i].position.x) / -1f, (uvTransform[i].y - spriteData[i].position.y) / -1f);

                var spriteImportData = GetSpriteImportData();
                if (spriteImportData.Count <= 0 || shouldResliceFromLayer || hasNewLayer)
                {
                    var newSpriteMeta = new List<SpriteMetaData>();

                    for (int i = 0; i < spriteData.Length && i < layerIndex.Count; ++i)
                    {
                        var psdLayer = psdLayers[layerIndex[i]];
                        var spriteSheet = spriteImportData.FirstOrDefault(x => x.spriteID == psdLayer.spriteID);
                        if (spriteSheet == null)
                        {
                            spriteSheet = new SpriteMetaData();
                            spriteSheet.border = Vector4.zero;
                            spriteSheet.alignment = (SpriteAlignment)m_TextureImporterSettings.spriteAlignment;
                            spriteSheet.pivot = m_TextureImporterSettings.spritePivot;
                            spriteSheet.rect = new Rect(spriteData[i].x, spriteData[i].y, spriteData[i].width, spriteData[i].height);
                            spriteSheet.spriteID = psdLayer.spriteID;
                        }
                        else
                        {
                            var r = spriteSheet.rect;
                            r.position = r.position - psdLayer.mosaicPosition + spriteData[i].position;
                            spriteSheet.rect = r;
                        }
                        
                        psdLayer.spriteName = ImportUtilities.GetUniqueSpriteName(psdLayer.name, spriteNameHash, m_KeepDupilcateSpriteName);
                        spriteSheet.name = psdLayer.spriteName;
                        spriteSheet.spritePosition = psdLayer.layerPosition + packOffsets[i];
                        
                        if(shouldResliceFromLayer)
                            spriteSheet.rect = new Rect(spriteData[i].x, spriteData[i].y, spriteData[i].width, spriteData[i].height);
                        
                        spriteSheet.uvTransform = uvTransform[i];

                        psdLayer.spriteID = spriteSheet.spriteID;
                        psdLayer.mosaicPosition = spriteData[i].position;
                        newSpriteMeta.Add(spriteSheet);
                    }
                    spriteImportData.Clear();
                    spriteImportData.AddRange(newSpriteMeta);
                }
                else
                {
                    spriteImportData.RemoveAll(x => removedLayersSprite.Contains(x.spriteID));

                    // First look for any user created SpriteRect and add those into the name hash
                    foreach (var importData in spriteImportData)
                    {
                        var psdLayer = psdLayers.FirstOrDefault(x => x.spriteID == importData.spriteID);
                        if (psdLayer == null)
                            spriteNameHash.AddHash(importData.name);
                    }

                    foreach (var importData in spriteImportData)
                    {
                        var psdLayer = psdLayers.FirstOrDefault(x => x.spriteID == importData.spriteID);
                        if (psdLayer == null)
                            importData.uvTransform = new Vector2Int((int)importData.rect.position.x, (int)importData.rect.position.y);
                        // If it is user created rect or the name has been changed before
                        // add it into the spriteNameHash and we don't copy it over from the layer
                        if (psdLayer == null || psdLayer.spriteName != importData.name)
                            spriteNameHash.AddHash(importData.name);

                        // If the sprite name has not been changed, we ensure the new
                        // layer name is still unique and use it as the sprite name
                        if (psdLayer != null && psdLayer.spriteName == importData.name)
                        {
                            psdLayer.spriteName = ImportUtilities.GetUniqueSpriteName(psdLayer.name, spriteNameHash, m_KeepDupilcateSpriteName);
                            importData.name = psdLayer.spriteName;
                        }
                    }

                    //Update names for those user has not changed and add new sprite rect based on PSD file.
                    for (var k = 0; k < layerIndex.Count; ++k)
                    {
                        var i = layerIndex[k];
                        var spriteSheet = spriteImportData.FirstOrDefault(x => x.spriteID == psdLayers[i].spriteID);
                        var inOldLayer = oldPsdLayers.FindIndex(x => mappingStrategy.Compare(x,psdLayers[i])) != -1;
                        if (spriteSheet == null && !inOldLayer)
                        {
                            spriteSheet = new SpriteMetaData();
                            spriteImportData.Add(spriteSheet);
                            spriteSheet.rect = new Rect(spriteData[k].x, spriteData[k].y, spriteData[k].width, spriteData[k].height);
                            spriteSheet.border = Vector4.zero;
                            spriteSheet.alignment = (SpriteAlignment)m_TextureImporterSettings.spriteAlignment;
                            spriteSheet.pivot = m_TextureImporterSettings.spritePivot;
                            spriteSheet.spritePosition = psdLayers[i].layerPosition;
                            psdLayers[i].spriteName = ImportUtilities.GetUniqueSpriteName(psdLayers[i].name, spriteNameHash, m_KeepDupilcateSpriteName);
                            spriteSheet.name = psdLayers[i].spriteName;
                        }
                        else if (spriteSheet != null)
                        {
                            var r = spriteSheet.rect;
                            r.position = spriteSheet.rect.position - psdLayers[i].mosaicPosition + spriteData[k].position;
                            
                            if (inOldLayer && (m_SpriteSizeExpand > 0 || m_SpriteSizeExpandChanged))
                            {
                                r.width = spriteData[k].width;
                                r.height = spriteData[k].height;
                            }
                            spriteSheet.rect = r;
                            spriteSheet.spritePosition = psdLayers[i].layerPosition + packOffsets[k];
                        }

                        if (spriteSheet != null)
                        {
                            spriteSheet.uvTransform = uvTransform[k];
                            psdLayers[i].spriteID = spriteSheet.spriteID;
                            psdLayers[i].mosaicPosition = spriteData[k].position;
                        }
                    }
                }

                foreach (var l in oldPsdLayers)
                    l.Dispose();
                oldPsdLayers.Clear();

                oldPsdLayers.AddRange(psdLayers);
                importData.importedTextureHeight = textureActualHeight = height;
                importData.importedTextureWidth = textureActualWidth = width;
                
                output = ImportTexture(ctx, outputImageBuffer, width, height, spriteImportData.ToArray());
                if (output.texture)
                {
                    importData.importedTextureHeight = output.texture.height;
                    importData.importedTextureWidth = output.texture.width;
                }
            }
            finally
            {
                if (outputImageBuffer.IsCreated)
                    outputImageBuffer.Dispose();
                foreach (var l in psdLayers)
                    l.Dispose();
            }

            return output;
        }

        void EnsureSingleSpriteExist()
        {
            if (m_SpriteImportData.Count <= 0)
                m_SpriteImportData.Add(new SpriteMetaData()); // insert default for single sprite mode
        }

        void RegisterAssets(AssetImportContext ctx, TextureGenerationOutput output)
        {
            if ((output.sprites == null || output.sprites.Length == 0) && output.texture == null)
            {
                Debug.LogWarning(TextContent.noSpriteOrTextureImportWarning, this);
                return;
            }

            var assetNameGenerator = new UniqueNameGenerator();
            if (!string.IsNullOrEmpty(output.importInspectorWarnings))
            {
                Debug.LogWarning(output.importInspectorWarnings);
            }
            if (output.importWarnings != null && output.importWarnings.Length != 0)
            {
                foreach (var warning in output.importWarnings)
                    Debug.LogWarning(warning);
            }
            if (output.thumbNail == null)
                Debug.LogWarning("Thumbnail generation fail");
            if (output.texture == null)
            {
                throw new Exception("Texture import fail");
            }

            var assetName = assetNameGenerator.GetUniqueName(System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath),  true, this);
            UnityEngine.Object mainAsset = null;

            RegisterTextureAsset(ctx, output, assetName, ref mainAsset);
            RegisterSpriteLibraryAsset(ctx, output, assetName);
            RegisterGameObjects(ctx, output, ref mainAsset);
            RegisterSprites(ctx, output, assetNameGenerator);
            RegisterSkeletonAsset(ctx, output, assetName);

            ctx.AddObjectToAsset("PSDImportData", m_ImportData);
            ctx.SetMainObject(mainAsset);
        }

        void RegisterTextureAsset(AssetImportContext ctx, TextureGenerationOutput output, string assetName, ref UnityEngine.Object mainAsset)
        {
            var registerTextureNameId = string.IsNullOrEmpty(m_TextureAssetName) ? "Texture" : m_TextureAssetName;

            output.texture.name = assetName;
            ctx.AddObjectToAsset(registerTextureNameId, output.texture, output.thumbNail);
            mainAsset = output.texture;
        }

        void RegisterSpriteLibraryAsset(AssetImportContext ctx, TextureGenerationOutput output, string assetName)
        {
            if (output.sprites == null)
                return;
            
            var slAsset = ProduceSpriteLibAsset(output.sprites);
            if (slAsset == null)
                return;
            
            slAsset.name = assetName;
            var spriteLibAssetNameId = string.IsNullOrEmpty(m_SpriteLibAssetName) ? "SpriteLibAsset" : m_SpriteLibAssetName;
            ctx.AddObjectToAsset(spriteLibAssetNameId, slAsset);
        }

        void RegisterGameObjects(AssetImportContext ctx, TextureGenerationOutput output, ref UnityEngine.Object mainAsset)
        {
            if (output.sprites == null)
                return;
            if (!shouldProduceGameObject)
                return;

            var contextObjects = new List<UnityEngine.Object>();
            ctx.GetObjects(contextObjects);
            var slAsset = contextObjects.Find(x => x.GetType() == typeof(SpriteLibraryAsset)) as SpriteLibraryAsset;
            
            var prefabRootNameId = string.IsNullOrEmpty(m_TextureAssetName) ? "root" : m_TextureAssetName;
            var registerPrefabNameId = string.IsNullOrEmpty(m_PrefabAssetName) ? "Prefab" : m_PrefabAssetName;
            
            GameObject prefab = null;
            if (m_PaperDollMode)
                prefab = OnProducePaperDollPrefab(prefabRootNameId, output.sprites, slAsset);
            else
                prefab = OnProducePrefab(prefabRootNameId, output.sprites, slAsset);
            
            if (prefab != null)
            {
                ctx.AddObjectToAsset(registerPrefabNameId, prefab);
                mainAsset = prefab;
            }
        }

        void RegisterSprites(AssetImportContext ctx, TextureGenerationOutput output, UniqueNameGenerator assetNameGenerator)
        {
            if (output.sprites == null)
                return;
            
            foreach (var s in output.sprites)
            {
                var spriteAssetName = assetNameGenerator.GetUniqueName(s.GetSpriteID().ToString(),  false, s);
                ctx.AddObjectToAsset(spriteAssetName, s);
            }            
        }

        void RegisterSkeletonAsset(AssetImportContext ctx, TextureGenerationOutput output, string assetName)
        {
            var skeletonAssetNameId = string.IsNullOrEmpty(m_SkeletonAssetName) ? "SkeletonAsset" : m_SkeletonAssetName;

            if (output.sprites != null)
            {
                if (inCharacterMode && skeletonAsset == null)
                {
                    var characterRig = ScriptableObject.CreateInstance<SkeletonAsset>();
                    characterRig.name = assetName + " Skeleton";
                    var bones = GetDataProvider<ICharacterDataProvider>().GetCharacterData().bones;
                    characterRig.SetSpriteBones(bones);
                    ctx.AddObjectToAsset(skeletonAssetNameId, characterRig);
                }
            }
            
            if (!string.IsNullOrEmpty(m_SkeletonAssetReferenceID))
            {
                var primaryAssetPath = AssetDatabase.GUIDToAssetPath(m_SkeletonAssetReferenceID);
                if (!string.IsNullOrEmpty(primaryAssetPath) && primaryAssetPath != assetPath)
                {
                    ctx.DependsOnArtifact(primaryAssetPath);
                }
            }            
        }

        void BuildGroupGameObject(List<PSDLayer> psdGroup, int index, Transform root)
        {
            var psdData = psdGroup[index];
            if (psdData.gameObject == null)
            {
                var spriteImported = !psdGroup[index].spriteID.Empty() && psdGroup[index].isImported;
                var isVisibleGroup = psdData.isGroup && (ImportUtilities.VisibleInHierarchy(psdGroup, index) || m_ImportHiddenLayers) && m_GenerateGOHierarchy;
                if (spriteImported || isVisibleGroup)
                {
                    var spriteData = GetSpriteImportData().FirstOrDefault(x => x.spriteID == psdData.spriteID);
                    // Determine if need to create GameObject i.e. if the sprite is not in a SpriteLib or if it is the first one
                    var b = ImportUtilities.SpriteIsMainFromSpriteLib(m_SpriteCategoryList.categories, psdData.spriteID.ToString(), out var categoryName);
                    var goName = string.IsNullOrEmpty(categoryName) ? spriteData  != null ? spriteData.name : psdData.name : categoryName;
                    if (b)
                        psdData.gameObject = m_GameObjectFactory.CreateGameObject(goName);
                }
                if (psdData.parentIndex >= 0 && m_GenerateGOHierarchy && psdData.gameObject != null)
                {
                    BuildGroupGameObject(psdGroup, psdData.parentIndex, root);
                    root = psdGroup[psdData.parentIndex].gameObject.transform;
                }

                if (psdData.gameObject != null)
                {
                    psdData.gameObject.transform.SetParent(root);
                    psdData.gameObject.transform.SetSiblingIndex(root.childCount-1);
                }
            }
        }

        void CreateBoneGO(int index, SpriteBone[] bones, BoneGO[] bonesGO, Transform defaultRoot)
        {
            if (bonesGO[index].go != null)
                return;
            var bone = bones[index];
            if (bone.parentId != -1 && bonesGO[bone.parentId].go == null)
                CreateBoneGO(bone.parentId, bones, bonesGO, defaultRoot);

            var go = m_GameObjectFactory.CreateGameObject(bone.name);
            if (bone.parentId == -1)
                go.transform.SetParent(defaultRoot);
            else
                go.transform.SetParent(bonesGO[bone.parentId].go.transform);
            go.transform.localPosition = bone.position * 1 / pixelsPerUnit;
            go.transform.localRotation = bone.rotation;
            bonesGO[index] = new BoneGO()
            {
                go = go,
                index = index
            };            
        }

        BoneGO[] CreateBonesGO(Transform root)
        {
            if (inCharacterMode)
            {
                var characterSkeleton = GetDataProvider<ICharacterDataProvider>().GetCharacterData();
                var bones = characterSkeleton.bones;
                if (bones != null)
                {
                    var boneGOs = new BoneGO[bones.Length];
                    for (int i = 0; i < bones.Length; ++i)
                    {
                        CreateBoneGO(i, bones, boneGOs, root);
                    }
                    return boneGOs;
                }
            }
            return new BoneGO[0];
        }

        void GetSpriteLibLabel(string spriteId, out string category, out string label)
        {
            category = "";
            label = "";
            foreach (var cat in m_SpriteCategoryList.categories)
            {
                var index = cat.labels.FindIndex(x => x.spriteId == spriteId);
                if (index != -1)
                {
                    category = cat.name;
                    label = cat.labels[index].name;
                    break;
                }
            }
        }

        GameObject OnProducePaperDollPrefab(string assetName, Sprite[] sprites, SpriteLibraryAsset spriteLib)
        {
            GameObject root = null;
            if (sprites != null && sprites.Length > 0)
            {
                root = new GameObject();
                root.name = assetName + "_GO";
                var spriteImportData = GetSpriteImportData();
                var psdLayers = GetPSDLayers();
                var boneGOs = CreateBonesGO(root.transform);
                if (spriteLib != null)
                    root.AddComponent<SpriteLibrary>().spriteLibraryAsset = spriteLib;
                var currentCharacterData = characterData;
                for (var i = 0; i < sprites.Length; ++i)
                {
                    if (ImportUtilities.SpriteIsMainFromSpriteLib(m_SpriteCategoryList.categories, sprites[i].GetSpriteID().ToString(), out var categoryName))
                    {
                        var spriteBones = currentCharacterData.parts.FirstOrDefault(x => new GUID(x.spriteId) == sprites[i].GetSpriteID()).bones;
                        var rootBone = root;
                        if (spriteBones != null && spriteBones.Any())
                        {
                            var b = spriteBones.Where(x => x >= 0 && x < boneGOs.Length).Select(x => boneGOs[x]).OrderBy(x => x.index);
                            if (b.Any())
                                rootBone = b.First().go;
                        }

                        var srGameObject = m_GameObjectFactory.CreateGameObject(string.IsNullOrEmpty(categoryName) ? sprites[i].name : categoryName);
                        var sr = srGameObject.AddComponent<SpriteRenderer>();
                        sr.sprite = sprites[i];
                        sr.sortingOrder = psdLayers.Count - psdLayers.FindIndex(x => x.spriteID == sprites[i].GetSpriteID());
                        srGameObject.transform.parent = rootBone.transform;
                        var spriteMetaData = spriteImportData.FirstOrDefault(x => x.spriteID == sprites[i].GetSpriteID());
                        if (spriteMetaData != null)
                        {
                            var uvTransform = spriteMetaData.uvTransform;
                            var outlineOffset = new Vector2(spriteMetaData.rect.x - uvTransform.x + (spriteMetaData.pivot.x * spriteMetaData.rect.width),
                                spriteMetaData.rect.y - uvTransform.y + (spriteMetaData.pivot.y * spriteMetaData.rect.height)) * definitionScale / sprites[i].pixelsPerUnit;
                            srGameObject.transform.position = new Vector3(outlineOffset.x, outlineOffset.y, 0);
                        }

                        GetSpriteLibLabel(sprites[i].GetSpriteID().ToString(), out var category, out var labelName);
                        if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(labelName))
                        {
                            var resolver = srGameObject.AddComponent<SpriteResolver>();
                            resolver.SetCategoryAndLabel(category, labelName);
                            resolver.ResolveSpriteToSpriteRenderer();
                        }
                    }
                }
            }
            return root;
        }

        internal void SetPlatformTextureSettings(TextureImporterPlatformSettings platformSettings)
        {
            var index = m_PlatformSettings.FindIndex(x => x.name == platformSettings.name);
            if(index < 0)
                m_PlatformSettings.Add(platformSettings);
            else
                m_PlatformSettings[index] = platformSettings;
        }

        internal TextureImporterPlatformSettings[] GetAllPlatformSettings()
        {
            return m_PlatformSettings.ToArray();
        }

        GameObject OnProducePrefab(string assetname, Sprite[] sprites, SpriteLibraryAsset spriteLib)
        {
            GameObject root = null;
            CharacterData? characterSkeleton = inCharacterMode ? new CharacterData ? (GetDataProvider<ICharacterDataProvider>().GetCharacterData()) : null;
            if (sprites != null && sprites.Length > 0)
            {
                var currentCharacterData = characterData;
                var spriteImportData = GetSpriteImportData();
                root = new GameObject();
                root.transform.SetSiblingIndex(0);
                root.name = assetname + "_GO";
                if (spriteLib != null)
                    root.AddComponent<SpriteLibrary>().spriteLibraryAsset = spriteLib;

                var psdLayers = GetPSDLayers();
                for (var i = 0; i < psdLayers.Count; ++i)
                {
                    BuildGroupGameObject(psdLayers, i, root.transform);
                }
                var boneGOs = CreateBonesGO(root.transform);
                for (var i = 0; i < psdLayers.Count; ++i)
                {
                    var l = psdLayers[i];
                    var layerSpriteID = l.spriteID;
                    var sprite = sprites.FirstOrDefault(x => x.GetSpriteID() == layerSpriteID);
                    var spriteMetaData = spriteImportData.FirstOrDefault(x => x.spriteID == layerSpriteID);
                    if (sprite != null && spriteMetaData != null && l.gameObject != null)
                    {
                        var spriteRenderer = l.gameObject.AddComponent<SpriteRenderer>();
                        spriteRenderer.sprite = sprite;
                        spriteRenderer.sortingOrder = psdLayers.Count - i;
                        
                        var pivot = spriteMetaData.pivot;
                        pivot.x *= spriteMetaData.rect.width;
                        pivot.y *= spriteMetaData.rect.height;

                        var spritePosition = spriteMetaData.spritePosition;
                        spritePosition.x += pivot.x;
                        spritePosition.y += pivot.y;
                        spritePosition *= (definitionScale / sprite.pixelsPerUnit);
                        
                        l.gameObject.transform.position = new Vector3(spritePosition.x, spritePosition.y, 0f);

                        if (characterSkeleton != null)
                        {
                            var part = characterSkeleton.Value.parts.FirstOrDefault(x => x.spriteId == spriteMetaData.spriteID.ToString());
                            if (part.bones != null && part.bones.Length > 0)
                            {
                                var spriteSkin = l.gameObject.AddComponent<SpriteSkin>();
                                if (spriteRenderer.sprite != null && spriteRenderer.sprite.GetBindPoses().Length > 0)
                                {
                                    var spriteBones = currentCharacterData.parts.FirstOrDefault(x => new GUID(x.spriteId) == spriteRenderer.sprite.GetSpriteID()).bones.Where(x => x >= 0 && x < boneGOs.Length).Select(x => boneGOs[x]);
                                    if (spriteBones.Any())
                                    {
                                        spriteSkin.rootBone = root.transform;
                                        spriteSkin.boneTransforms = spriteBones.Select(x => x.go.transform).ToArray();
                                        if (spriteSkin.isValid)
                                            spriteSkin.CalculateBounds();
                                    }
                                }
                            }
                        }

                        GetSpriteLibLabel(layerSpriteID.ToString(), out var category, out var labelName);
                        if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(labelName))
                        {
                            var resolver = l.gameObject.AddComponent<SpriteResolver>();
                            resolver.SetCategoryAndLabel(category, labelName);
                            resolver.ResolveSpriteToSpriteRenderer();
                        }
                    }
                }

                var prefabBounds = new Rect(0 , 0, importData.documentSize.x / pixelsPerUnit, importData.documentSize.y / pixelsPerUnit);
                var documentPivot = (Vector3)ImportUtilities.GetPivotPoint(prefabBounds, m_DocumentAlignment, m_DocumentPivot);
                for (var i = 0; i < psdLayers.Count; ++i)
                {
                    var l = psdLayers[i];
                    if (l.gameObject == null || l.gameObject.GetComponent<SpriteRenderer>() == null)
                        continue;
                    var p = l.gameObject.transform.localPosition;
                    p -= documentPivot;
                    l.gameObject.transform.localPosition = p;
                }
                for (int i = 0; i < boneGOs.Length; ++i)
                {
                    if (boneGOs[i].go.transform.parent != root.transform)
                        continue;
                    var p = boneGOs[i].go.transform.position;
                    p -= documentPivot;
                    boneGOs[i].go.transform.position = p;
                }
            }

            return root;
        }

        int spriteDataCount
        {
            get
            {
                var spriteImportData = GetSpriteImportData();
                if (inMosaicMode)
                    return spriteImportData.Count;
                if (spriteImportModeToUse != SpriteImportMode.Multiple)
                    return 1;
                return spriteImportData.Count - 1;
            }
        }

        internal void Apply()
        {
            // Do this so that asset change save dialog will not show
            var originalValue = EditorPrefs.GetBool("VerifySavingAssets", false);
            EditorPrefs.SetBool("VerifySavingAssets", false);
            AssetDatabase.ForceReserializeAssets(new string[] { assetPath }, ForceReserializeAssetsOptions.ReserializeMetadata);
            EditorPrefs.SetBool("VerifySavingAssets", originalValue);
        }

        List<SpriteMetaData> GetSpriteImportData()
        {
            if (inMosaicMode)
            {
                if (inCharacterMode)
                {
                    if (skeletonAsset != null)
                    {
                        return m_SharedRigSpriteImportData;
                    }
                    return m_RigSpriteImportData;
                }
                return m_MosaicSpriteImportData;
            }
            return m_SpriteImportData;
        }

        SkeletonAsset skeletonAsset =>
            AssetDatabase.LoadAssetAtPath<SkeletonAsset>(AssetDatabase.GUIDToAssetPath(m_SkeletonAssetReferenceID));

        internal List<PSDLayer> GetPSDLayers()
        {
            if (inMosaicMode)
            {
                if (inCharacterMode)
                {
                    if (skeletonAsset != null)
                        return m_SharedRigPSDLayers;
                    else
                        return m_RigPSDLayers;
                }
                return m_MosaicPSDLayers;
            }
            return null;
        }

        internal SpriteMetaData[] GetSpriteMetaData()
        {
            var spriteImportData = GetSpriteImportData();
            var skip = inMosaicMode ? 0 : 1;
            return spriteImportModeToUse == SpriteImportMode.Multiple ? spriteImportData.Skip(skip).ToArray() : new[] { new SpriteMetaData(spriteImportData[0]) };
        }

        internal SpriteRect GetSpriteDataFromAllMode(GUID guid)
        {
            var spriteMetaData = m_RigSpriteImportData.FirstOrDefault(x => x.spriteID == guid);
            if (spriteMetaData == null)
                spriteMetaData = m_SharedRigSpriteImportData.FirstOrDefault(x => x.spriteID == guid);
            if (spriteMetaData == null)
                spriteMetaData = m_MosaicSpriteImportData.FirstOrDefault(x => x.spriteID == guid);
            if (spriteMetaData == null)
                spriteMetaData = m_SpriteImportData.FirstOrDefault(x => x.spriteID == guid);
            return spriteMetaData;
        }
        
        internal SpriteRect GetSpriteData(GUID guid)
        {
            var spriteImportData = GetSpriteImportData();
            var skip = inMosaicMode ? 0 : 1;
            return spriteImportModeToUse == SpriteImportMode.Multiple ? spriteImportData.Skip(skip).FirstOrDefault(x => x.spriteID == guid) : spriteImportData[0];
        }

        internal Vector2 GetDocumentPivot()
        {
            return ImportUtilities.GetPivotPoint(new Rect(0, 0, 1, 1), m_DocumentAlignment, m_DocumentPivot);
        }

        internal void SetDocumentPivot(Vector2 pivot)
        {
            ImportUtilities.TranslatePivotPoint(pivot, new Rect(0, 0, 1, 1), out m_DocumentAlignment, out m_DocumentPivot);
        }

        bool inMosaicMode => spriteImportModeToUse == SpriteImportMode.Multiple && m_MosaicLayers;

        SpriteImportMode spriteImportModeToUse =>
            m_TextureImporterSettings.textureType != TextureImporterType.Sprite ?
                SpriteImportMode.None :
                (SpriteImportMode)m_TextureImporterSettings.spriteMode;

        internal CharacterData characterData
        {
            get
            {
                if (skeletonAsset != null)
                    return m_SharedRigCharacterData;
                return m_CharacterData;
            }
            set
            {
                if (skeletonAsset != null)
                    m_SharedRigCharacterData = value;
                else
                    m_CharacterData = value;
            }
        }

        internal Vector2Int canvasSize => importData.documentSize;

        SpriteLibraryAsset ProduceSpriteLibAsset(Sprite[] sprites)
        {
            if (!inCharacterMode || m_SpriteCategoryList.categories == null)
                return null;
            var categories = m_SpriteCategoryList.categories.Select(x =>
                new SpriteLibCategory()
                {
                    name = x.name,
                    categoryList = x.labels.Select(y =>
                    {
                        var sprite = sprites.FirstOrDefault(z => z.GetSpriteID().ToString() == y.spriteId);
                        return new SpriteCategoryEntry()
                        {
                            name = y.name,
                            sprite = sprite
                        };
                    }).ToList()
                }).ToList();
            categories.RemoveAll(x => x.categoryList.Count == 0);
            if (categories.Count > 0)
            {
                // Always set version to 0 since we will never be updating this
                return SpriteLibraryAsset.CreateAsset(categories, "Sprite Lib", 0);
            }
            return null;
        }

        internal void ReadTextureSettings(TextureImporterSettings dest)
        {
            m_TextureImporterSettings.CopyTo(dest);
        }
        
        internal IPSDLayerMappingStrategy GetLayerMappingStrategy()
        {
            return m_MappingCompare[(int)m_LayerMappingOption];
        }

        static void SetPhysicsOutline(ISpritePhysicsOutlineDataProvider physicsOutlineDataProvider, Sprite[] sprites, float definitionScale, float pixelsPerUnit, bool generatePhysicsShape)
        {
            foreach (var sprite in sprites)
            {
                var guid = sprite.GetSpriteID();
                var outline = physicsOutlineDataProvider.GetOutlines(guid);

                var outlineOffset = sprite.rect.size / 2;
                var generated = false;
                if ((outline == null || outline.Count == 0) && generatePhysicsShape)
                {
                    InternalEditorBridge.GenerateOutlineFromSprite(sprite, 0.25f, 200, true, out var defaultOutline);
                    outline = new List<Vector2[]>(defaultOutline.Length);
                    for (var i = 0; i < defaultOutline.Length; ++i)
                    {
                        outline.Add(defaultOutline[i]);
                    }

                    generated = true;
                }
                if (outline != null && outline.Count > 0)
                {
                    // Ensure that outlines are all valid.
                    var validOutlineCount = 0;
                    for (var i = 0; i < outline.Count; ++i)
                        validOutlineCount += ( (outline[i].Length > 2) ? 1 : 0 );

                    var index = 0;
                    var convertedOutline = new Vector2[validOutlineCount][];
                    var useScale = generated ? pixelsPerUnit * definitionScale : definitionScale;

                    for (var i = 0; i < outline.Count; ++i)
                    {
                        if (outline[i].Length > 2)
                        {
                            convertedOutline[index] = new Vector2[outline[i].Length];
                            for (var j = 0; j < outline[i].Length; ++j)
                            {
                                convertedOutline[index][j] = outline[i][j] * useScale + outlineOffset;
                            }
                            index++;
                        }
                    }
                    sprite.OverridePhysicsShape(convertedOutline);
                }
            }
        }
    }
}
