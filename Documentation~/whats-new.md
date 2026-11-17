# What's new in the PSD Importer package

Discover new features and improvements in the latest updates to PSD Importer.

For more information, refer to the [changelog](../changelog/CHANGELOG.html).

## Version 17.0.0

### Upgrade projects from Unity 2022.3 through Unity 6000.x

The PSD Importer no longer migrates import settings saved by PSD Importer 8.x or earlier. If your project was last opened in Unity 2022.3, open it in a Unity 6000.x version first so that the PSD Importer re-imports your .psb and .psd files and rewrites their .meta files in the current format. Commit the rewritten .meta files, then upgrade to this version.

If you skip this step, the PSD Importer discards the saved sprite and layer settings of those files without logging a warning:

- Sprite IDs are regenerated, so references to the sprites in scenes, prefabs, and animation clips break.
- Rigging data, such as bones and weights, is lost.

To recover, restore the .meta files from version control, open the project in Unity 6000.x, and then upgrade again.

### Importing no longer changes .meta files

The PSD Importer no longer writes to the .meta file of a .psb or .psd file while it imports the file. The import result depends only on the source file and its .meta file, so reimporting a file no longer leaves its .meta file modified. You can commit or discard the modifications that earlier versions left in .meta files.

Sprite IDs are paired with the layers that the importer records in the .meta file. The importer now records the layers when you apply changes in the Inspector or the Sprite Editor, so Sprite IDs change only when you apply. A file imported for the first time with this version keeps the Sprite IDs generated from its layers until you apply it the first time. If a script changes **Layer Mapping** of such a file through `SerializedObject` before the file was applied, its Sprite IDs change. Apply the file once, for example in the Inspector, before you change **Layer Mapping** from a script.

### Automatic Reslice removed

The **Automatic Reslice** option has been removed. When **Import Mode** is set to **Individual Sprites (Mosaic)**, the PSD Importer now always generates the `SpriteRect` data of each sprite from its layer in the source file. If you added, deleted, moved, resized, or renamed `SpriteRect` data in this mode while the option was disabled, the importer discards those changes the next time it imports the source file, and logs a warning that lists the sprites that were moved or renamed, or that had no source layer. The importer no longer uses those changes, so the warning stops once you apply any change to the asset, which removes them from the .meta file. In a project upgraded from an earlier version, the first time you apply a change to such an asset, its `SpriteRect` data in the .meta file is rewritten at once. Everyone on the team gets the same change, so commit it once. A renamed sprite takes the name of its layer again, and so does the GameObject that the importer generates for it when **Use as Rig** is enabled. When **Layer Mapping** is set to **Use Layer ID**, you can keep a custom name by giving the layer that name in the source file. The importer keeps pivots, borders, bones, weights, and sprite IDs. For more information, refer to [How the PSD Importer uses SpriteRect data](PSD-importer-SpriteRect.md).

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
