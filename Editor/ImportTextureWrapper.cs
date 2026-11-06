using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor.AssetImporters;
using UnityEditor.U2D.Common;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    record ImportTextureWrapper(int actualTextureWidth,
        int actualTextureHeight,
        NativeArray<Color32> textureData,
        IReadOnlyList<SpriteMetaData> spriteMetaData)
    {
        TextureGenerationOutput textureGenerationOutput;
        public int actualTextureWidth { get; } = actualTextureWidth;
        public int actualTextureHeight { get; } = actualTextureHeight;
        public NativeArray<Color32> textureData { get; } = textureData;
        public IReadOnlyList<SpriteMetaData> spriteMetaData { get; } = spriteMetaData;

        void GenerateIfNeeded(AssetImportContext ctx, ITextureImporterSettings textureImporterSettings)
        {
            if (textureGenerationOutput.sprites == null &&
                textureGenerationOutput.texture == null &&
                textureGenerationOutput.thumbNail == null &&
                textureData.IsCreated && textureData.Length > 0)
            {
                textureGenerationOutput = ImportTexture(ctx, textureData, actualTextureWidth, actualTextureHeight, spriteMetaData, textureImporterSettings);
            }
        }

        public Sprite[] GetSprites(AssetImportContext ctx, ITextureImporterSettings textureImporterSettings)
        {
            GenerateIfNeeded(ctx, textureImporterSettings);
            return textureGenerationOutput.sprites;
        }

        public Texture2D GetTexture(AssetImportContext ctx, ITextureImporterSettings textureImporterSettings)
        {
            GenerateIfNeeded(ctx, textureImporterSettings);
            return textureGenerationOutput.texture;
        }

        public Texture2D GetThumbnail(AssetImportContext ctx, ITextureImporterSettings textureImporterSettings)
        {
            GenerateIfNeeded(ctx, textureImporterSettings);
            return textureGenerationOutput.thumbNail;
        }

        public string GetImportInspectorWarnings(AssetImportContext ctx, ITextureImporterSettings textureImporterSettings)
        {
            GenerateIfNeeded(ctx, textureImporterSettings);
            return textureGenerationOutput.importInspectorWarnings;
        }

        public string[] GetImportWarnings(AssetImportContext ctx, ITextureImporterSettings textureImporterSettings)
        {
            GenerateIfNeeded(ctx, textureImporterSettings);
            return textureGenerationOutput.importWarnings;
        }
        static TextureGenerationOutput ImportTexture(AssetImportContext ctx, NativeArray<Color32> imageData, int textureWidth, int textureHeight, IReadOnlyList<SpriteMetaData> sprites, ITextureImporterSettings interfaces)
        {
            if (!imageData.IsCreated || imageData.Length == 0)
                return new TextureGenerationOutput();

            TextureGenerationOutput output = new TextureGenerationOutput();
            UnityEngine.Profiling.Profiler.BeginSample("ImportTexture");
            try
            {
                interfaces.SetActualTextureDimensions(textureWidth, textureHeight);
                TextureImporterPlatformSettings platformSettings = interfaces.GetPlatformSettings(ctx);

                TextureSettings textureSettings = interfaces.ExtractTextureSettings();
                textureSettings.assetPath = ctx.assetPath;
                textureSettings.enablePostProcessor = true;
                textureSettings.containsAlpha = true;
                textureSettings.hdr = false;

                TextureAlphaSettings textureAlphaSettings = interfaces.ExtractTextureAlphaSettings();
                TextureMipmapSettings textureMipmapSettings = interfaces.ExtractTextureMipmapSettings();
                TextureCubemapSettings textureCubemapSettings = interfaces.ExtractTextureCubemapSettings();
                TextureWrapSettings textureWrapSettings = interfaces.ExtractTextureWrapSettings();

                switch (interfaces.textureType)
                {
                    case TextureImporterType.Default:
                        output = TextureGeneratorHelper.GenerateTextureDefault(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureAlphaSettings, textureMipmapSettings, textureCubemapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.NormalMap:
                        TextureNormalSettings textureNormalSettings = interfaces.ExtractTextureNormalSettings();
                        output = TextureGeneratorHelper.GenerateNormalMap(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureNormalSettings, textureMipmapSettings, textureCubemapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.GUI:
                        output = TextureGeneratorHelper.GenerateTextureGUI(imageData, textureWidth, textureHeight, textureSettings, platformSettings, textureAlphaSettings, textureMipmapSettings, textureWrapSettings);
                        break;
                    case TextureImporterType.Sprite:
                        TextureSpriteSettings textureSpriteSettings = interfaces.ExtractTextureSpriteSettings();
                        textureSpriteSettings.spriteSheetData = new SpriteImportData[sprites.Count];
                        textureSettings.npotScale = TextureImporterNPOTScale.None;
                        textureSettings.secondaryTextures = interfaces.secondaryTextures;

                        for (int i = 0; i < sprites.Count; ++i)
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
                interfaces.logger(LogType.Error, "Unable to generate Texture2D. Possibly texture size is too big to be generated. ex:" + e.ToString());
            }
            finally
            {
                UnityEngine.Profiling.Profiler.EndSample();
            }

            return output;
        }

        public void Dispose()
        {
            if (textureData.IsCreated)
                textureData.Dispose();
        }
    }

    interface ITextureImporterSettings
    {
        TextureSettings ExtractTextureSettings();
        TextureAlphaSettings ExtractTextureAlphaSettings();
        TextureMipmapSettings ExtractTextureMipmapSettings();
        TextureCubemapSettings ExtractTextureCubemapSettings();
        TextureWrapSettings ExtractTextureWrapSettings();
        TextureNormalSettings ExtractTextureNormalSettings();
        TextureSpriteSettings ExtractTextureSpriteSettings();
        TextureImporterPlatformSettings GetPlatformSettings(AssetImportContext ctx);
        SecondarySpriteTexture[] secondaryTextures { get; }
        TextureImporterType textureType { get; }
        string spritePackingTag { get; }
        void SetActualTextureDimensions(int width, int height);
        Action<LogType, string> logger { get; }
    }

    public partial class PSDImporter : ITextureImporterSettings
    {
        TextureImporterPlatformSettings ITextureImporterSettings.GetPlatformSettings(AssetImportContext ctx)
        {
            return TextureImporterUtilities.GetPlatformTextureSettings(ctx.selectedBuildTarget, in m_PlatformSettings);
        }

        TextureSettings ITextureImporterSettings.ExtractTextureSettings()
        {
            return m_TextureImporterSettings.ExtractTextureSettings();
        }

        TextureAlphaSettings ITextureImporterSettings.ExtractTextureAlphaSettings()
        {
            return m_TextureImporterSettings.ExtractTextureAlphaSettings();
        }

        TextureMipmapSettings ITextureImporterSettings.ExtractTextureMipmapSettings()
        {
            return m_TextureImporterSettings.ExtractTextureMipmapSettings();
        }

        TextureCubemapSettings ITextureImporterSettings.ExtractTextureCubemapSettings()
        {
            return m_TextureImporterSettings.ExtractTextureCubemapSettings();
        }

        TextureWrapSettings ITextureImporterSettings.ExtractTextureWrapSettings()
        {
            return m_TextureImporterSettings.ExtractTextureWrapSettings();
        }

        TextureNormalSettings ITextureImporterSettings.ExtractTextureNormalSettings()
        {
            return m_TextureImporterSettings.ExtractTextureNormalSettings();
        }

        TextureSpriteSettings ITextureImporterSettings.ExtractTextureSpriteSettings()
        {
            return m_TextureImporterSettings.ExtractTextureSpriteSettings();
        }

        TextureImporterType ITextureImporterSettings.textureType => m_TextureImporterSettings.textureType;
        string ITextureImporterSettings.spritePackingTag => m_SpritePackingTag;

        SecondarySpriteTexture[] ITextureImporterSettings.secondaryTextures => this.secondaryTextures;

        Action<LogType, string> ITextureImporterSettings.logger => Logger;

        void ITextureImporterSettings.SetActualTextureDimensions(int width, int height)
        {
            importData.textureActualWidth = width;
            importData.textureActualHeight = height;
        }

    }
}
