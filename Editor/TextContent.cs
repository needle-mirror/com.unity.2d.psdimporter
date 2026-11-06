namespace UnityEditor.U2D.PSD
{
    internal static class TextContent
    {
        internal static readonly string noSpriteOrTextureImportWarning = L10n.Tr("No Sprites or Texture are generated. Possibly because all layers in file are hidden or failed to generate texture.");
        internal static readonly string mosaicSpriteEditsDiscardedWarning = L10n.Tr("Automatic Reslice has been removed. In Individual Sprites (Mosaic) mode, Sprite rects and names always come from the layers of the source file. Rects and names that differed from their layers were reset, and Sprites with no source layer were removed: {0}. To change a Sprite rect, edit its layer in the source file. When Layer Mapping is set to Use Layer ID, you can keep a custom Sprite name by giving its layer that name in the source file.");
    }
}
