#if UNITY_6000_1_OR_NEWER
#define ENABLE_2D_TILEMAP_EDITOR
#endif

using System;
using System.Collections.Generic;
using System.IO;
using PDNWrapper;
using UnityEngine;
using Unity.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor.AssetImporters;
using UnityEditor.U2D.Common;
using UnityEditor.U2D.Sprites;
using UnityEngine.U2D;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

#if ENABLE_2D_TILEMAP_EDITOR
using UnityEditor.Tilemaps;
using UnityEngine.Tilemaps;
#endif

#if ENABLE_2D_ANIMATION
using UnityEditor.U2D.Animation;
using UnityEngine.U2D.Animation;
#endif

namespace UnityEditor.U2D.PSD
{
    /// <summary>
    /// ScriptedImporter to import Photoshop files
    /// </summary>
    // Version using unity release + 5 digit padding for future upgrade. Eg 2021.2 -> 21200000, 6000.5 -> 60500000
    [ScriptedImporter(60500000, new string[] { "psb" }, new[] { "psd" }, AllowCaching = true)]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.2d.psdimporter@17.0")]
    [MovedFrom("UnityEditor.Experimental.AssetImporters")]
    public partial class PSDImporter : ScriptedImporter, ISpriteEditorDataProvider, ISerializationCallbackReceiver
    {
        internal enum ELayerMappingOption
        {
            UseLayerName,
            UseLayerNameCaseSensitive,
            UseLayerId,
            Unknown = 0xff
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
            streamingMipmaps = false,
            streamingMipmapsPriority = 0,
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

        [SerializeField] List<SpriteMetaData> m_SingleSpriteImportData = new List<SpriteMetaData>(1) { new SpriteMetaData() };
        [SerializeField] List<SpriteMetaData> m_MultiSpriteImportData = new List<SpriteMetaData>();
        [SerializeField] List<SpriteMetaData> m_LayeredSpriteImportData = new List<SpriteMetaData>();

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        // The field initializer does not survive deserialization, so a .meta holding an empty list
        // brings the single entry back as absent. Everything below indexes [0] unconditionally.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_SingleSpriteImportData == null || m_SingleSpriteImportData.Count < 1)
                m_SingleSpriteImportData = new List<SpriteMetaData>(1) { new SpriteMetaData() };
            else if (m_SingleSpriteImportData[0] == null)
                m_SingleSpriteImportData[0] = new SpriteMetaData();
        }

#if ENABLE_2D_ANIMATION
        // CharacterData for shared rig mode
        [SerializeField] CharacterData m_SharedRigCharacterData = new CharacterData();
        // CharacterData for Rig mode
        [SerializeField] CharacterData m_CharacterData = new CharacterData();
#endif

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
        bool m_GeneratePhysicsShape = false;

        [SerializeField]
        bool m_PaperDollMode = false;

        // .meta files store the key without the m_ prefix; Presets store the property path with it.
        [SerializeField]
        [FormerlySerializedAs("keepDupilcateSpriteName")]
        [FormerlySerializedAs("m_KeepDupilcateSpriteName")]
        bool m_KeepDuplicateSpriteName = true;

#if ENABLE_2D_TILEMAP_EDITOR
        [SerializeField]
        bool m_GenerateTileAssets = false;

        [SerializeField]
        GridLayout.CellLayout m_TilePaletteCellLayout = GridLayout.CellLayout.Rectangle;

        private static readonly GridLayout.CellSwizzle[] s_TilePaletteexagonSwizzleTypeValue =
        {
            GridLayout.CellSwizzle.XYZ,
            GridLayout.CellSwizzle.YXZ,
        };

        [SerializeField]
        int m_TilePaletteHexagonLayout;

        [SerializeField]
        Vector3 m_TilePaletteCellSize = new Vector3(1, 1, 0);

        [SerializeField]
        GridPalette.CellSizing m_TilePaletteCellSizing = GridPalette.CellSizing.Automatic;

        [SerializeField]
        TransparencySortMode m_TransparencySortMode = TransparencySortMode.Default;

        [SerializeField]
        Vector3 m_TransparencySortAxis = new Vector3(0f, 0f, 1f);

        [SerializeField]
        TileTemplate m_TileTemplate = null;
#endif

        [SerializeField]
        int m_Padding = 4;

        [SerializeField]
        ushort m_SpriteSizeExpand = 0;

        // Only read when com.unity.2d.animation is installed (ENABLE_2D_ANIMATION) and by PSDImporterEditor through SerializedProperty.
        // The field stays outside the #if so the skeleton reference in .meta files survives while 2D Animation is not installed.
        [SerializeField]
#pragma warning disable CS0414
        string m_SkeletonAssetReferenceID = null;
#pragma warning restore CS0414

        [SerializeField]
        ScriptableObject m_Pipeline;
        [SerializeField]
        string m_PipelineVersion;

#if ENABLE_2D_ANIMATION
        [SerializeField]
        SpriteCategoryList m_SpriteCategoryList = new SpriteCategoryList() { categories = new List<SpriteCategory>() };
#endif

        GameObjectCreationFactory m_GameObjectFactory = new GameObjectCreationFactory(null);

        PSDImportData m_ImportData;

        internal PSDImportData importData
        {
            get
            {
                PSDImportData returnValue = m_ImportData;
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
            private set => importData.textureActualWidth = value;
        }

        internal int textureActualHeight
        {
            get => importData.textureActualHeight;
            private set => importData.textureActualHeight = value;
        }

        [SerializeField]
        string m_SpritePackingTag = "";

        [SerializeField]
        PSDLayerImportSetting[] m_PSDLayerImportSetting;

        [SerializeField]
        List<PSDLayer> m_PsdLayers = new List<PSDLayer>();

        // Use for inspector to check if the file node is checked
        [SerializeField]
#pragma warning disable 169, 414
        bool m_ImportFileNodeState = true;

        // Used by platform settings to mark it dirty so that it will trigger a reimport
        [SerializeField]
#pragma warning disable 169, 414
        long m_PlatformSettingsDirtyTick;

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
        bool inCharacterMode => inMosaicMode && m_CharacterMode;

        float definitionScale
        {
            get
            {
                float definitionScaleW = importData.importedTextureWidth / (float)textureActualWidth;
                float definitionScaleH = importData.importedTextureHeight / (float)textureActualHeight;
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
#if ENABLE_2D_ANIMATION
                SkeletonAsset skeleton = skeletonAsset;
                return skeleton != null ? skeleton.GetSpriteBones() : null;
#else
                return null;
#endif
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

        static void PackImage(NativeArray<Color32>[] buffers, int[] width, int[] height, int padding, uint spriteSizeExpand, Action<LogType, string> logger, ScriptableObject pipeline,
            out NativeArray<Color32> outPackedBuffer, out int outPackedBufferWidth, out int outPackedBufferHeight, out RectInt[] outPackedRect, out Vector2Int[] outUVTransform, bool requireSquarePOT = false)
        {
            try
            {
                if (pipeline == null)
                    pipeline = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.2d.psdimporter/Editor/Pipeline.asset");

                object[] args = new object[] { buffers, width, height, padding, spriteSizeExpand, null, 0, 0, null, null, requireSquarePOT };
                pipeline.GetType().InvokeMember("PackImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod | BindingFlags.Static, null,
                    pipeline, args);
                outPackedBuffer = (NativeArray<Color32>)args[5];
                outPackedBufferWidth = (int)args[6];
                outPackedBufferHeight = (int)args[7];
                outPackedRect = (RectInt[])args[8];
                outUVTransform = (Vector2Int[])args[9];
            }
            catch (Exception e)
            {
                logger(LogType.Error, "Unable to pack image. ex:" + e.ToString());
                ImagePacker.Pack(buffers, width, height, padding, spriteSizeExpand, out outPackedBuffer, out outPackedBufferWidth, out outPackedBufferHeight, out outPackedRect, out outUVTransform, requireSquarePOT);
            }
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

            Document doc = null;

            m_ImportData = ScriptableObject.CreateInstance<PSDImportData>();
            m_ImportData.hideFlags = HideFlags.HideInHierarchy;

            PSDImportOutput output = null;
            try
            {
                UnityEngine.Profiling.Profiler.BeginSample("OnImportAsset");

                UnityEngine.Profiling.Profiler.BeginSample("PsdLoad");
                doc = LoadDocument(ctx.assetPath);
                UnityEngine.Profiling.Profiler.EndSample();

                ValidatePSDLayerId(doc, m_LayerMappingOption);
                m_ImportData.CreatePSDLayerData(doc.Layers);
                m_ImportData.MapLayerToPreviousImport(m_LayerMappingOption, GetPSDLayers());

                PSDImporterImportProcess.BuildLayerToExtract(doc.Layers, this, out m_ExtractData);

                importData.documentSize = new Vector2Int(doc.width, doc.height);

                bool singleSpriteMode = m_TextureImporterSettings.textureType == TextureImporterType.Sprite && m_TextureImporterSettings.spriteMode != (int)SpriteImportMode.Multiple;
                if (m_TextureImporterSettings.textureType != TextureImporterType.Sprite ||
                    m_MosaicLayers == false || singleSpriteMode)
                {
                    importData.textureActualWidth = doc.width;
                    importData.textureActualHeight = doc.height;
                    output = ImportFlattenImage(doc, ctx, this);
                }
                else
                {
                    output = ImportFromLayers(ctx, this);
                    importData.textureActualWidth = output.textureGenerationOutput.actualTextureWidth;
                    importData.textureActualHeight = output.textureGenerationOutput.actualTextureHeight;
                }

                {
                    // TODO this will be remove in V2
                    List<PSDLayer> oldPsdLayers = GetPSDLayers();
                    foreach (PSDLayer l in oldPsdLayers)
                        l.Dispose();
                    oldPsdLayers.Clear();

                    oldPsdLayers.AddRange(output.psdLayers);
                    List<SpriteMetaData> spriteRects = GetSpriteImportData();
                    spriteRects.Clear();
                    spriteRects.AddRange(output.spriteRects);
                }


                Texture2D texture = output.textureGenerationOutput.GetTexture(ctx, this);
                Sprite[] sprites = output.textureGenerationOutput.GetSprites(ctx, this);
                Texture2D thumbNail = output.textureGenerationOutput.GetThumbnail(ctx, this);

                importData.importedTextureWidth = texture?.width ?? importData.textureActualWidth;
                importData.importedTextureHeight = texture?.height ?? importData.textureActualHeight;
                importData.spriteRects = output.spriteRects;

                if (texture != null && sprites != null)
                    SetPhysicsOutline(GetDataProvider<ISpritePhysicsOutlineDataProvider>(), sprites, definitionScale, pixelsPerUnit, m_GeneratePhysicsShape);

                ProducedAsset producedAsset = new ProducedAsset();
                producedAsset.texture = texture;
                producedAsset.thumbNail = thumbNail;
                producedAsset.sprites = sprites;
                producedAsset.textureAssetName = string.IsNullOrEmpty(m_TextureAssetName) ? "Texture" : m_TextureAssetName;
                producedAsset.spriteLibAssetName = string.IsNullOrEmpty(m_SpriteLibAssetName) ? "SpriteLibAsset" : m_SpriteLibAssetName;
                producedAsset.importData = importData;

                string assetName = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);

#if ENABLE_2D_ANIMATION
                {
                    producedAsset.skeletonAssetName = string.IsNullOrEmpty(m_SkeletonAssetName) ? "SkeletonAsset" : m_SkeletonAssetName;

                    if (sprites != null)
                    {
                        producedAsset.spriteLibraryAsset = ProduceSpriteLibAsset(sprites);
                        if (producedAsset.spriteLibraryAsset != null)
                            producedAsset.spriteLibraryAsset.name = assetName;
                        if (inCharacterMode && skeletonAsset == null)
                        {

                            SkeletonAsset characterRig = ScriptableObject.CreateInstance<SkeletonAsset>();
                            characterRig.name = assetName + " Skeleton";
                            SpriteBone[] bones = GetDataProvider<ICharacterDataProvider>().GetCharacterData().bones;
                            characterRig.SetSpriteBones(bones);
                            producedAsset.skeletonAsset = characterRig;
                        }

                        producedAsset.skeletonAssetReferenceID = m_SkeletonAssetReferenceID;
                    }
                }
#endif

                // do this after spritelib since the prefab uses it
                producedAsset.prefabAssetName = string.IsNullOrEmpty(m_PrefabAssetName) ? "Prefab" : m_PrefabAssetName;
                if (sprites != null)
                {
                    string prefabRootNameId = string.IsNullOrEmpty(m_TextureAssetName) ? "root" : m_TextureAssetName;
                    producedAsset.prefabRoot = ((IImportConfigProvider)this).producePrefabMode switch
                    {
                        ProducePrefabMode.None => null,
                        ProducePrefabMode.Prefab => OnProducePrefab(prefabRootNameId, producedAsset, output.psdLayers, (IImportConfigProvider)this, m_GameObjectFactory, m_DocumentAlignment, m_DocumentPivot),
                        ProducePrefabMode.Paperdoll => OnProducePaperDollPrefab(prefabRootNameId, producedAsset, output.psdLayers, (IImportConfigProvider)this, m_GameObjectFactory),
                        _ => null
                    };
                }

#if ENABLE_2D_TILEMAP_EDITOR
                {
                    if (texture != null && sprites != null && m_GenerateTileAssets)
                    {
                        List<Sprite> tileSprite = new List<Sprite>();
                        foreach (Sprite s in sprites)
                        {
                            if (s.rect.width > 4 && s.rect.height > 4)
                                tileSprite.Add(s);
                        }
                        GameObject paletteGO = GridPaletteUtility.CreateNewPaletteAsSubAsset(assetName
                            , m_TilePaletteCellLayout
                            , m_TilePaletteCellSizing
                            , m_TilePaletteCellSize
                            , s_TilePaletteexagonSwizzleTypeValue[m_TilePaletteHexagonLayout]
                            , m_TransparencySortMode
                            , m_TransparencySortAxis
                            , new[] { texture }
                            , new[] { tileSprite }
                            , new[] { m_TileTemplate }
                            , out GridPalette palette
                            , out List<TileBase> tiles);
                        producedAsset.tilePaletteGO = paletteGO;
                        producedAsset.tiles = tiles;
                        producedAsset.gridPalette = palette;
                    }
                }
#endif

                if (!string.IsNullOrEmpty(output.textureGenerationOutput.GetImportInspectorWarnings(ctx, this)))
                {
                    Debug.LogWarning(output.textureGenerationOutput.GetImportInspectorWarnings(ctx, this));
                }
                string[] importWarnings = output.textureGenerationOutput.GetImportWarnings(ctx, this);
                if (importWarnings != null && importWarnings.Length != 0)
                {
                    foreach (string warning in importWarnings)
                        Debug.LogWarning(warning);
                }

                RegisterAssets(ctx, producedAsset, Logger);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import file {assetPath}. Error: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                output?.Dispose();
                if (doc != null)
                    doc.Dispose();
                UnityEngine.Profiling.Profiler.EndSample();
                EditorUtility.SetDirty(this);
            }

        }

        internal static Document LoadDocument(string assetPath)
        {
            using (FileStream fileStream = new FileStream(assetPath, FileMode.Open, FileAccess.Read))
                return PaintDotNet.Data.PhotoshopFileType.PsdLoad.Load(fileStream);
        }

        void ValidatePSDLayerId(Document doc, ELayerMappingOption layerMappingOption)
        {
            if (layerMappingOption == ELayerMappingOption.UseLayerId)
            {
                UniqueNameGenerator uniqueNameGenerator = new UniqueNameGenerator();
                ImportUtilities.ValidatePSDLayerId(GetPSDLayers(), doc.Layers, uniqueNameGenerator, this);
            }
        }

        // Import adjusts the Sprite data it is given, so it works on copies and leaves the importer's data untouched.
        static List<SpriteMetaData> CopySpriteMetaData(IReadOnlyList<SpriteMetaData> spriteMetaData)
        {
            List<SpriteMetaData> copies = new List<SpriteMetaData>(spriteMetaData.Count);
            foreach (SpriteMetaData sr in spriteMetaData)
                copies.Add(sr.Clone());
            return copies;
        }

        static PSDImportOutput ImportFlattenImage(Document doc, AssetImportContext ctx, IImportConfigProvider importParameter)
        {
            ImportTextureWrapper output = null;
            NativeArray<Color32> outputImageBuffer = new NativeArray<Color32>(doc.width * doc.height, Allocator.Persistent);
            List<PSDLayer> psdLayers = new();
            List<SpriteMetaData> newSpriteMeta = new List<SpriteMetaData>();
            try
            {
                FlattenImageTask.Execute(importParameter.extractedLayerData, ref outputImageBuffer, importParameter.importHiddenLayers, importParameter.canvasSize);

                psdLayers.AddRange(importParameter.obsoletePsdLayers);
                newSpriteMeta.AddRange(CopySpriteMetaData(importParameter.spriteMetaData));
                output = new ImportTextureWrapper(doc.width, doc.height, outputImageBuffer, newSpriteMeta);
            }
            catch (Exception ex)
            {
                importParameter.logger(LogType.Error, "Unable to import from layers. ex:" + ex.ToString());
            }
            finally
            {
                if (output == null && outputImageBuffer.IsCreated)
                {
                    outputImageBuffer.Dispose();
                    output = new ImportTextureWrapper(0, 0, default(NativeArray<Color32>), new List<SpriteMetaData>());
                }

            }

            return new PSDImportOutput(output, psdLayers, newSpriteMeta);
        }

        // The stored rect of an unedited Sprite sits at the mosaic position of its layer, and its name is the one generated from the layer.
        static bool IsSpriteEditedFromLayer(SpriteMetaData storedSprite, PSDLayer previousLayer, string spriteName)
        {
            bool moved = storedSprite.rect.position != (Vector2)previousLayer.mosaicPosition;
            bool renamed = !string.IsNullOrEmpty(previousLayer.spriteName) && storedSprite.name != previousLayer.spriteName && storedSprite.name != spriteName;
            return moved || renamed;
        }

        static PSDImportOutput ImportFromLayers(AssetImportContext ctx, IImportConfigProvider importParameter)
        {
            ImportTextureWrapper output = null;
            NativeArray<Color32> outputImageBuffer = default(NativeArray<Color32>);

            List<int> layerIndex = new List<int>();
            UniqueNameGenerator spriteNameHash = new UniqueNameGenerator();

            IReadOnlyList<PSDLayer> oldPsdLayers = importParameter.obsoletePsdLayers;
            List<PSDLayer> psdLayers = new();
            int packedImageWidth = 0;
            int packedImageHeight = 0;
            List<SpriteMetaData> newSpriteMeta = new List<SpriteMetaData>();
            try
            {
                ExtractLayerTask.Execute(importParameter.extractedLayerData, out psdLayers, importParameter.importHiddenLayers, importParameter.canvasSize);

                IPSDLayerMappingStrategy mappingStrategy = ImportUtilities.GetLayerMappingStrategy(importParameter.layerMappingOption);
                string layerUnique = mappingStrategy.LayersUnique(psdLayers.ConvertAll(x => (IPSDLayerMappingStrategyComparable)x));
                if (!string.IsNullOrEmpty(layerUnique))
                {
                    importParameter.logger(LogType.Warning, layerUnique);
                }

                // Pair one to one in document order so that layers sharing an identifier, e.g.
                // duplicated layer names, are each compared with their own previous layer.
                PSDLayer[] pairedOldPsdLayers = new PSDLayer[psdLayers.Count];
                HashSet<PSDLayer> mappedOldPsdLayers = new HashSet<PSDLayer>();
                for (int i = 0; i < psdLayers.Count; ++i)
                {
                    PSDLayer psdLayer = psdLayers[i];
                    PSDLayer match = oldPsdLayers.FirstOrDefault(x => !mappedOldPsdLayers.Contains(x) && mappingStrategy.Compare(psdLayer, x));
                    if (match != null)
                    {
                        pairedOldPsdLayers[i] = match;
                        mappedOldPsdLayers.Add(match);
                    }
                }

                List<NativeArray<Color32>> layerBuffers = new List<NativeArray<Color32>>();
                List<int> layerWidth = new List<int>();
                List<int> layerHeight = new List<int>();
                for (int i = 0; i < psdLayers.Count; ++i)
                {
                    PSDLayer l = psdLayers[i];
                    int expectedBufferLength = l.width * l.height;
                    if (l.texture.IsCreated && l.texture.Length == expectedBufferLength && l.isImported)
                    {
                        layerBuffers.Add(l.texture);
                        layerIndex.Add(i);
                        layerWidth.Add(l.width);
                        layerHeight.Add(l.height);
                    }
                }

                PackImage(layerBuffers.ToArray(), layerWidth.ToArray(), layerHeight.ToArray(), importParameter.padding, importParameter.spriteSizeExpand, importParameter.logger, importParameter.pipeline, out outputImageBuffer, out packedImageWidth, out packedImageHeight, out RectInt[] spriteData, out Vector2Int[] uvTransform, importParameter.RequireSquarePOT(ctx));

                Vector2[] packOffsets = new Vector2[spriteData.Length];
                for (int i = 0; i < packOffsets.Length; ++i)
                    packOffsets[i] = new Vector2((uvTransform[i].x - spriteData[i].position.x) / -1f, (uvTransform[i].y - spriteData[i].position.y) / -1f);

                IReadOnlyList<SpriteMetaData> spriteImportData = CopySpriteMetaData(importParameter.spriteMetaData);
                List<string> discardedSpriteEdits = new List<string>();
                for (int i = 0; i < spriteData.Length && i < layerIndex.Count; ++i)
                {
                    PSDLayer psdLayer = psdLayers[layerIndex[i]];
                    PSDLayer oldPsdLayer = pairedOldPsdLayers[layerIndex[i]];
                    string spriteName = ImportUtilities.GetUniqueSpriteName(psdLayer.name, spriteNameHash, importParameter.keepDuplicateSpriteName);
                    SpriteMetaData spriteSheet = spriteImportData.FirstOrDefault(x => x.spriteID == psdLayer.spriteID);
                    if (spriteSheet == null)
                    {
                        spriteSheet = new SpriteMetaData();
                        spriteSheet.border = Vector4.zero;
                        spriteSheet.alignment = importParameter.singleModeSpriteAlignment;
                        spriteSheet.pivot = importParameter.singleModeSpritePivot;
                        spriteSheet.spriteID = psdLayer.spriteID;
                    }
                    else if (oldPsdLayer != null && oldPsdLayer.isImported && IsSpriteEditedFromLayer(spriteSheet, oldPsdLayer, spriteName))
                    {
                        discardedSpriteEdits.Add(spriteSheet.name);
                    }

                    psdLayer.spriteName = spriteName;
                    spriteSheet.name = spriteName;
                    spriteSheet.spritePosition = psdLayer.layerPosition + packOffsets[i];
                    spriteSheet.rect = new Rect(spriteData[i].x, spriteData[i].y, spriteData[i].width, spriteData[i].height);
                    spriteSheet.uvTransform = uvTransform[i];

                    psdLayer.spriteID = spriteSheet.spriteID;
                    psdLayer.mosaicPosition = spriteData[i].position;
                    newSpriteMeta.Add(spriteSheet);
                }

                HashSet<GUID> layerSpriteIds = new HashSet<GUID>(oldPsdLayers.Select(x => x.spriteID).Concat(psdLayers.Select(x => x.spriteID)));
                foreach (SpriteMetaData userCreatedSprite in spriteImportData.Where(x => !layerSpriteIds.Contains(x.spriteID)))
                    discardedSpriteEdits.Add(userCreatedSprite.name);

                if (discardedSpriteEdits.Count > 0)
                    importParameter.logger(LogType.Warning, string.Format(TextContent.mosaicSpriteEditsDiscardedWarning, string.Join(", ", discardedSpriteEdits)));

                output = new ImportTextureWrapper(packedImageWidth, packedImageHeight, outputImageBuffer, newSpriteMeta);
            }
            finally
            {
                if (output == null && outputImageBuffer.IsCreated)
                {
                    outputImageBuffer.Dispose();
                    output = new ImportTextureWrapper(0, 0, default(NativeArray<Color32>), new List<SpriteMetaData>());
                }
            }

            return new PSDImportOutput(output, psdLayers, newSpriteMeta);
        }

        static void RegisterAssets(AssetImportContext ctx, ProducedAsset producedAsset, Action<LogType, string> logger)
        {
            ctx.AddObjectToAsset("PSDImportData", producedAsset.importData);
            if ((producedAsset.sprites == null || producedAsset.sprites.Count == 0) && producedAsset.texture == null)
            {
                logger(LogType.Warning, TextContent.noSpriteOrTextureImportWarning);
                return;
            }

            UniqueNameGenerator assetNameGenerator = new UniqueNameGenerator();

            if (producedAsset.thumbNail == null)
                Debug.LogWarning("Thumbnail generation fail");
            if (producedAsset.texture == null)
            {
                throw new Exception("Texture import fail");
            }

            string assetName = assetNameGenerator.GetUniqueName(System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath), true);
            UnityEngine.Object mainAsset = null;

            RegisterTextureAsset(ctx, producedAsset, assetName, ref mainAsset);
            RegisterSpriteLibraryAsset(ctx, producedAsset, assetName);
            RegisterGameObjects(ctx, producedAsset, ref mainAsset);
            RegisterSprites(ctx, producedAsset, assetNameGenerator);
            RegisterSkeletonAsset(ctx, producedAsset, assetName);
            RegisterTilemapAsset(ctx, producedAsset, assetName, ref mainAsset);
            ctx.SetMainObject(mainAsset);
        }

        static void RegisterTextureAsset(AssetImportContext ctx, ProducedAsset producedAsset, string assetName, ref UnityEngine.Object mainAsset)
        {
            string registerTextureNameId = string.IsNullOrEmpty(producedAsset.textureAssetName) ? "Texture" : producedAsset.textureAssetName;

            producedAsset.texture.name = assetName;
            ctx.AddObjectToAsset(registerTextureNameId, producedAsset.texture, producedAsset.thumbNail);
            mainAsset = producedAsset.texture;
        }

        static void RegisterSpriteLibraryAsset(AssetImportContext ctx, ProducedAsset producedAsset, string assetName)
        {
#if ENABLE_2D_ANIMATION
            if (producedAsset.spriteLibraryAsset != null)
            {
                string spriteLibAssetNameId = string.IsNullOrEmpty(producedAsset.spriteLibAssetName) ? "SpriteLibAsset" : producedAsset.spriteLibAssetName;
                ctx.AddObjectToAsset(spriteLibAssetNameId, producedAsset.spriteLibraryAsset);
            }
#endif
        }

        static void RegisterGameObjects(AssetImportContext ctx, ProducedAsset producedAsset, ref UnityEngine.Object mainAsset)
        {
            if (producedAsset.prefabRoot != null)
            {
                ctx.AddObjectToAsset(producedAsset.prefabAssetName, producedAsset.prefabRoot);
                mainAsset = producedAsset.prefabRoot;
            }
        }

        static void RegisterSprites(AssetImportContext ctx, ProducedAsset producedAsset, UniqueNameGenerator assetNameGenerator)
        {
            if (producedAsset.sprites == null)
                return;

            foreach (Sprite s in producedAsset.sprites)
            {
                string spriteAssetName = assetNameGenerator.GetUniqueName(s.GetSpriteID().ToString(), false);
                ctx.AddObjectToAsset(spriteAssetName, s);
            }
        }

        static void RegisterSkeletonAsset(AssetImportContext ctx, ProducedAsset producedAsset, string assetName)
        {
#if ENABLE_2D_ANIMATION
            if (producedAsset.skeletonAsset != null && !string.IsNullOrEmpty(producedAsset.skeletonAssetName))
                ctx.AddObjectToAsset(producedAsset.skeletonAssetName, producedAsset.skeletonAsset);
            if (!string.IsNullOrEmpty(producedAsset.skeletonAssetReferenceID))
            {
                string primaryAssetPath = AssetDatabase.GUIDToAssetPath(producedAsset.skeletonAssetReferenceID);
                if (!string.IsNullOrEmpty(primaryAssetPath) && primaryAssetPath != ctx.assetPath)
                {
                    ctx.DependsOnArtifact(primaryAssetPath);
                }
            }
#endif
        }

        static void RegisterTilemapAsset(AssetImportContext ctx
            , ProducedAsset producedAsset
            , string assetName
            , ref UnityEngine.Object mainAsset)
        {
#if ENABLE_2D_TILEMAP_EDITOR
            if (producedAsset.gridPalette != null)
                ctx.AddObjectToAsset("GridPalette", producedAsset.gridPalette);
            if (producedAsset.tiles != null)
            {
                foreach (TileBase tile in producedAsset.tiles)
                {
                    // Use Sprite ID + Tile for pure Tile
                    if (tile is Tile t && t.sprite != null)
                    {
                        ctx.AddObjectToAsset($"{t.sprite.GetSpriteID()} Tile", tile);
                    }
                    else
                    {
                        ctx.AddObjectToAsset(tile.name, tile);
                    }
                }
            }

            if (producedAsset.tilePaletteGO != null)
            {
                ctx.AddObjectToAsset("Palette", producedAsset.tilePaletteGO);
                mainAsset = producedAsset.tilePaletteGO;
            }
#endif
        }

        static void BuildGroupGameObject(IReadOnlyList<PSDLayer> psdGroup, int index, Transform root, IImportConfigProvider config, GameObjectCreationFactory gameObjectFactory)
        {
            PSDLayer psdData = psdGroup[index];
            if (psdData.gameObject == null)
            {
                bool spriteImported = !psdGroup[index].spriteID.Empty() && psdGroup[index].isImported;
                bool isVisibleGroup = psdData.isGroup && (ImportUtilities.VisibleInHierarchy(psdGroup, index) || config.importHiddenLayers) && config.generateGOHierarchy;
                if (spriteImported || isVisibleGroup)
                {
                    SpriteMetaData spriteData = config.importedSpriteMetaData.FirstOrDefault(x => x.spriteID == psdData.spriteID);
#if ENABLE_2D_ANIMATION
                    // Determine if need to create GameObject i.e. if the sprite is not in a SpriteLib or if it is the first one
                    bool shouldCreateGo = ImportUtilities.SpriteIsMainFromSpriteLib(config.spriteCategoryList.categories, psdData.spriteID.ToString(), out string categoryName);
                    string goName = string.IsNullOrEmpty(categoryName) ? spriteData != null ? spriteData.name : psdData.name : categoryName;
                    if (shouldCreateGo)
#else
                    string goName = spriteData != null ? spriteData.name : psdData.name;
#endif
                    {
                        psdData.gameObject = gameObjectFactory.CreateGameObject(goName);
                    }
                }

                if (psdData.parentIndex >= 0 && config.generateGOHierarchy && psdData.gameObject != null)
                {
                    BuildGroupGameObject(psdGroup, psdData.parentIndex, root, config, gameObjectFactory);
                    root = psdGroup[psdData.parentIndex].gameObject.transform;
                }

                if (psdData.gameObject != null)
                {
                    psdData.gameObject.transform.SetParent(root);
                    psdData.gameObject.transform.SetSiblingIndex(root.childCount - 1);
                }
            }
        }

        static void CreateBoneGO(int index, SpriteBone[] bones, BoneGO[] bonesGO, Transform defaultRoot, IImportConfigProvider config, GameObjectCreationFactory gameObjectCreationFactory)
        {
            if (bonesGO[index].go != null)
                return;
            SpriteBone bone = bones[index];
            if (bone.parentId != -1 && bonesGO[bone.parentId].go == null)
                CreateBoneGO(bone.parentId, bones, bonesGO, defaultRoot, config, gameObjectCreationFactory);

            GameObject go = gameObjectCreationFactory.CreateGameObject(bone.name);
            if (bone.parentId == -1)
                go.transform.SetParent(defaultRoot);
            else
                go.transform.SetParent(bonesGO[bone.parentId].go.transform);
            go.transform.localPosition = bone.position * 1 / config.pixelsPerUnit;
            go.transform.localRotation = bone.rotation;
            bonesGO[index] = new BoneGO()
            {
                go = go,
                index = index
            };
        }

        static BoneGO[] CreateBonesGO(Transform root, IImportConfigProvider config, GameObjectCreationFactory gameObjectCreationFactory)
        {
#if ENABLE_2D_ANIMATION
            if (config.inCharacterMode)
            {
                CharacterData characterSkeleton = config.characterData;
                SpriteBone[] bones = characterSkeleton.bones;
                if (bones != null)
                {
                    BoneGO[] boneGOs = new BoneGO[bones.Length];
                    for (int i = 0; i < bones.Length; ++i)
                    {
                        CreateBoneGO(i, bones, boneGOs, root, config, gameObjectCreationFactory);
                    }
                    return boneGOs;
                }
            }
#endif
            return new BoneGO[0];
        }

        static void GetSpriteLibLabel(string spriteId, IImportConfigProvider config, out string category, out string label)
        {
            category = "";
            label = "";

#if ENABLE_2D_ANIMATION
            foreach (SpriteCategory cat in config.spriteCategoryList.categories)
            {
                int index = cat.labels.FindIndex(x => x.spriteId == spriteId);
                if (index != -1)
                {
                    category = cat.name;
                    label = cat.labels[index].name;
                    break;
                }
            }
#endif
        }

        static GameObject OnProducePaperDollPrefab(string assetName, ProducedAsset producedAsset, IReadOnlyList<PSDLayer> psdLayers, IImportConfigProvider config, GameObjectCreationFactory gameObjectFactory)
        {
            GameObject root = null;
            IReadOnlyList<Sprite> sprites = producedAsset.sprites;
            if (sprites != null && sprites.Count > 0)
            {
                root = new GameObject();
                root.name = assetName + "_GO";

#if ENABLE_2D_ANIMATION
                IReadOnlyList<SpriteMetaData> spriteImportData = config.importedSpriteMetaData;
                BoneGO[] boneGOs = CreateBonesGO(root.transform, config, gameObjectFactory);

                SpriteLibraryAsset spriteLib = producedAsset.spriteLibraryAsset;

                if (spriteLib != null)
                    root.AddComponent<SpriteLibrary>().spriteLibraryAsset = spriteLib;

                CharacterData currentCharacterData = config.characterData;
                for (int i = 0; i < sprites.Count; ++i)
                {
                    if (ImportUtilities.SpriteIsMainFromSpriteLib(config.spriteCategoryList.categories, sprites[i].GetSpriteID().ToString(), out string categoryName))
                    {
                        int[] spriteBones = currentCharacterData.parts.FirstOrDefault(x => new GUID(x.spriteId) == sprites[i].GetSpriteID()).bones;
                        GameObject rootBone = root;
                        if (spriteBones != null && spriteBones.Any())
                        {
                            IOrderedEnumerable<BoneGO> b = spriteBones.Where(x => x >= 0 && x < boneGOs.Length).Select(x => boneGOs[x]).OrderBy(x => x.index);
                            if (b.Any())
                                rootBone = b.First().go;
                        }

                        GameObject srGameObject = gameObjectFactory.CreateGameObject(string.IsNullOrEmpty(categoryName) ? sprites[i].name : categoryName);
                        SpriteRenderer sr = srGameObject.AddComponent<SpriteRenderer>();
                        sr.sprite = sprites[i];
                        sr.sortingOrder = psdLayers.Count - psdLayers.FindIndex(x => x.spriteID == sprites[i].GetSpriteID());
                        srGameObject.transform.parent = rootBone.transform;
                        SpriteMetaData spriteMetaData = spriteImportData.FirstOrDefault(x => x.spriteID == sprites[i].GetSpriteID());
                        if (spriteMetaData != null)
                        {
                            Vector2Int uvTransform = spriteMetaData.uvTransform;
                            Vector2 outlineOffset = new Vector2(spriteMetaData.rect.x - uvTransform.x + (spriteMetaData.pivot.x * spriteMetaData.rect.width),
                                spriteMetaData.rect.y - uvTransform.y + (spriteMetaData.pivot.y * spriteMetaData.rect.height)) * config.definitionScale / sprites[i].pixelsPerUnit;
                            srGameObject.transform.position = new Vector3(outlineOffset.x, outlineOffset.y, 0);
                        }

                        GetSpriteLibLabel(sprites[i].GetSpriteID().ToString(), config, out string category, out string labelName);
                        if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(labelName))
                        {
                            SpriteResolver resolver = srGameObject.AddComponent<SpriteResolver>();
                            resolver.SetCategoryAndLabel(category, labelName);
                            resolver.ResolveSpriteToSpriteRenderer();
                        }
                    }
                }
#endif
            }
            return root;
        }

        internal void SetPlatformTextureSettings(TextureImporterPlatformSettings platformSettings)
        {
            int index = m_PlatformSettings.FindIndex(x => x.name == platformSettings.name);
            if (index < 0)
                m_PlatformSettings.Add(platformSettings);
            else
                m_PlatformSettings[index] = platformSettings;
        }

        internal TextureImporterPlatformSettings[] GetAllPlatformSettings()
        {
            return m_PlatformSettings.ToArray();
        }

        static GameObject OnProducePrefab(string assetname, ProducedAsset producedAsset, IReadOnlyList<PSDLayer> psdLayers, IImportConfigProvider config, GameObjectCreationFactory gameObjectFactory, SpriteAlignment documentAlignment, Vector2 documentPivot)
        {
            GameObject root = null;
            IReadOnlyList<Sprite> sprites = producedAsset.sprites;
            if (sprites != null && sprites.Count > 0)
            {
                IReadOnlyList<SpriteMetaData> spriteImportData = config.importedSpriteMetaData;
                root = new GameObject();
                root.transform.SetSiblingIndex(0);
                root.name = assetname + "_GO";

#if ENABLE_2D_ANIMATION
                CharacterData currentCharacterData = config.characterData;
                SpriteLibraryAsset spriteLib = producedAsset.spriteLibraryAsset;
                if (spriteLib != null)
                    root.AddComponent<SpriteLibrary>().spriteLibraryAsset = spriteLib;

                CharacterData? characterSkeleton = config.inCharacterMode ? new CharacterData?(config.characterData) : null;
#endif

                for (int i = 0; i < psdLayers.Count; ++i)
                {
                    BuildGroupGameObject(psdLayers, i, root.transform, config, gameObjectFactory);
                }
                BoneGO[] boneGOs = CreateBonesGO(root.transform, config, gameObjectFactory);
                for (int i = 0; i < psdLayers.Count; ++i)
                {
                    PSDLayer l = psdLayers[i];
                    GUID layerSpriteID = l.spriteID;
                    Sprite sprite = sprites.FirstOrDefault(x => x.GetSpriteID() == layerSpriteID);
                    SpriteMetaData spriteMetaData = spriteImportData.FirstOrDefault(x => x.spriteID == layerSpriteID);
                    if (sprite != null && spriteMetaData != null && l.gameObject != null)
                    {
                        SpriteRenderer spriteRenderer = l.gameObject.AddComponent<SpriteRenderer>();
                        spriteRenderer.sprite = sprite;
                        spriteRenderer.sortingOrder = psdLayers.Count - i;

                        Vector2 pivot = spriteMetaData.pivot;
                        pivot.x *= spriteMetaData.rect.width;
                        pivot.y *= spriteMetaData.rect.height;

                        Vector2 spritePosition = spriteMetaData.spritePosition;
                        spritePosition.x += pivot.x;
                        spritePosition.y += pivot.y;
                        spritePosition *= (config.definitionScale / sprite.pixelsPerUnit);

                        l.gameObject.transform.position = new Vector3(spritePosition.x, spritePosition.y, 0f);

#if ENABLE_2D_ANIMATION
                        if (characterSkeleton != null)
                        {
                            CharacterPart part = characterSkeleton.Value.parts.FirstOrDefault(x => x.spriteId == spriteMetaData.spriteID.ToString());
                            if (part.bones != null && part.bones.Length > 0)
                            {
                                SpriteSkin spriteSkin = l.gameObject.AddComponent<SpriteSkin>();
                                if (spriteRenderer.sprite != null && spriteRenderer.sprite.GetBindPoses().Length > 0)
                                {
                                    IEnumerable<BoneGO> spriteBones = currentCharacterData.parts.FirstOrDefault(x => new GUID(x.spriteId) == spriteRenderer.sprite.GetSpriteID()).bones.Where(x => x >= 0 && x < boneGOs.Length).Select(x => boneGOs[x]);
                                    if (spriteBones.Any())
                                    {
                                        spriteSkin.SetRootTransform(root.transform);
                                        spriteSkin.SetBoneTransforms(spriteBones.Select(x => x.go.transform).ToArray());
                                        if (spriteSkin.isValid)
                                            spriteSkin.CalculateBounds();
                                    }
                                }
                            }
                        }

                        GetSpriteLibLabel(layerSpriteID.ToString(), config, out string category, out string labelName);
                        if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(labelName))
                        {
                            SpriteResolver resolver = l.gameObject.AddComponent<SpriteResolver>();
                            resolver.SetCategoryAndLabel(category, labelName);
                            resolver.ResolveSpriteToSpriteRenderer();
                        }
#endif
                    }
                }

                Rect prefabBounds = new Rect(0, 0, config.canvasSize.x / config.pixelsPerUnit, config.canvasSize.y / config.pixelsPerUnit);
                Vector3 docPivot = (Vector3)ImportUtilities.GetPivotPoint(prefabBounds, documentAlignment, documentPivot);
                for (int i = 0; i < psdLayers.Count; ++i)
                {
                    PSDLayer l = psdLayers[i];
                    if (l.gameObject == null || l.gameObject.GetComponent<SpriteRenderer>() == null)
                        continue;
                    Vector3 p = l.gameObject.transform.localPosition;
                    p -= docPivot;
                    l.gameObject.transform.localPosition = p;
                }
                for (int i = 0; i < boneGOs.Length; ++i)
                {
                    if (boneGOs[i].go.transform.parent != root.transform)
                        continue;
                    Vector3 p = boneGOs[i].go.transform.position;
                    p -= docPivot;
                    boneGOs[i].go.transform.position = p;
                }
            }

            return root;
        }

        internal void Apply()
        {
            // Do this so that asset change save dialog will not show
            bool originalValue = EditorPrefs.GetBool("VerifySavingAssets", false);
            EditorPrefs.SetBool("VerifySavingAssets", false);
            AssetDatabase.ForceReserializeAssets(new string[] { assetPath }, ForceReserializeAssetsOptions.ReserializeMetadata);
            EditorPrefs.SetBool("VerifySavingAssets", originalValue);
        }

#if ENABLE_2D_ANIMATION
        SkeletonAsset skeletonAsset =>
            AssetDatabase.LoadAssetAtPath<SkeletonAsset>(AssetDatabase.GUIDToAssetPath(m_SkeletonAssetReferenceID));
#endif

        internal List<PSDLayer> GetPSDLayers() => m_PsdLayers;

        List<SpriteMetaData> GetSpriteImportData()
        {
            if (spriteImportModeToUse == SpriteImportMode.Multiple)
            {
                if (inMosaicMode)
                    return m_LayeredSpriteImportData;
                return m_MultiSpriteImportData;
            }

            return new List<SpriteMetaData> { GetSingleSpriteImportData() };
        }

        internal SpriteMetaData[] GetSpriteMetaData()
        {
            if (spriteImportModeToUse == SpriteImportMode.Multiple)
            {
                if (inMosaicMode)
                    return m_LayeredSpriteImportData.ToArray();
                return m_MultiSpriteImportData.ToArray();
            }

            return new[] { GetSingleSpriteImportData() };
        }

        internal SpriteRect GetSpriteData(GUID guid)
        {
            if (spriteImportModeToUse == SpriteImportMode.Multiple)
            {
                if (inMosaicMode)
                    return m_LayeredSpriteImportData.FirstOrDefault(x => x.spriteID == guid);
                return m_MultiSpriteImportData.FirstOrDefault(x => x.spriteID == guid);
            }

            // Data providers write authored data through this, so hand out the serialized instance.
            // GetSingleSpriteImportData returns a derived copy, which would discard those writes.
            return m_SingleSpriteImportData[0];
        }

        SpriteMetaData GetSingleSpriteImportData()
        {
            SpriteMetaData spriteMetaData = new SpriteMetaData();
            spriteMetaData.Copy(m_SingleSpriteImportData[0]);
            spriteMetaData.spriteID = AssetDatabase.GUIDFromAssetPath(assetPath);
            if (assetPath != null)
                spriteMetaData.name = System.IO.Path.GetFileNameWithoutExtension(assetPath) + "_1";
            if (importData != null)
            {
                spriteMetaData.rect = new Rect(0, 0, importData.textureActualWidth, importData.textureActualHeight);
                spriteMetaData.pivot = m_TextureImporterSettings.spritePivot;
                spriteMetaData.alignment = (SpriteAlignment)m_TextureImporterSettings.spriteAlignment;
                spriteMetaData.border = m_TextureImporterSettings.spriteBorder;
            }

            return spriteMetaData;
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

        internal Vector2Int canvasSize => importData.documentSize;

#if ENABLE_2D_ANIMATION
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

        SpriteLibraryAsset ProduceSpriteLibAsset(Sprite[] sprites)
        {
            if (!inCharacterMode || m_SpriteCategoryList.categories == null)
                return null;
            List<SpriteLibCategory> categories = m_SpriteCategoryList.categories.Select(x =>
                new SpriteLibCategory()
                {
                    name = x.name,
                    categoryList = x.labels.Select(y =>
                    {
                        Sprite sprite = sprites.FirstOrDefault(z => z.GetSpriteID().ToString() == y.spriteId);
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
#endif

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
            foreach (Sprite sprite in sprites)
            {
                GUID guid = sprite.GetSpriteID();
                List<Vector2[]> outline = physicsOutlineDataProvider.GetOutlines(guid);

                Vector2 outlineOffset = sprite.rect.size / 2;
                bool generated = false;
                if ((outline == null || outline.Count == 0) && generatePhysicsShape)
                {
                    InternalEditorBridge.GenerateOutlineFromSprite(sprite, 0.25f, 200, true, out Vector2[][] defaultOutline);
                    outline = new List<Vector2[]>(defaultOutline.Length);
                    for (int i = 0; i < defaultOutline.Length; ++i)
                    {
                        outline.Add(defaultOutline[i]);
                    }

                    generated = true;
                }
                if (outline != null && outline.Count > 0)
                {
                    // Ensure that outlines are all valid.
                    int validOutlineCount = 0;
                    for (int i = 0; i < outline.Count; ++i)
                        validOutlineCount += ((outline[i].Length > 2) ? 1 : 0);

                    int index = 0;
                    Vector2[][] convertedOutline = new Vector2[validOutlineCount][];
                    float useScale = generated ? pixelsPerUnit * definitionScale : definitionScale;

                    for (int i = 0; i < outline.Count; ++i)
                    {
                        if (outline[i].Length > 2)
                        {
                            convertedOutline[index] = new Vector2[outline[i].Length];
                            for (int j = 0; j < outline[i].Length; ++j)
                            {
                                convertedOutline[index][j] = outline[i][j] * useScale + outlineOffset;
                            }
                            index++;
                        }
                    }
                    sprite.OverridePhysicsOutline(convertedOutline);
                }
            }
        }

        void Logger(LogType logType, string msg)
        {
            switch (logType)
            {
                case LogType.Log:
                    Debug.Log(msg, this);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(msg, this);
                    break;
                case LogType.Error:
                    Debug.LogError(msg, this);
                    break;
                default:
                    Debug.LogAssertion(msg, this);
                    break;
            }
        }

        internal IReadOnlyList<SpriteMetaData> GetStoredSpriteRectData()
        {
            if (spriteImportModeToUse == SpriteImportMode.Multiple)
            {
                List<SpriteMetaData> inImporter;
                if (inMosaicMode)
                    inImporter = m_LayeredSpriteImportData;
                else
                    inImporter = m_MultiSpriteImportData;
                return inImporter;
            }

            return new List<SpriteMetaData> { GetSingleSpriteImportData() };
        }

        ELayerMappingOption layerMappingOption
        {
            get
            {
                // if(importerVersion == ImporterVersion.V2 && importData != null && importData.mappingOption != ELayerMappingOption.Unknown)
                //     return importData.mappingOption;
                return m_LayerMappingOption;
            }
        }
    }
}
