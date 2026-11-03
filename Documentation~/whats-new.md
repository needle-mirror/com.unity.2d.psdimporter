# What's new in the PSD Importer package

Discover new features and improvements in the latest updates to PSD Importer.

For more information, refer to the [changelog](../changelog/CHANGELOG.html).

## Version 14.0.1

### Importer removes tangent data from sprites with bones

The PSD Importer no longer automatically adds tangent data to vertex attributes of sprites that have bones. For more information, refer to [PSD Importer Inspector properties](PSD-importer-properties.md).

## Version 13.0.1

### Correct positions of layer masks

Layer masks are now positioned correctly relative to their associated layer when imported. For more information, refer to [PSD Importer Inspector properties](PSD-importer-properties.md).

## Version 13.0.0

### Importing excludes empty layers

The PSD Importer now automatically excludes empty layers from the import file. For more information, refer to [PSD Importer Inspector properties](PSD-importer-properties.md).

## Version 10.0.0

### Sprite Frame Editing with source-locked data

You can now edit the `SpriteRect` data of sprites you import from a .psb file. For more information, refer to [How the PSD Importer uses SpriteRect data](PSD-importer-SpriteRect.md).

### Generate Tile assets and Tile Palettes from a .psb file

You can now generate tile assets and tile palettes directly from a .psb file. For more information, refer to [PSD Importer Inspector properties](PSD-importer-properties.md).

## Version 9.0.1

### PVRTC texture compression supported on iOS

The PSD Importer now correctly applies PowerVR Texture Compression (PVRTC) compression when you build for iOS platforms. Previously, selecting PVRTC had no effect and textures remained uncompressed. For more information, refer to [PSD Importer Inspector properties](PSD-importer-properties.md).

## Additional resources

- [Changelog](../changelog/CHANGELOG.html)
