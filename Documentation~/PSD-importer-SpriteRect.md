# How the PSD Importer uses SpriteRect data
The PSD Importer can store three separate sets of[ SpriteRect](https://docs.unity3d.com/Packages/com.unity.2d.sprite@1.0/api/UnityEditor.SpriteRect.html) data, with one set for each of the three combinations of Importer property settings below:
   1. When [Sprite Mode](PSD-importer-properties.html#SpriteMode) is set to **Single**.
   2. When **Sprite Mode** is set to **Multiple**.
   3. When **Sprite Mode** is set to **Multiple,** and [Individual Sprites (Mosaic)](PSD-importer-properties.html#Mosaic) is enabled.

Each set of data is persistent, and does not affect or overwrite the data of other sets. This means you can save different SpriteRect data for different importer settings for the same source file. In **Individual Sprites (Mosaic)** mode, the other data of each Sprite, such as its pivot, border, bones, and weights, persists even if you modify the dimensions and position of images in the source file, as long as the original [Layer ID](https://github.com/adobe-photoshop/generator-core/wiki/Understanding-Layer-IDs-and-Layer-Indices) of the source layers remains the same.

## Modifying SpriteRect data
The SpriteRect defines the location of the Sprite on the Texture that Unity generates from the imported source file. When **Sprite Mode** is set to **Multiple** and [Individual Sprites (Mosaic)](PSD-importer-properties.html#Mosaic) is disabled, you can modify the location and size of each SpriteRect in the Sprite Editor. In **Individual Sprites (Mosaic)** mode, the importer generates each SpriteRect from its source layer, so to change a SpriteRect, edit its layer in the source file.

### Moving the SpriteRect

Drag the corners of the SpriteRect to modify its dimensions and location, or enter the coordinates and dimensions in the Sprite panel.

A SpriteRect’s modified dimensions and location on the Texture is reflected for its respective Sprite in the Scene view.

### SpriteRect follows the position of its source layer
When you enable the [Individual Sprites (Mosaic)](PSD-importer-properties.html#Mosaic) importer setting, the PSD Importer arranges the different layers of the source file together to form a single combined Texture when it is imported. The importer generates a SpriteRect for each of these imported layers which follows the position of its associated layer wherever that layer is placed in the combined Mosaic Texture. The importer regenerates these SpriteRects from their layers every time it imports the source file, and discards any changes that scripts make to their size, position, or name.

### Resizing the source file or image
If you change the size of a layer or of the canvas in the source file, the importer regenerates the SpriteRects to match the new size and position of their source layers.

## Summary of source file modifications and their effects on SpriteRect data
| __Modification to the source file__             | __Effect on SpriteRect data__                                |
| ----------------------------------------------- | ------------------------------------------------------------ |
| __Add a new layer or enable layer visibility__ | The PSD importer automatically generates a new Sprite from the new layer, or newly visible layer, with its associated SpriteRect. |
| __Delete a layer or disable layer visibility__  | The PSD Importer deletes the associated Sprite and SpriteRect from the Project file. |
| __Rename a layer__                            | The PSD Importer renames the SpriteRect and its Sprite to match the new name of their source layer. |
| __Change a layer or canvas size__                | The importer regenerates the SpriteRect to match the new size and position of its source layer. |


## Name collision errors
A name collision error occurs when two or more layers have the same name. This can happen for the following reasons:

1. Two or more layers in the imported source file have the same name. However, Photoshop [group layers](https://helpx.adobe.com/photoshop/using/selecting-grouping-linking-layers.html#group_and_link_layers) with the same names do not cause this issue.<br/>
2. A previously hidden layer with the same name as another layer becomes visible and Unity imports it.

When a name collision occurs, the SpriteRect of the first layer in the source file, starting from the bottom of the layer stack, retains the original name, and the importer appends a running number (starting with 1) to the names of the other SpriteRects.
